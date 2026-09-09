using System.Buffers.Binary;

namespace NetBootloader.Core.Protocol;

/// <summary>
/// Response to a CALC_CHECKSUM command. Mirrors <c>mcbootflash.protocol.Checksum</c>.
///
/// <code>
/// | [Response] | uint16   |
/// | [Response] | checksum |
/// </code>
/// </summary>
public sealed class ChecksumResponse : Response
{
    /// <summary>Size in bytes of this response type on the wire.</summary>
    public new const int Size = Response.Size + 2;

    /// <summary>Checksum of <c>DataLength</c> bytes starting from <c>Address</c>, as calculated on-device.</summary>
    public ushort Checksum { get; private init; }

    private ChecksumResponse(
        byte command, ushort dataLength, uint unlockSequence, uint address, ResponseCode success)
        : base(command, dataLength, unlockSequence, address, success)
    {
    }

    public static new ChecksumResponse Unpack(ReadOnlySpan<byte> data)
    {
        if (data.Length < Size)
        {
            throw new ArgumentException($"Checksum response requires at least {Size} bytes, got {data.Length}.", nameof(data));
        }

        var (command, dataLength, unlockSequence, address) = ReadHeader(data);
        var success = (ResponseCode)data[HeaderSize];

        return new ChecksumResponse(command, dataLength, unlockSequence, address, success)
        {
            Checksum = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(Response.Size, 2)),
        };
    }
}
