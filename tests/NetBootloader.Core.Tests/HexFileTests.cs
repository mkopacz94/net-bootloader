using System.Text;
using NetBootloader.Core.HexFile;
using NetBootloader.Core.Protocol;
using Xunit;

namespace NetBootloader.Core.Tests;

public class HexFileTests
{
    [Fact]
    public void Parse_SingleDataRecord_ProducesOneSegment()
    {
        var hex = BuildFile(BuildDataRecord(0x0000, new byte[] { 0x00, 0x01, 0x02, 0x03 }));

        var segments = IntelHexParser.Parse(new StringReader(hex));

        var segment = Assert.Single(segments);
        Assert.Equal(0x0000u, segment.Address);
        Assert.Equal(new byte[] { 0x00, 0x01, 0x02, 0x03 }, segment.Data);
    }

    [Fact]
    public void Parse_ExtendedLinearAddress_OffsetsSubsequentDataRecords()
    {
        var hex = BuildFile(
            BuildExtendedLinearAddressRecord(0x0001), // upper 16 bits -> base 0x00010000
            BuildDataRecord(0x0010, new byte[] { 0xAA, 0xBB }));

        var segments = IntelHexParser.Parse(new StringReader(hex));

        var segment = Assert.Single(segments);
        Assert.Equal(0x00010010u, segment.Address);
        Assert.Equal(new byte[] { 0xAA, 0xBB }, segment.Data);
    }

    [Fact]
    public void Parse_NonContiguousRecords_ProduceSeparateSegments()
    {
        var hex = BuildFile(
            BuildDataRecord(0x0000, new byte[] { 0x11, 0x22 }),
            BuildDataRecord(0x0100, new byte[] { 0x33, 0x44 }));

        var segments = IntelHexParser.Parse(new StringReader(hex));

        Assert.Equal(2, segments.Count);
        Assert.Equal(0x0000u, segments[0].Address);
        Assert.Equal(0x0100u, segments[1].Address);
    }

    [Fact]
    public void Parse_StopsAtEndOfFileRecord()
    {
        var hex = BuildFile(
            BuildDataRecord(0x0000, new byte[] { 0x11 }),
            BuildEndOfFileRecord()) + BuildDataRecord(0x0100, new byte[] { 0x22 }) + "\n";

        var segments = IntelHexParser.Parse(new StringReader(hex));

        var segment = Assert.Single(segments);
        Assert.Equal(0x0000u, segment.Address);
    }

    [Fact]
    public void Parse_CorruptedChecksum_Throws()
    {
        var line = BuildDataRecord(0x0000, new byte[] { 0x01 }).TrimEnd('\n');
        // Flip the last checksum digit to corrupt it.
        var corrupted = line[..^1] + (line[^1] == '0' ? '1' : '0') + "\n";

        Assert.Throws<FormatException>(() => IntelHexParser.Parse(new StringReader(corrupted)));
    }

