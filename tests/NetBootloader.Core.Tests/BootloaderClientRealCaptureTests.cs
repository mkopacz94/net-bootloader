using NetBootloader.Core.Exceptions;
using NetBootloader.Core.Protocol;
using Xunit;

namespace NetBootloader.Core.Tests;

/// <summary>
/// Regression tests built from real wire captures: two from mcbootflash's own CLI
/// (Python, the reference this port is based on) and one from Microchip's official
/// Unified Bootloader Host Application (UBHA) - an independent implementation talking
/// to the same bootloader firmware. Byte sequences below are copied verbatim from
/// those logs, so these tests confirm this port's framing against real device traffic
/// rather than just against itself.
/// </summary>
public class BootloaderClientRealCaptureTests
{
    // From bootloader_bez_CRC_OK.txt / bootloader_z_CRC_ERR.txt (mcbootflash --debug),
    // lines 5-16: READ_VERSION followed by GET_MEMORY_ADDRESS_RANGE.
    [Fact]
    public void GetBootAttributes_MatchesRealCapturedHandshake()
    {
        var connection = new FakeBootloaderConnection(
            Bytes("00 00 00 00 00 00 00 00 00 00 00"), // READ_VERSION header echo
            Bytes("02 01 00 01 00 00 56 34 00 00 00 04 04 00 00 00 00 00 00 00 00 00 00 00 00 00"),
            Bytes("0B 08 00 00 00 00 00 00 00 00 00"), // GET_MEMORY_ADDRESS_RANGE header echo
            Bytes("01"),
            Bytes("00 38 00 00 FE 53 01 00"));
        var client = new BootloaderClient(connection);

        var attrs = client.GetBootAttributes();

        Assert.Equal(258, attrs.Version); // 0x0102
        Assert.Equal(256, attrs.MaxPacketLength);
        Assert.Equal(0x3456, attrs.DeviceId);
        Assert.Equal(1024, attrs.EraseSize);
        Assert.Equal(4, attrs.WriteSize);
        Assert.Equal(0x00003800u, attrs.MemoryRangeStart);
        // Bootloader reports an inclusive 0x0153FE; half-open range is +2 (see BootloaderClient).
        Assert.Equal(0x00015400u, attrs.MemoryRangeEnd);

        Assert.Equal(Bytes("00 00 00 00 00 00 00 00 00 00 00"), connection.WrittenPackets[0]);
        Assert.Equal(Bytes("0B 00 00 00 00 00 00 00 00 00 00"), connection.WrittenPackets[1]);
    }

    // Same log, lines 18-21: the first ERASE_FLASH page.
    [Fact]
    public void EraseFlash_MatchesRealCapturedCommandAndAck()
    {
        var connection = new FakeBootloaderConnection(
            Bytes("03 01 00 55 00 AA 00 00 38 00 00"),
            Bytes("01"));
        var client = new BootloaderClient(connection);

        client.EraseFlash(0x00003800, 0x00003C00, eraseSize: 1024);

        Assert.Equal(Bytes("03 01 00 55 00 AA 00 00 38 00 00"), Assert.Single(connection.WrittenPackets));
    }

    // bootloader_bez_CRC_OK.txt lines 299-307: a WRITE_FLASH followed by a matching
    // CALC_CHECKSUM ("Checksum OK: 29627"). The log doesn't record the actual firmware
    // data bytes (mcbootflash only logs the command header for TX), so this test
    // supplies data engineered to reproduce the same local checksum mcbootflash
    // computed, and asserts the real captured CALC_CHECKSUM response bytes decode to
    // that same value without mismatch.
    [Fact]
    public void VerifyChecksum_AcceptsRealCapturedMatchingResponse()
    {
        var connection = new FakeBootloaderConnection(
            Bytes("02 F4 00 55 00 AA 00 00 38 00 00"), // WRITE_FLASH header echo
            Bytes("01"),
            Bytes("08 F4 00 00 00 00 00 00 38 00 00"), // CALC_CHECKSUM header echo
            Bytes("01"),
            Bytes("BB 73")); // checksum = 0x73BB = 29627, as printed in the log
        var client = new BootloaderClient(connection);

        var data = MakeChecksummingTo(29627, length: 244);
        var chunk = new FirmwareChunk(0x00003800, data);

        client.WriteFlash(chunk);
        var exception = Record.Exception(() => client.VerifyChecksum(chunk));

        Assert.Null(exception);
        Assert.Equal(Bytes("08 F4 00 00 00 00 00 00 38 00 00"), connection.WrittenPackets[1]);
    }

