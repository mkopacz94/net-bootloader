using System.Text;
using NetBootloader.Core.Communication;
using NetBootloader.Core.Exceptions;
using NetBootloader.Core.Protocol;

namespace NetBootloader.Core;

/// <summary>
/// Low-level operations for talking to Microchip's MCC 16-bit UART bootloader.
/// A direct translation of mcbootflash's <c>flash.py</c> module.
/// </summary>
public sealed class BootloaderClient
{
    private const uint FlashUnlockKey = 0x00AA0055;

    private readonly IBootloaderConnection _connection;

    /// <summary>
    /// Raised with a human-readable line for every packet sent or received. Hook this
    /// up to a log viewer or leave unsubscribed.
    /// </summary>
    public event Action<string>? DebugLog;

    public BootloaderClient(IBootloaderConnection connection)
    {
        _connection = connection;
    }

    /// <summary>Reads bootloader version, packet/erase/write sizes and the writable memory range.</summary>
    public BootAttributes GetBootAttributes()
    {
        var version = ReadVersion();
        var (start, end) = GetMemoryAddressRange();

        return new BootAttributes(
            version.Version,
            version.MaxPacketLength,
            version.DeviceId,
            version.EraseSize,
            version.WriteSize,
            start,
            end);
    }

    private VersionResponse ReadVersion()
    {
        var response = Exchange(new BootloaderCommand(CommandCode.ReadVersion));

        if (response is not VersionResponse version)
        {
            throw new BootloaderException($"Expected a version response, got {response.GetType().Name}.");
        }

        DebugLog?.Invoke("Got bootloader attributes:");
        DebugLog?.Invoke($"Max packet length: {version.MaxPacketLength}");
        DebugLog?.Invoke($"Erase size:        {version.EraseSize}");
        DebugLog?.Invoke($"Write size:        {version.WriteSize}");
        return version;
    }

    private (uint Start, uint End) GetMemoryAddressRange()
    {
        var response = Exchange(new BootloaderCommand(CommandCode.GetMemoryAddressRange));

        if (response is not MemoryRangeResponse memoryRange)
        {
            throw new BootloaderException($"Expected a memory range response, got {response.GetType().Name}.");
        }

        DebugLog?.Invoke(
            $"Got program memory range: {memoryRange.ProgramStart:X8}:{memoryRange.ProgramEnd:X8}");

        // +2 explained: the bootloader reports an inclusive upper bound, and the final
        // byte of the final 24-bit instruction isn't included in that bound, but it's
        // still writable. We want a half-open range, so +1 for each.
        return (memoryRange.ProgramStart, memoryRange.ProgramEnd + 2);
    }

    /// <summary>
    /// Erases a program memory area.
    /// </summary>
    /// <remarks>Erasing a large memory area can take several seconds - size the connection's read timeout accordingly.</remarks>
    /// <param name="eraseRangeStart">Start address of the (half-open) range to erase.</param>
    /// <param name="eraseRangeEnd">End address of the (half-open) range to erase; not itself erased.</param>
    /// <param name="eraseSize">Size of a flash page, i.e. the smallest atomically erasable unit.</param>
    /// <exception cref="ArgumentException">If the range length isn't a multiple of <paramref name="eraseSize"/>.</exception>
    public void EraseFlash(uint eraseRangeStart, uint eraseRangeEnd, int eraseSize)
    {
        var length = eraseRangeEnd - eraseRangeStart;

        if (length % eraseSize != 0)
        {
            throw new ArgumentException("Address range is not a multiple of erase size.");
        }

        DebugLog?.Invoke($"Erasing addresses {eraseRangeStart:X8}:{eraseRangeEnd:X8}");
        Exchange(new BootloaderCommand(
            CommandCode.EraseFlash,
            dataLength: (ushort)(length / eraseSize),
            unlockSequence: FlashUnlockKey,
            address: eraseRangeStart));
    }

    /// <summary>Writes a firmware chunk to the bootloader.</summary>
    public void WriteFlash(FirmwareChunk chunk)
    {
        DebugLog?.Invoke($"Writing {chunk.Data.Length} bytes to {chunk.Address:X8}");
        Exchange(
            new BootloaderCommand(
                CommandCode.WriteFlash,
                dataLength: checked((ushort)chunk.Data.Length),
                unlockSequence: FlashUnlockKey,
                address: chunk.Address),
            chunk.Data);
    }

    /// <summary>
    /// Runs the bootloader's self-verification.
    /// </summary>
    /// <exception cref="VerifyFailException">If no bootable application is detected in program memory.</exception>
    public void SelfVerify()
    {
        Exchange(new BootloaderCommand(CommandCode.SelfVerify));
    }

    /// <summary>
    /// Compares a locally-computed checksum against the bootloader's own checksum for
    /// the same address range.
    /// </summary>
    /// <exception cref="BootloaderException">If the checksums don't match.</exception>
    public void VerifyChecksum(FirmwareChunk chunk)
    {
        var localChecksum = ComputeLocalChecksum(chunk.Data);
        ushort remoteChecksum;

        try
        {
            remoteChecksum = GetRemoteChecksum(chunk.Address, chunk.Data.Length);
        }
        catch (BadAddressException)
        {
            DebugLog?.Invoke("Got BAD_ADDRESS while checksumming, continuing anyway.");
            DebugLog?.Invoke("This is probably a bug in the bootloader, not in this client.");
            DebugLog?.Invoke("See https://github.com/bessman/mcbootflash/issues/54");
            return;
        }

        if (localChecksum != remoteChecksum)
        {
            DebugLog?.Invoke($"Checksum mismatch: {localChecksum} != {remoteChecksum}");
            DebugLog?.Invoke("unlock_sequence field may be incorrect.");
            throw new BootloaderException("Checksum mismatch.");
        }

        DebugLog?.Invoke($"Checksum OK: {localChecksum}");
    }

