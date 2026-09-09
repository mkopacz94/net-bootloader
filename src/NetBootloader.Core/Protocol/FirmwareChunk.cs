namespace NetBootloader.Core.Protocol;

/// <summary>
/// A piece of a firmware image, sized and aligned to fit the bootloader's
/// <see cref="BootAttributes.MaxPacketLength"/> and <see cref="BootAttributes.WriteSize"/>.
/// Mirrors <c>mcbootflash.protocol.Chunk</c>.
/// </summary>
/// <param name="Address">
/// Start address of <paramref name="Data"/>. Must be a multiple of the bootloader's
/// write size.
/// </param>
/// <param name="Data">
/// Data to be written to the bootloader. Its length must be a multiple of the
/// bootloader's write size, and no longer than
/// <c>MaxPacketLength - BootloaderCommand.Size</c>.
/// </param>
public sealed record FirmwareChunk(uint Address, byte[] Data);
