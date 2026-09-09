using NetBootloader.Core.Protocol;

namespace NetBootloader.Core.Exceptions;

/// <summary>
/// Base class for exceptions raised while talking to the bootloader. Mirrors
/// <c>mcbootflash.error.BootloaderError</c>.
/// </summary>
public class BootloaderException : Exception
{
    public BootloaderException(string message)
        : base(message)
    {
    }

    public BootloaderException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Creates the specific subclass matching <paramref name="responseCode"/>, or a
    /// plain <see cref="BootloaderException"/> if the code isn't a known error.
    /// </summary>
    public static BootloaderException ForResponseCode(ResponseCode responseCode) => responseCode switch
    {
        ResponseCode.UnsupportedCommand => new UnsupportedCommandException(),
        ResponseCode.BadAddress => new BadAddressException(),
        ResponseCode.BadLength => new BadLengthException(),
        ResponseCode.VerifyFail => new VerifyFailException(),
        _ => new BootloaderException($"Unexpected response code: {responseCode}"),
    };
}

/// <summary>
/// Raised when the bootloader did not recognize the most recently sent command.
/// </summary>
public sealed class UnsupportedCommandException()
    : BootloaderException("The bootloader did not recognize the command.");

/// <summary>
/// Raised when an operation was attempted outside of the program memory range.
/// </summary>
public sealed class BadAddressException()
    : BootloaderException("The operation targeted an address outside the program memory range.");

/// <summary>
/// Raised when the size of the command packet plus associated data was greater than
/// permitted.
/// </summary>
public sealed class BadLengthException()
    : BootloaderException("The command packet plus data exceeded the permitted length.");

/// <summary>
/// Raised when the bootloader cannot detect a bootable application in program memory.
/// </summary>
public sealed class VerifyFailException()
    : BootloaderException("The bootloader did not detect a bootable application.");
