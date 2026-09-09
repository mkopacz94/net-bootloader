using System.Buffers.Binary;

namespace NetBootloader.Core.Protocol;

/// <summary>
/// Response to a READ_VERSION command. Unlike every other response, this one has no
/// leading "success" byte. Mirrors <c>mcbootflash.protocol.Version</c>.
///
/// <code>
/// | [header] | uint16  | uint16            | uint16    | uint16    | ...
/// | [header] | version | max_packet_length | (ignored) | device_id | ...
///
/// ... | uint16    | uint16     | uint16     | 12 bytes (ignored) |
/// ... | (ignored) | erase_size | write_size |                    |
/// </code>
/// </summary>
public sealed class VersionResponse : BootloaderResponse
{
    /// <summary>Size in bytes of this response type on the wire.</summary>
    public const int Size = HeaderSize + 26;

    /// <summary>Bootloader version number.</summary>
    public ushort Version { get; private init; }

    /// <summary>
    /// Maximum number of bytes which can be sent to the bootloader per packet.
    /// Includes the size of the command packet itself plus associated data.
    /// </summary>
    public ushort MaxPacketLength { get; private init; }

    /// <summary>A device-specific identifier.</summary>
    public ushort DeviceId { get; private init; }

    /// <summary>
    /// Size of a flash erase page in bytes. When erasing flash, the size of the memory
    /// area erased must be given in number of erase pages.
    /// </summary>
    public ushort EraseSize { get; private init; }

    /// <summary>
    /// Size of a write block in bytes. When writing to flash, data must align with a
    /// write block.
    /// </summary>
    public ushort WriteSize { get; private init; }

    private VersionResponse(byte command, ushort dataLength, uint unlockSequence, uint address)
        : base(command, dataLength, unlockSequence, address)
    {
    }

    public static VersionResponse Unpack(ReadOnlySpan<byte> data)
    {
        if (data.Length < Size)
        {
            throw new ArgumentException($"Version response requires at least {Size} bytes, got {data.Length}.", nameof(data));
        }

        var (command, dataLength, unlockSequence, address) = ReadHeader(data);

        return new VersionResponse(command, dataLength, unlockSequence, address)
        {
            Version = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(HeaderSize, 2)),
            MaxPacketLength = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(HeaderSize + 2, 2)),
            // 2 bytes ignored at offset HeaderSize + 4
            DeviceId = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(HeaderSize + 6, 2)),
            // 2 bytes ignored at offset HeaderSize + 8
            EraseSize = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(HeaderSize + 10, 2)),
            WriteSize = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(HeaderSize + 12, 2)),
            // 12 trailing bytes ignored
        };
    }
}
