namespace NetBootloader.Core.Protocol;

/// <summary>
/// Base class for packets received from the bootloader. Layout is identical to the
/// common header - mirrors <c>mcbootflash.protocol.ResponseBase</c>.
/// </summary>
public abstract class BootloaderResponse : BootloaderPacket
{
    protected BootloaderResponse(byte command, ushort dataLength, uint unlockSequence, uint address)
        : base(command, dataLength, unlockSequence, address)
    {
    }
}