    // bootloader_z_CRC_ERR.txt, the final exchange (lines ~4270-4278): a real checksum
    // mismatch ("Checksum mismatch: 3120 != 2100") that made the actual mcbootflash run
    // fail. Remote checksum bytes below (34 08 = 0x0834 = 2100) are copied verbatim;
    // local data is arbitrary bytes that don't sum to 2100, reproducing the mismatch.
    [Fact]
    public void VerifyChecksum_ThrowsOnRealCapturedMismatch()
    {
        var connection = new FakeBootloaderConnection(
            Bytes("02 F4 00 55 00 AA 00 00 50 01 00"),
            Bytes("01"),
            Bytes("08 F4 00 00 00 00 00 00 50 01 00"),
            Bytes("01"),
            Bytes("34 08")); // checksum = 0x0834 = 2100, as printed in the log
        var client = new BootloaderClient(connection);

        var data = new byte[244]; // all zero -> local checksum is 0, not 2100
        var chunk = new FirmwareChunk(0x00015000, data);

        client.WriteFlash(chunk);
        var exception = Assert.Throws<BootloaderException>(() => client.VerifyChecksum(chunk));
        Assert.Equal("Checksum mismatch.", exception.Message);
    }

    // bootloader_z_CRC_OK.txt (Microchip's own UBHA tool - an independent
    // implementation), the read-back verification near the end of the log:
    // "Reading 0x153D0 to 0x15400" / Sent 0x01 0x60 0x00 0x55 0x00 0xAA 0x00 0xD0 0x53
    // 0x01 0x00 / Received (ack + 96 zero bytes). Note UBHA sends the flash unlock key
    // even for a read; mcbootflash's read_flash - and this port - doesn't, since the
    // protocol docs say unlock_sequence only matters for WRITE_FLASH/ERASE_FLASH. The
    // header echo below reflects what *this* command sends (unlock_sequence=0); the
    // response's data_length echo and the 96 payload bytes are the real captured
    // device behavior being verified.
    [Fact]
    public void ReadFlash_MatchesRealCapturedUbhaReadback()
    {
        var expectedData = new byte[96]; // log shows all-zero bytes for this page
        var connection = new FakeBootloaderConnection(
            Bytes("01 60 00 00 00 00 00 D0 53 01 00"), // READ_FLASH header echo
            Bytes("01"),
            expectedData);
        var client = new BootloaderClient(connection);

        var result = client.ReadFlash(0x000153D0, 96);

        Assert.Equal(expectedData, result);
        Assert.Equal(Bytes("01 60 00 00 00 00 00 D0 53 01 00"), Assert.Single(connection.WrittenPackets));
    }

    private static byte[] Bytes(string hex) =>
        hex.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(h => Convert.ToByte(h, 16))
            .ToArray();

    /// <summary>
    /// Builds a buffer whose checksum, per BootloaderClient's algorithm (sum the
    /// first 3 bytes of every 4-byte group, truncated to 16 bits), equals
    /// <paramref name="target"/>.
    /// </summary>
    private static byte[] MakeChecksummingTo(ushort target, int length)
    {
        var data = new byte[length];

        for (var offset = 0; offset < length; offset += 4)
        {
            data[offset + 3] = 0xFF; // phantom byte, ignored by the checksum
        }

        data[0] = (byte)(target & 0xFF);

        if (length > 1)
        {
            data[1] = (byte)((target >> 8) & 0xFF);
        }

        return data;
    }
}
