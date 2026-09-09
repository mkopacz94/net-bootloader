using NetBootloader.Core.Protocol;

namespace NetBootloader.Core.HexFile;

/// <summary>
/// Splits an Intel HEX firmware image into <see cref="FirmwareChunk"/>s sized and
/// aligned for a given bootloader. Mirrors <c>mcbootflash.util.chunked</c>.
/// </summary>
public static class HexFileChunker
{
    /// <summary>
    /// Parses <paramref name="hexFilePath"/>, crops it to the bootloader's writable
    /// memory range, and splits it into appropriately sized, write-size-aligned
    /// chunks.
    /// </summary>
    /// <returns>The total number of bytes across all chunks (after alignment padding), and the chunks themselves.</returns>
    /// <exception cref="InvalidOperationException">
    /// If the HEX file contains no data within the program memory range, or the
    /// bootloader's reported packet/write sizes are inconsistent.
    /// </exception>
    public static (int TotalBytes, IReadOnlyList<FirmwareChunk> Chunks) Chunk(
        string hexFilePath, BootAttributes attrs)
    {
        var segments = IntelHexParser.Parse(hexFilePath);
        var cropped = CropToRange(segments, attrs.MemoryRangeStart, attrs.MemoryRangeEnd);

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
                chunks.Add(new FirmwareChunk(padded.Address + (uint)offset, pieceData));
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
