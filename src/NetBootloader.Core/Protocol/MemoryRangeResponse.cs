using System.Buffers.Binary;

namespace NetBootloader.Core.Protocol;

/// <summary>
/// Response to a GET_MEMORY_ADDRESS_RANGE command. Mirrors
/// <c>mcbootflash.protocol.MemoryRange</c>.
///
/// <code>
/// | [Response] | uint32        | uint32      |
/// | [Response] | program_start | program_end |
/// </code>
/// </summary>
public sealed class MemoryRangeResponse : Response
{
    /// <summary>Size in bytes of this response type on the wire.</summary>
    public new const int Size = Response.Size + 8;

    /// <summary>Low end of the address space to which application firmware can be flashed.</summary>
    public uint ProgramStart { get; private init; }

    /// <summary>
    /// High end (inclusive, as reported by the bootloader) of the address space to
    /// which application firmware can be flashed.
    /// </summary>
    public uint ProgramEnd { get; private init; }

    private MemoryRangeResponse(
        byte command, ushort dataLength, uint unlockSequence, uint address, ResponseCode success)
        : base(command, dataLength, unlockSequence, address, success)
    {
    }

    public static new MemoryRangeResponse Unpack(ReadOnlySpan<byte> data)
    {
        if (data.Length < Size)
        {
            throw new ArgumentException($"Memory range response requires at least {Size} bytes, got {data.Length}.", nameof(data));
        }

        var (command, dataLength, unlockSequence, address) = ReadHeader(data);
        var success = (ResponseCode)data[HeaderSize];

        return new MemoryRangeResponse(command, dataLength, unlockSequence, address, success)
        {
            ProgramStart = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(Response.Size, 4)),
            ProgramEnd = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(Response.Size + 4, 4)),
        };
    }
}
