using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using NetBootloader.Core.Security;

namespace NetBootloader.Core.Api;

/// <summary>
/// Wire shape of <c>GET api/Hex/{device}</c>'s response: a base64-encoded encrypted
/// package plus the base64-encoded key it was encrypted with. Unlike a locally loaded
/// <c>.tmfw</c> file (which uses the app's baked-in shared key, see
/// <see cref="FirmwarePackage"/>'s doc comment), the server issues a fresh random key
/// per download, so there's no long-lived secret embedded in the app to extract.
/// </summary>
internal sealed record SoftwarePackageResponse(string Data, string Key);

/// <summary><see cref="ISoftwareCatalogClient"/> backed by the <c>HexController</c> HTTP API.</summary>
public sealed class SoftwareCatalogClient : ISoftwareCatalogClient
{
    private const string BasePath = "api/Hex";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;

    public SoftwareCatalogClient(HttpClient httpClient) => _httpClient = httpClient;

    public async Task<IReadOnlyList<SoftwareInfo>> GetAvailableSoftwareAsync(CancellationToken cancellationToken = default)
    {
        var software = await _httpClient.GetFromJsonAsync<List<SoftwareInfo>>(
            $"{BasePath}/available-devices", JsonOptions, cancellationToken);

        return software ?? [];
    }

    public async Task<string> DownloadAndDecryptAsync(string softwareName, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetFromJsonAsync<SoftwarePackageResponse>(
            $"{BasePath}/{Uri.EscapeDataString(softwareName)}", JsonOptions, cancellationToken)
            ?? throw new InvalidDataException("Server returned an empty software package.");

        byte[] data;
        byte[] key;

        try
        {
            data = Convert.FromBase64String(response.Data);
            key = Convert.FromBase64String(response.Key);
        }
        catch (FormatException ex)
        {
            throw new InvalidDataException("Server returned a malformed software package.", ex);
        }

        return FirmwarePackage.Decrypt(data, key);
    }
}
