namespace NetBootloader.Core.Protocol;

/// <summary>
/// Response to any command except READ_VERSION. Mirrors
/// <c>mcbootflash.protocol.Response</c>.
///
/// <code>
/// | [header] | uint8   |
/// | [header] | success |
/// </code>
/// </summary>
public class Response : BootloaderResponse
{
    /// <summary>Size in bytes of this response type on the wire.</summary>
    public const int Size = HeaderSize + 1;

    /// <summary>Success or failure status of the command this responds to.</summary>
    public ResponseCode Success { get; }

    protected Response(byte command, ushort dataLength, uint unlockSequence, uint address, ResponseCode success)
        : base(command, dataLength, unlockSequence, address)
    {
        Success = success;
    }

    public static Response Unpack(ReadOnlySpan<byte> data)
    {
        if (data.Length < Size)
        {
            throw new ArgumentException($"Response requires at least {Size} bytes, got {data.Length}.", nameof(data));
        }

        var (command, dataLength, unlockSequence, address) = ReadHeader(data);
        var success = (ResponseCode)data[HeaderSize];
        return new Response(command, dataLength, unlockSequence, address, success);
    }
}