    private ushort GetRemoteChecksum(uint address, int length)
    {
        var response = Exchange(new BootloaderCommand(
            CommandCode.CalcChecksum,
            dataLength: checked((ushort)length),
            address: address));

        if (response is not ChecksumResponse checksum)
        {
            throw new BootloaderException($"Expected a checksum response, got {response.GetType().Name}.");
        }

        return checksum.Checksum;
    }

    private static ushort ComputeLocalChecksum(byte[] data)
    {
        // Matches mcbootflash's checksum algorithm: sum the first 3 bytes of every
        // 4-byte group (24-bit instruction words are stored 3 real bytes + 1 phantom
        // byte at a time), truncated to 16 bits.
        const int extendedAddressWidth = 4;
        var checksum = 0;

        for (var offset = 0; offset < data.Length; offset += extendedAddressWidth)
        {
            checksum += data[offset];

            if (offset + 1 < data.Length)
            {
                checksum += data[offset + 1] << 8;
            }

            if (offset + 2 < data.Length)
            {
                checksum += data[offset + 2];
            }
        }

        return unchecked((ushort)checksum);
    }

    /// <summary>Resets the device.</summary>
    public void Reset()
    {
        Exchange(new BootloaderCommand(CommandCode.ResetDevice));
        DebugLog?.Invoke("Device reset");
    }

    /// <summary>Reads bytes from flash starting at <paramref name="address"/>.</summary>
    public byte[] ReadFlash(uint address, int size)
    {
        var response = Exchange(new BootloaderCommand(
            CommandCode.ReadFlash,
            dataLength: checked((ushort)size),
            address: address));
        return _connection.Read(response.DataLength);
    }

    private BootloaderResponse Exchange(BootloaderCommand command, ReadOnlySpan<byte> data = default)
    {
        var packed = command.Pack();
        DebugLog?.Invoke(
            $"TX: {FormatBytes(packed)}" + (data.IsEmpty ? "" : $" plus {data.Length} data bytes"));

        var payload = data.IsEmpty ? packed : Concat(packed, data);
        _connection.Write(payload);
        return GetResponse(command);
    }

    private BootloaderResponse GetResponse(BootloaderCommand inResponseTo)
    {
        // We can't read the whole response in one go: its length depends on whether
        // it's an error and which command it answers. Start with just the header.
        var header = _connection.Read(BootloaderPacket.HeaderSize);
        DebugLog?.Invoke($"RX: {FormatBytes(header)}");

        if (header[0] != (byte)inResponseTo.Command)
        {
            throw new BootloaderException("Command code mismatch.");
        }

        var commandCode = (CommandCode)header[0];

        if (commandCode == CommandCode.ReadVersion)
        {
            // READ_VERSION has no leading 'success' byte.
            var remainder = _connection.Read(VersionResponse.Size - BootloaderPacket.HeaderSize);
            DebugLog?.Invoke($"RX: {FormatBytes(remainder, header.Length)}");
            return VersionResponse.Unpack(Concat(header, remainder));
        }

        var successByte = _connection.Read(1);
        DebugLog?.Invoke($"RX: {FormatBytes(successByte, header.Length)}");
        var success = (ResponseCode)successByte[0];

        if (success != ResponseCode.Success)
        {
            throw BootloaderException.ForResponseCode(success);
        }

        var headerPlusSuccess = Concat(header, successByte);
        var fullSize = commandCode switch
        {
            CommandCode.GetMemoryAddressRange => MemoryRangeResponse.Size,
            CommandCode.CalcChecksum => ChecksumResponse.Size,
            _ => Response.Size,
        };

        var tail = _connection.Read(fullSize - headerPlusSuccess.Length);

        if (tail.Length > 0)
        {
            DebugLog?.Invoke($"RX: {FormatBytes(tail, headerPlusSuccess.Length)}");
        }

        var fullResponse = Concat(headerPlusSuccess, tail);

        return commandCode switch
        {
            CommandCode.GetMemoryAddressRange => MemoryRangeResponse.Unpack(fullResponse),
            CommandCode.CalcChecksum => ChecksumResponse.Unpack(fullResponse),
            _ => Response.Unpack(fullResponse),
        };
    }

    private static byte[] Concat(ReadOnlySpan<byte> first, ReadOnlySpan<byte> second)
    {
        var result = new byte[first.Length + second.Length];
        first.CopyTo(result);
        second.CopyTo(result.AsSpan(first.Length));
        return result;
    }

    private static string FormatBytes(ReadOnlySpan<byte> bytes, int padWithSpacesFor = 0)
    {
        var builder = new StringBuilder(new string(' ', padWithSpacesFor * 3));

        for (var i = 0; i < bytes.Length; i++)
        {
            if (i > 0)
            {
                builder.Append(' ');
            }

            builder.Append(bytes[i].ToString("X2"));
        }

        return builder.ToString();
    }
}
