using NetBootloader.Core.Security;

namespace NetBootloader.HexPackager;

/// <summary>
/// Encrypts a firmware .hex file into a package NetBootloader.App can decrypt in
/// memory right before flashing, so the plaintext HEX is never what you hand to a
/// client - only the encrypted package. See <see cref="FirmwarePackage"/> for the
/// format and the (client-side, so inherently limited) threat model this protects
/// against.
/// </summary>
public static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length != 3)
        {
            PrintUsage();
            return 1;
        }

        var command = args[0];
        var inputPath = args[1];
        var outputPath = args[2];

        try
        {
            switch (command.ToLowerInvariant())
            {
                case "encode":
                    Encode(inputPath, outputPath);
                    return 0;
                case "decode":
                    Decode(inputPath, outputPath);
                    return 0;
                default:
                    Console.Error.WriteLine($"Unknown command '{command}'.");
                    PrintUsage();
                    return 1;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    private static void Encode(string hexPath, string packagePath)
    {
        var hexContent = File.ReadAllText(hexPath);
        var package = FirmwarePackage.Encrypt(hexContent);
        File.WriteAllBytes(packagePath, package);
        Console.WriteLine($"Encoded {hexPath} ({hexContent.Length} chars) -> {packagePath} ({package.Length} bytes).");
    }

    private static void Decode(string packagePath, string hexPath)
    {
        var package = File.ReadAllBytes(packagePath);
        var hexContent = FirmwarePackage.Decrypt(package);
        File.WriteAllText(hexPath, hexContent);
        Console.WriteLine($"Decoded {packagePath} -> {hexPath} ({hexContent.Length} chars).");
        Console.WriteLine("Reminder: this command is for verifying a package locally - don't hand the decoded .hex to a client.");
    }

    private static void PrintUsage()
    {
        Console.WriteLine("NetBootloader.HexPackager - encrypts firmware .hex files for distribution.");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  NetBootloader.HexPackager encode <input.hex> <output.tmfw>");
        Console.WriteLine("  NetBootloader.HexPackager decode <input.tmfw> <output.hex>   (local verification only)");
    }
}
