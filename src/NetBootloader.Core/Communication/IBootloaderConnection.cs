namespace NetBootloader.Core.Communication;

/// <summary>
/// Anything that can read and write bytes to/from the bootloader. Typically an open
/// serial port. Mirrors <c>mcbootflash.protocol.Connection</c>.
/// </summary>
public interface IBootloaderConnection
{
    /// <summary>
    /// Reads exactly <paramref name="count"/> bytes.
    /// </summary>
    /// <exception cref="TimeoutException">
    /// Thrown if fewer than <paramref name="count"/> bytes arrive before the
    /// connection's read timeout elapses.
    /// </exception>
    byte[] Read(int count);

    /// <summary>Writes <paramref name="data"/> and returns the number of bytes written.</summary>
    int Write(ReadOnlySpan<byte> data);

    /// <summary>Discards any bytes currently buffered for reading.</summary>
    void DiscardInBuffer();
}
