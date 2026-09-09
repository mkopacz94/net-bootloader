using NetBootloader.Core.Protocol;

namespace NetBootloader.Core.HexFile;

/// <summary>
/// Splits an Intel HEX firmware image into <see cref="FirmwareChunk"/>s sized and
/// aligned for a given bootloader. Mirrors <c>mcbootflash.util.chunked</c>.
/// </summary>
public static class HexFileChunker
{
    /// <summary>
    /// Parses the HEX file at <paramref name="hexFilePath"/>, crops it to the
    /// bootloader's writable memory range, and splits it into appropriately sized,
    /// write-size-aligned chunks.
    /// </summary>
    /// <returns>The total number of bytes across all chunks (after alignment padding), and the chunks themselves.</returns>
    /// <exception cref="InvalidOperationException">
    /// If the HEX file contains no data within the program memory range, or the
    /// bootloader's reported packet/write sizes are inconsistent.
    /// </exception>
    public static (int TotalBytes, IReadOnlyList<FirmwareChunk> Chunks) Chunk(
        string hexFilePath, BootAttributes attrs)
    {
        using var reader = new StreamReader(hexFilePath);
        return Chunk(reader, attrs);
    }

    /// <summary>
    /// Parses HEX content from <paramref name="hexReader"/> - e.g. a <see cref="StringReader"/>
    /// over content decrypted in memory - crops it to the bootloader's writable memory
    /// range, and splits it into appropriately sized, write-size-aligned chunks.
    /// </summary>
    /// <returns>The total number of bytes across all chunks (after alignment padding), and the chunks themselves.</returns>
    /// <exception cref="InvalidOperationException">
    /// If the HEX content has no data within the program memory range, or the
    /// bootloader's reported packet/write sizes are inconsistent.
    /// </exception>
    public static (int TotalBytes, IReadOnlyList<FirmwareChunk> Chunks) Chunk(
        TextReader hexReader, BootAttributes attrs)
    {
        var segments = IntelHexParser.Parse(hexReader);

        // Microchip's 16-bit HEX format encodes every address as twice the real
        // device (word) address - see IntelHexParser's doc comment. IntelHexParser
        // itself is address-format-agnostic and returns segments still in that raw,
        // file-literal space, so MemoryRangeStart/End (real device addresses, as
        // reported by GET_MEMORY_ADDRESS_RANGE) need scaling into the same space
        // before they're comparable to segment addresses for cropping. The final
        // chunk addresses get scaled back down below, once cropping/padding/
        // splitting - all byte-length-based, so unaffected by this - are done.
        //
        // Getting this wrong doesn't fail loudly: checksums are computed purely
        // from data bytes, not addresses, so a wrong-but-internally-consistent
        // address slips straight through per-chunk verification and only surfaces
        // as VERIFY_FAIL at the very end, or as a silently bricked device if
        // self-verify isn't checked either. Confirmed against a real device: the
        // exact same firmware self-verified fine through mcbootflash (whose
        // bincopy-based parser applies this same scaling) and failed through this
        // port before this fix.
        const uint microchipAddressScale = 2;
        var cropped = CropToRange(
            segments, attrs.MemoryRangeStart * microchipAddressScale, attrs.MemoryRangeEnd * microchipAddressScale);

        if (cropped.Sum(s => s.Data.Length) == 0)
        {
            throw new InvalidOperationException("HEX file contains no data within the program memory range.");
        }

        var chunkSize = attrs.MaxPacketLength - BootloaderCommand.Size;
        chunkSize -= chunkSize % attrs.WriteSize;

        if (chunkSize <= 0)
        {
            throw new InvalidOperationException("Bootloader's max packet length is too small for its write size.");
        }

        var chunks = new List<FirmwareChunk>();
        var totalBytes = 0;

        foreach (var segment in cropped)
        {
            var padded = PadToAlignment(segment, attrs.WriteSize);
            totalBytes += padded.Data.Length;

            for (var offset = 0; offset < padded.Data.Length; offset += chunkSize)
            {
                var length = Math.Min(chunkSize, padded.Data.Length - offset);
                var pieceData = new byte[length];
                Array.Copy(padded.Data, offset, pieceData, 0, length);
                var address = (padded.Address + (uint)offset) / microchipAddressScale;
                chunks.Add(new FirmwareChunk(address, pieceData));
            }
        }

        return (totalBytes, chunks);
    }

    private static List<HexSegment> CropToRange(IReadOnlyList<HexSegment> segments, uint start, uint end)
    {
        var result = new List<HexSegment>();

        foreach (var segment in segments)
        {
            var segmentEnd = segment.Address + (uint)segment.Data.Length;
            var croppedStart = Math.Max(segment.Address, start);
            var croppedEnd = Math.Min(segmentEnd, end);

            if (croppedStart >= croppedEnd)
            {
                continue;
            }

            var offset = (int)(croppedStart - segment.Address);
            var length = (int)(croppedEnd - croppedStart);
            var data = new byte[length];
            Array.Copy(segment.Data, offset, data, 0, length);
            result.Add(new HexSegment(croppedStart, data));
        }

        return result;
    }

    /// <summary>Pads a segment's tail with 0xFF (the erased-flash value) up to a multiple of <paramref name="writeSize"/>.</summary>
    private static HexSegment PadToAlignment(HexSegment segment, int writeSize)
    {
        var remainder = segment.Data.Length % writeSize;

        if (remainder == 0)
        {
            return segment;
        }

        var padded = new byte[segment.Data.Length + (writeSize - remainder)];
        Array.Copy(segment.Data, padded, segment.Data.Length);
        Array.Fill(padded, (byte)0xFF, segment.Data.Length, padded.Length - segment.Data.Length);
        return segment with { Data = padded };
    }
}
