namespace NetBootloader.Core.Protocol;

/// <summary>
/// A packet sent to the bootloader. Layout is identical to the common header -
/// mirrors <c>mcbootflash.protocol.Command</c>.
/// </summary>
public sealed class BootloaderCommand : BootloaderPacket
{
    /// <summary>Size in bytes of a command packet on the wire.</summary>
    public const int Size = HeaderSize;

    public BootloaderCommand(
        CommandCode command,
        ushort dataLength = 0,
        uint unlockSequence = 0,
        uint address = 0)
        : base((byte)command, dataLength, unlockSequence, address)
    {
    }

    public byte[] Pack()
    {
        var buffer = new byte[Size];
        WriteHeader(buffer);
        return buffer;
    }
}
