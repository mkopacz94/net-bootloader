using NetBootloader.Core.Exceptions;
using NetBootloader.Core.HexFile;
using NetBootloader.Core.Protocol;

namespace NetBootloader.Core;

/// <summary>
/// High-level firmware flashing workflow: handshake, erase, write, verify, optionally
/// reset. A translation of the orchestration logic in mcbootflash's
/// <c>__main__.py</c>, exposed as an awaitable operation with progress reporting
/// suitable for driving a WPF progress bar.
/// </summary>
public sealed class FirmwareFlasher
{
    private readonly BootloaderClient _client;

    public FirmwareFlasher(BootloaderClient client)
    {
        _client = client;
    }

    /// <summary>Forwards every packet trace line from the underlying <see cref="BootloaderClient"/>.</summary>
    public event Action<string>? DebugLog
    {
        add => _client.DebugLog += value;
        remove => _client.DebugLog -= value;
    }

    /// <summary>
    /// Reads bootloader attributes, erases the program memory area, writes the HEX file
    /// at <paramref name="hexFilePath"/> in chunks, runs self-verification, and
    /// optionally resets the device.
    /// </summary>
    /// <param name="hexFilePath">Path to an Intel HEX file containing application firmware.</param>
    /// <param name="verifyChecksum">If true, checksums each chunk against the device after writing it.</param>
    /// <param name="resetAfterFlash">If true, resets the device once flashing and self-verification succeed.</param>
    /// <param name="progress">Optional progress sink.</param>
    /// <param name="cancellationToken">Checked between chunks and erase pages.</param>
    /// <returns>The bootloader attributes read at the start of the operation.</returns>
    /// <exception cref="VerifyFailException">If self-verification fails after flashing.</exception>
    /// <exception cref="BootloaderException">If any protocol exchange fails.</exception>
    public Task<BootAttributes> FlashAsync(
        string hexFilePath,
        bool verifyChecksum,
        bool resetAfterFlash,
        IProgress<FlashProgressReport>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(
            () =>
            {
                using var reader = new StreamReader(hexFilePath);
                return Flash(reader, verifyChecksum, resetAfterFlash, progress, cancellationToken);
            },
            cancellationToken);
    }

    /// <summary>
    /// Same as <see cref="FlashAsync(string, bool, bool, IProgress{FlashProgressReport}?, CancellationToken)"/>,
    /// but reads HEX content from <paramref name="hexReader"/> instead of a file path -
    /// e.g. a <see cref="StringReader"/> over content decrypted in memory, so the
    /// plaintext HEX never touches disk.
    /// </summary>
    public Task<BootAttributes> FlashAsync(
        TextReader hexReader,
        bool verifyChecksum,
        bool resetAfterFlash,
        IProgress<FlashProgressReport>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(
            () => Flash(hexReader, verifyChecksum, resetAfterFlash, progress, cancellationToken),
            cancellationToken);
    }

    private BootAttributes Flash(
        TextReader hexReader,
        bool verifyChecksum,
        bool resetAfterFlash,
        IProgress<FlashProgressReport>? progress,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        progress?.Report(new FlashProgressReport(FlashStage.Handshaking, 0, 0));
        var attrs = _client.GetBootAttributes();

        var (totalBytes, chunks) = HexFileChunker.Chunk(hexReader, attrs);

        EraseProgramMemory(attrs, progress, cancellationToken);
        WriteChunks(chunks, totalBytes, verifyChecksum, progress, cancellationToken);

        progress?.Report(new FlashProgressReport(FlashStage.SelfVerifying, 0, 0));
        _client.SelfVerify();

        if (resetAfterFlash)
        {
            progress?.Report(new FlashProgressReport(FlashStage.Resetting, 0, 0));
            _client.Reset();
        }

        return attrs;
    }

    private void EraseProgramMemory(
        BootAttributes attrs, IProgress<FlashProgressReport>? progress, CancellationToken cancellationToken)
    {
        var start = attrs.MemoryRangeStart;
        var end = attrs.MemoryRangeEnd;
        var eraseSize = (uint)attrs.EraseSize;
        var totalBytes = (int)(end - start);
        var erasedBytes = 0;
        var pageStart = start;

        while (pageStart < end)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var pageEnd = Math.Min(pageStart + eraseSize, end);

            try
            {
                _client.EraseFlash(pageStart, pageEnd, attrs.EraseSize);
            }
            catch (BadAddressException)
            {
                // Some bootloader versions incorrectly think addresses greater than
                // 0xFFFF are misaligned. Work around it by erasing everything
                // remaining - including the last page that's known to have erased
                // successfully, as a safety margin - in a single command. This can
                // take a while; make sure the connection's read timeout allows for it.
                var safeStart = pageStart >= start + eraseSize ? pageStart - eraseSize : start;
                _client.EraseFlash(safeStart, end, attrs.EraseSize);
                pageEnd = end;
            }

            erasedBytes += (int)(pageEnd - pageStart);
            progress?.Report(new FlashProgressReport(FlashStage.Erasing, erasedBytes, totalBytes));
            pageStart = pageEnd;
        }
    }

    private void WriteChunks(
        IReadOnlyList<FirmwareChunk> chunks,
        int totalBytes,
        bool verifyChecksum,
        IProgress<FlashProgressReport>? progress,
        CancellationToken cancellationToken)
    {
        var writtenBytes = 0;

        foreach (var chunk in chunks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _client.WriteFlash(chunk);

            if (verifyChecksum)
            {
                _client.VerifyChecksum(chunk);
            }

            writtenBytes += chunk.Data.Length;
            progress?.Report(new FlashProgressReport(FlashStage.Writing, writtenBytes, totalBytes));
        }
    }
}
