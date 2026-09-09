using NetBootloader.Core.Security;
using Xunit;

namespace NetBootloader.Core.Tests;

public class FirmwarePackageTests
{
    private const string SampleHex = ":10000000000102030405060708090A0B0C0D0E0F72\n:00000001FF\n";

    [Fact]
    public void Encrypt_ThenDecrypt_RoundTripsExactly()
    {
        var package = FirmwarePackage.Encrypt(SampleHex);

        var decrypted = FirmwarePackage.Decrypt(package);

        Assert.Equal(SampleHex, decrypted);
    }

    [Fact]
    public void Encrypt_DoesNotEmbedPlaintextHexBytesInThePackage()
    {
        var package = FirmwarePackage.Encrypt(SampleHex);
        var packageAsLatin1 = System.Text.Encoding.Latin1.GetString(package);

        // The raw HEX content shouldn't appear anywhere in the encrypted bytes -
        // that's the entire point of encrypting it before distribution.
        Assert.DoesNotContain(":10000000000102030405060708090A0B0C0D0E0F72", packageAsLatin1);
    }

    [Fact]
    public void Encrypt_ProducesDifferentBytesEachTime()
    {
        // Nonce is random per call, so encrypting the same content twice must not
        // produce identical packages (otherwise the nonce isn't doing its job).
        var first = FirmwarePackage.Encrypt(SampleHex);
        var second = FirmwarePackage.Encrypt(SampleHex);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Decrypt_TamperedPackage_Throws()
    {
        var package = FirmwarePackage.Encrypt(SampleHex);
        package[^1] ^= 0xFF; // flip a bit in the ciphertext

        Assert.Throws<InvalidDataException>(() => FirmwarePackage.Decrypt(package));
    }

    [Fact]
    public void Decrypt_WrongKey_Throws()
    {
        var package = FirmwarePackage.Encrypt(SampleHex, TestKey(1));

        Assert.Throws<InvalidDataException>(() => FirmwarePackage.Decrypt(package, TestKey(2)));
    }

    [Fact]
    public void Decrypt_MatchingExplicitKey_RoundTrips()
    {
        var key = TestKey(7);
        var package = FirmwarePackage.Encrypt(SampleHex, key);

        var decrypted = FirmwarePackage.Decrypt(package, key);

        Assert.Equal(SampleHex, decrypted);
    }

    [Theory]
    [InlineData(new byte[0])]
    [InlineData(new byte[] { 0x4E, 0x42 })] // truncated magic
    public void Decrypt_TooShort_Throws(byte[] package)
    {
        Assert.Throws<InvalidDataException>(() => FirmwarePackage.Decrypt(package));
    }

    [Fact]
    public void Decrypt_BadMagic_Throws()
    {
        var package = FirmwarePackage.Encrypt(SampleHex);
        package[0] = (byte)'X';

        Assert.Throws<InvalidDataException>(() => FirmwarePackage.Decrypt(package));
    }

    private static byte[] TestKey(byte seed)
    {
        var key = new byte[32];
        Array.Fill(key, seed);
        return key;
    }
}
