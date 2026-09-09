namespace NetBootloader.Core.Protocol;

/// <summary>
/// Commands supported by Microchip's MCC-generated 16-bit UART bootloader.
/// Mirrors <c>mcbootflash.protocol.CommandCode</c>.
/// </summary>
public enum CommandCode : byte
{
    ReadVersion = 0x00,
    ReadFlash = 0x01,
    WriteFlash = 0x02,
    EraseFlash = 0x03,
    CalcChecksum = 0x08,
    ResetDevice = 0x09,
    SelfVerify = 0x0A,
    GetMemoryAddressRange = 0x0B,
}
