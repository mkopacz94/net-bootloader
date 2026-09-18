namespace NetBootloader.Core.Api;

/// <summary>Lists and downloads firmware packages from the software catalog API.</summary>
public interface ISoftwareCatalogClient
{
    /// <summary>Fetches the catalog of software available to download.</summary>
    Task<IReadOnlyList<SoftwareInfo>> GetAvailableSoftwareAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads <paramref name="softwareName"/>'s package and decrypts it in memory using
    /// the per-download key the server returns alongside it - the plaintext HEX content
    /// never touches disk, the same guarantee a locally loaded <c>.tmfw</c> package gets.
    /// </summary>
    /// <exception cref="InvalidDataException">
    /// If the server's response is malformed, or the package fails its integrity check.
    /// </exception>
    Task<string> DownloadAndDecryptAsync(string softwareName, CancellationToken cancellationToken = default);
}
