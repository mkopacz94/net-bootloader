using System.IO.Ports;

namespace NetBootloader.Core.Communication;

/// <summary>
/// <see cref="IBootloaderConnection"/> implementation backed by a
/// <see cref="SerialPort"/>. Equivalent to using a <c>serial.Serial</c> instance as
/// the connection in mcbootflash.
/// </summary>
public sealed class SerialBootloaderConnection : IBootloaderConnection, IDisposable
{
    private readonly SerialPort _port;

    public SerialBootloaderConnection(string portName, int baudRate, int timeoutMilliseconds = 1000)
    {
        _port = new SerialPort(portName, baudRate)
        {
            DataBits = 8,
            Parity = Parity.None,
            StopBits = StopBits.One,
            ReadTimeout = timeoutMilliseconds,
            WriteTimeout = timeoutMilliseconds,
        };

        try
        {
            _port.Open();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new IOException($"Could not open serial port '{portName}': {ex.Message}", ex);
        }

        _port.DiscardInBuffer();
    }

    /// <summary>Lists the serial port names currently available on this machine.</summary>
    public static string[] GetAvailablePortNames() => SerialPort.GetPortNames();

    public byte[] Read(int count)
    {
        var buffer = new byte[count];
        var offset = 0;

        // SerialPort.Read returns as soon as *some* data is available, not
        // necessarily all of it, so keep pulling until we either have everything or
        // time out - matching pyserial's read(size) semantics.
        while (offset < count)
        {
            int read;

            try
            {
                read = _port.Read(buffer, offset, count - offset);
            }
            catch (TimeoutException ex)
            {
                throw new TimeoutException(
                    $"Timed out waiting for {count} bytes from the bootloader; received {offset}.", ex);
            }

            if (read == 0)
            {
                throw new TimeoutException(
                    $"Connection closed while waiting for {count} bytes from the bootloader; received {offset}.");
            }

            offset += read;
        }

        return buffer;
    }

    public int Write(ReadOnlySpan<byte> data)
    {
        var array = data.ToArray();
        _port.Write(array, 0, array.Length);
        return array.Length;
    }

    public void DiscardInBuffer() => _port.DiscardInBuffer();

    public void Dispose()
    {
        try
        {
            if (_port.IsOpen)
            {
                _port.Close();
            }
        }
        catch (IOException)
        {
            // Port may already have been yanked out (e.g. USB-serial adapter unplugged).
        }

        _port.Dispose();
    }
}
