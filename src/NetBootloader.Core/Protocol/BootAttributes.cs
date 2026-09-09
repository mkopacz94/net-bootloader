namespace NetBootloader.Core.Protocol;

/// <summary>
/// Bootloader attributes, as read via READ_VERSION and GET_MEMORY_ADDRESS_RANGE.
/// Mirrors <c>mcbootflash.protocol.BootAttrs</c>.
/// </summary>
/// <param name="Version">Bootloader version number.</param>
/// <param name="MaxPacketLength">
/// Maximum number of bytes which can be sent to the bootloader per packet. Includes
/// the size of the command packet itself plus associated data.
/// </param>
/// <param name="DeviceId">A device-specific identifier.</param>
/// <param name="EraseSize">
/// Size of a flash erase page in bytes. When erasing flash, the size of the memory
/// area erased must be given in number of erase pages.
/// </param>
/// <param name="WriteSize">
/// Size of a write block in bytes. When writing to flash, data must align with a
/// write block.
/// </param>
/// <param name="MemoryRangeStart">Low end of the writable program memory range.</param>
/// <param name="MemoryRangeEnd">
/// High end of the writable program memory range. The range is half-open, i.e. this
/// address itself is not part of the writable range.
/// </param>
public sealed record BootAttributes(
    int Version,
    int MaxPacketLength,
    int DeviceId,
    int EraseSize,
    int WriteSize,
    uint MemoryRangeStart,
    uint MemoryRangeEnd);
