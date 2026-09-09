using System.Buffers.Binary;

namespace NetBootloader.Core.Protocol;

/// <summary>
/// Common 11-byte header shared by every packet exchanged with the bootloader.
/// Mirrors the layout of <c>mcbootflash.protocol.Packet</c>:
///
/// <code>
/// | uint8   | uint16      | uint32          | uint32   |
/// | command | data_length | unlock_sequence | address  |
/// </code>
///
/// All multi-byte fields are little-endian.
/// </summary>
public abstract class BootloaderPacket
{
    /// <summary>Size in bytes of the header common to all packets.</summary>
    public const int HeaderSize = 11;

    /// <summary>
    /// Command code. For a <see cref="BootloaderCommand"/> this is the command being
    /// sent; for a response it is an echo of the command it answers.
    /// </summary>
    public byte Command { get; }

    /// <summary>
    /// Meaning depends on <see cref="Command"/>: number of data bytes following a
    /// WRITE_FLASH command, number of flash pages for ERASE_FLASH, or number of bytes
    /// to checksum for CALC_CHECKSUM. Ignored for other commands.
    /// </summary>
    public ushort DataLength { get; }

    /// <summary>
    /// Key required to unlock flash memory for writing. Write operations
    /// (WRITE_FLASH, ERASE_FLASH) fail <em>silently</em> if this is incorrect - the
    /// bootloader still reports success.
    /// </summary>
    public uint UnlockSequence { get; }

    /// <summary>Address at which to perform the command.</summary>
    public uint Address { get; }

    protected BootloaderPacket(byte command, ushort dataLength, uint unlockSequence, uint address)
    {
        Command = command;
        DataLength = dataLength;
        UnlockSequence = unlockSequence;
        Address = address;
    }

    protected void WriteHeader(Span<byte> destination)
    {
        destination[0] = Command;
        BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(1, 2), DataLength);
        BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(3, 4), UnlockSequence);
        BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(7, 4), Address);
    }

    protected static (byte Command, ushort DataLength, uint UnlockSequence, uint Address) ReadHeader(
        ReadOnlySpan<byte> source)
    {
        if (source.Length < HeaderSize)
        {
            throw new ArgumentException(
                $"Header requires at least {HeaderSize} bytes, got {source.Length}.", nameof(source));
        }

        return (
            source[0],
            BinaryPrimitives.ReadUInt16LittleEndian(source.Slice(1, 2)),
            BinaryPrimitives.ReadUInt32LittleEndian(source.Slice(3, 4)),
            BinaryPrimitives.ReadUInt32LittleEndian(source.Slice(7, 4)));
    }
}
