namespace NetBootloader.Core.Protocol;

/// <summary>
/// Status byte sent by the bootloader in response to a command.
/// Mirrors <c>mcbootflash.protocol.ResponseCode</c>.
/// </summary>
public enum ResponseCode : byte
{
    Success = 0x01,
    UnsupportedCommand = 0xFF,
    BadAddress = 0xFE,
    BadLength = 0xFD,
    VerifyFail = 0xFC,
}
