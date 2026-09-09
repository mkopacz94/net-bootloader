using System.Security.Cryptography;
using System.Text;

namespace NetBootloader.Core.Security;

/// <summary>
/// Encrypts/decrypts firmware HEX content into a small package format, so a raw Intel
/// HEX file never has to be distributed - or land on disk - in plaintext.
///
/// Uses AES-256-GCM (authenticated: tampering or corruption is detected, not just
/// silently mis-decoded) with a key shared between <c>NetBootloader.HexPackager</c>
/// (which creates packages) and <c>NetBootloader.App</c> (which decrypts one in memory
/// right before flashing, without ever writing the decrypted HEX to disk).
///
/// Threat model: this key is embedded in NetBootloader.App, so anyone who decompiles
/// it can recover the key and decrypt any package - the same limitation every
/// client-side content-protection scheme has. It protects against casual exposure (a
/// client opening the file in a text/hex editor, an accidental share, a competitor who
/// isn't going to reverse-engineer a .NET binary for it), not a determined attacker.
/// If you need protection against that too, swap <see cref="Key"/> for a passphrase
/// supplied at runtime (e.g. typed into the app, or delivered out of band) instead of
/// one baked into the assembly - <see cref="Encrypt(string, byte[])"/> and
/// <see cref="Decrypt(byte[], byte[])"/> overloads below accept an explicit key for
/// exactly that.
/// </summary>
public static class FirmwarePackage
{
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private static readonly byte[] Magic = "TMFW"u8.ToArray();
    private const byte FormatVersion = 1;

    // Generated once with a CSPRNG; not derived from anything guessable. Rotating this
    // invalidates every previously-issued package - bump FormatVersion too if you do.
    private static readonly byte[] DefaultKey =
        Convert.FromBase64String("7JtQQlV3zapLJJyMb1QUClcWGM3e8vZlPC5aI9gpScY=");

    /// <summary>Encrypts <paramref name="hexContent"/> using the built-in shared key.</summary>
    public static byte[] Encrypt(string hexContent) => Encrypt(hexContent, DefaultKey);

    /// <summary>Encrypts <paramref name="hexContent"/> using an explicit 256-bit key.</summary>
    public static byte[] Encrypt(string hexContent, byte[] key)
    {
        var plaintext = Encoding.UTF8.GetBytes(hexContent);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagSize];

        using (var aesGcm = new AesGcm(key, TagSize))
        {
            aesGcm.Encrypt(nonce, plaintext, ciphertext, tag);
        }

        using var package = new MemoryStream(Magic.Length + 1 + NonceSize + TagSize + ciphertext.Length);
        package.Write(Magic);
        package.WriteByte(FormatVersion);
        package.Write(nonce);
        package.Write(tag);
        package.Write(ciphertext);
        return package.ToArray();
    }

    /// <summary>Decrypts a package produced by <see cref="Encrypt(string)"/>, using the built-in shared key.</summary>
    public static string Decrypt(byte[] package) => Decrypt(package, DefaultKey);

    /// <summary>Decrypts a package produced by <see cref="Encrypt(string, byte[])"/>, using an explicit key.</summary>
    /// <exception cref="InvalidDataException">
    /// If the package is too short, doesn't start with the expected magic/version, or
    /// fails authentication (wrong key, or the package was corrupted/tampered with).
    /// </exception>
    public static string Decrypt(byte[] package, byte[] key)
    {
        var headerSize = Magic.Length + 1 + NonceSize + TagSize;

        if (package.Length < headerSize)
        {
            throw new InvalidDataException("Firmware package is too short to be valid.");
        }

        var offset = 0;

        if (!package.AsSpan(offset, Magic.Length).SequenceEqual(Magic))
        {
            throw new InvalidDataException("Not a recognized firmware package.");
        }

        offset += Magic.Length;
        var version = package[offset];
        offset += 1;

        if (version != FormatVersion)
        {
            throw new InvalidDataException($"Unsupported firmware package version {version}.");
        }

        var nonce = package.AsSpan(offset, NonceSize);
        offset += NonceSize;
        var tag = package.AsSpan(offset, TagSize);
        offset += TagSize;
        var ciphertext = package.AsSpan(offset);
        var plaintext = new byte[ciphertext.Length];

        try
        {
            using var aesGcm = new AesGcm(key, TagSize);
            aesGcm.Decrypt(nonce, ciphertext, tag, plaintext);
        }
        catch (CryptographicException ex)
        {
            throw new InvalidDataException(
                "Firmware package failed its integrity check - it may be corrupted, or encrypted with a different key.",
                ex);
        }

        return Encoding.UTF8.GetString(plaintext);
    }
}
