using NetBootloader.Core.Exceptions;
using NetBootloader.Core.Protocol;
using Xunit;

namespace NetBootloader.Core.Tests;

public class ProtocolTests
{
    [Fact]
    public void Command_Pack_WritesLittleEndianHeader()
    {
        var command = new BootloaderCommand(
            CommandCode.WriteFlash,
            dataLength: 4,
            unlockSequence: 0x00AA0055,
            address: 0x00001000);

        var expected = new byte[] { 0x02, 0x04, 0x00, 0x55, 0x00, 0xAA, 0x00, 0x00, 0x10, 0x00, 0x00 };

        Assert.Equal(expected, command.Pack());
    }

    [Fact]
    public void Command_Pack_DefaultsAreAllZeroExceptCommand()
    {
        var command = new BootloaderCommand(CommandCode.ReadVersion);
        var expected = new byte[11];

        Assert.Equal(expected, command.Pack());
    }

    [Fact]
    public void VersionResponse_Unpack_ReadsAllFieldsAtCorrectOffsets()
    {
        var data = new byte[VersionResponse.Size];
        data[0] = (byte)CommandCode.ReadVersion;
        WriteUInt16(data, 11, 261); // version
        WriteUInt16(data, 13, 256); // max_packet_length
        WriteUInt16(data, 17, 0x1234); // device_id
        WriteUInt16(data, 21, 1024); // erase_size
        WriteUInt16(data, 23, 8); // write_size

        var version = VersionResponse.Unpack(data);

        Assert.Equal(261, version.Version);
        Assert.Equal(256, version.MaxPacketLength);
        Assert.Equal(0x1234, version.DeviceId);
        Assert.Equal(1024, version.EraseSize);
        Assert.Equal(8, version.WriteSize);
    }

    [Fact]
    public void Response_Unpack_ReadsCommandEchoAndSuccessByte()
    {
        var data = new byte[Response.Size];
        data[0] = (byte)CommandCode.EraseFlash;
        data[11] = (byte)ResponseCode.Success;

        var response = Response.Unpack(data);

        Assert.Equal((byte)CommandCode.EraseFlash, response.Command);
        Assert.Equal(ResponseCode.Success, response.Success);
    }

    [Fact]
    public void MemoryRangeResponse_Unpack_ReadsProgramBounds()
    {
        var data = new byte[MemoryRangeResponse.Size];
        data[0] = (byte)CommandCode.GetMemoryAddressRange;
        data[11] = (byte)ResponseCode.Success;
        WriteUInt32(data, 12, 0x00001000);
        WriteUInt32(data, 16, 0x0000FFFE);

        var response = MemoryRangeResponse.Unpack(data);

        Assert.Equal(0x00001000u, response.ProgramStart);
        Assert.Equal(0x0000FFFEu, response.ProgramEnd);
    }

    [Fact]
    public void ChecksumResponse_Unpack_ReadsChecksum()
    {
        var data = new byte[ChecksumResponse.Size];
        data[0] = (byte)CommandCode.CalcChecksum;
        data[11] = (byte)ResponseCode.Success;
        WriteUInt16(data, 12, 0xBEEF & 0xFFFF);

        var response = ChecksumResponse.Unpack(data);

        Assert.Equal((ushort)0xBEEF, response.Checksum);
    }

    [Theory]
    [InlineData(ResponseCode.UnsupportedCommand, typeof(UnsupportedCommandException))]
    [InlineData(ResponseCode.BadAddress, typeof(BadAddressException))]
    [InlineData(ResponseCode.BadLength, typeof(BadLengthException))]
    [InlineData(ResponseCode.VerifyFail, typeof(VerifyFailException))]
    public void BootloaderException_ForResponseCode_MapsToSpecificSubclass(ResponseCode code, Type expectedType)
    {
        var exception = BootloaderException.ForResponseCode(code);
        Assert.IsType(expectedType, exception);
    }

    private static void WriteUInt16(byte[] buffer, int offset, int value)
    {
        buffer[offset] = (byte)(value & 0xFF);
        buffer[offset + 1] = (byte)((value >> 8) & 0xFF);
    }

    private static void WriteUInt32(byte[] buffer, int offset, uint value)
    {
        buffer[offset] = (byte)(value & 0xFF);
        buffer[offset + 1] = (byte)((value >> 8) & 0xFF);
        buffer[offset + 2] = (byte)((value >> 16) & 0xFF);
        buffer[offset + 3] = (byte)((value >> 24) & 0xFF);
    }
}