    [Fact]
    public void Chunker_PadsToWriteSizeAndSplitsAtMaxPacketLength()
    {
        var tempFile = Path.GetTempFileName();

        try
        {
            File.WriteAllText(
                tempFile,
                BuildFile(BuildDataRecord(0x0100, Enumerable.Range(0, 10).Select(i => (byte)i).ToArray())));

            // chunkSize = MaxPacketLength(19) - Command.Size(11) = 8, already a
            // multiple of WriteSize(4).
            var attrs = new BootAttributes(
                Version: 1,
                MaxPacketLength: 19,
                DeviceId: 0,
                EraseSize: 1024,
                WriteSize: 4,
                MemoryRangeStart: 0x0000,
                MemoryRangeEnd: 0x1000);

            var (totalBytes, chunks) = HexFileChunker.Chunk(tempFile, attrs);

            // 10 bytes padded up to the next multiple of 4 -> 12.
            Assert.Equal(12, totalBytes);
            Assert.Equal(2, chunks.Count);

            // File address 0x0100 -> real device address 0x0080 (Microchip HEX
            // addresses are twice the real address - see IntelHexParser's docs).
            Assert.Equal(0x0080u, chunks[0].Address);
            Assert.Equal(8, chunks[0].Data.Length);
            Assert.Equal(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7 }, chunks[0].Data);

            Assert.Equal(0x0084u, chunks[1].Address);
            Assert.Equal(4, chunks[1].Data.Length);
            // Bytes 8 and 9 are real data; the rest is 0xFF padding.
            Assert.Equal(new byte[] { 8, 9, 0xFF, 0xFF }, chunks[1].Data);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void Chunker_CropsDataOutsideMemoryRange()
    {
        var tempFile = Path.GetTempFileName();

        try
        {
            File.WriteAllText(
                tempFile,
                BuildFile(BuildDataRecord(0x0000, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 })));

            // MemoryRangeStart/End are real device addresses; internally they're
            // doubled to file-literal space (0x0004..0x000C) before cropping the
            // file's data (at file addresses 0x0000..0x0008), keeping file addresses
            // 4-7 (values 5-8). See the address-scale comment in HexFileChunker.
            var attrs = new BootAttributes(1, 19, 0, 1024, 4, MemoryRangeStart: 0x0002, MemoryRangeEnd: 0x0006);

            var (totalBytes, chunks) = HexFileChunker.Chunk(tempFile, attrs);

            Assert.Equal(4, totalBytes);
            var chunk = Assert.Single(chunks);
            Assert.Equal(0x0002u, chunk.Address);
            Assert.Equal(new byte[] { 5, 6, 7, 8 }, chunk.Data);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void Chunker_ScalesMicrochipFileAddressToRealDeviceAddress()
    {
        // The actual bug this guards against: the same firmware self-verified fine
        // through mcbootflash (whose bincopy-based parser halves Microchip HEX
        // addresses) and failed self-verify through this port before this fix,
        // because file addresses were being sent to the device unscaled.
        var tempFile = Path.GetTempFileName();

        try
        {
            // File address 0x0010 -> real device address 0x0008.
            File.WriteAllText(
                tempFile,
                BuildFile(BuildDataRecord(0x0010, new byte[] { 0xAA, 0xBB, 0xCC, 0xDD })));

            var attrs = new BootAttributes(1, 19, 0, 1024, 4, MemoryRangeStart: 0x0000, MemoryRangeEnd: 0x1000);

            var (totalBytes, chunks) = HexFileChunker.Chunk(tempFile, attrs);

            Assert.Equal(4, totalBytes);
            var chunk = Assert.Single(chunks);
            Assert.Equal(0x0008u, chunk.Address);
            Assert.Equal(new byte[] { 0xAA, 0xBB, 0xCC, 0xDD }, chunk.Data);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void Chunker_NoDataInRange_Throws()
    {
        var tempFile = Path.GetTempFileName();

        try
        {
            File.WriteAllText(tempFile, BuildFile(BuildDataRecord(0x0000, new byte[] { 1, 2 })));
            var attrs = new BootAttributes(1, 19, 0, 1024, 4, MemoryRangeStart: 0x0100, MemoryRangeEnd: 0x0200);

            Assert.Throws<InvalidOperationException>(() => HexFileChunker.Chunk(tempFile, attrs));
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    private static string BuildFile(params string[] records) => string.Concat(records);

    private static string BuildDataRecord(ushort address, byte[] data) =>
        BuildRecord(recordType: 0x00, address, data);

    private static string BuildExtendedLinearAddressRecord(ushort upper16) =>
        BuildRecord(recordType: 0x04, address: 0x0000, data: new byte[] { (byte)(upper16 >> 8), (byte)(upper16 & 0xFF) });

    private static string BuildEndOfFileRecord() => BuildRecord(recordType: 0x01, address: 0x0000, data: Array.Empty<byte>());

    private static string BuildRecord(byte recordType, ushort address, byte[] data)
    {
        var body = new byte[4 + data.Length];
        body[0] = (byte)data.Length;
        body[1] = (byte)(address >> 8);
        body[2] = (byte)(address & 0xFF);
        body[3] = recordType;
        Array.Copy(data, 0, body, 4, data.Length);

        var sum = 0;
        foreach (var b in body)
        {
            sum += b;
        }

        var checksum = unchecked((byte)-sum);

        var builder = new StringBuilder(":");
        foreach (var b in body)
        {
            builder.Append(b.ToString("X2"));
        }

        builder.Append(checksum.ToString("X2"));
        builder.Append('\n');
        return builder.ToString();
    }
}
