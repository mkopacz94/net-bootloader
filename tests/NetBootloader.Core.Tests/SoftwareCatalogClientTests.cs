using System.Net;
using System.Net.Http;
using System.Text;
using NetBootloader.Core.Api;
using NetBootloader.Core.Security;
using Xunit;

namespace NetBootloader.Core.Tests;

public class SoftwareCatalogClientTests
{
    private const string SampleHex = ":10000000000102030405060708090A0B0C0D0E0F72\n:00000001FF\n";

    [Fact]
    public async Task GetAvailableSoftwareAsync_ParsesCatalogFromExpectedRoute()
    {
        var handler = new FakeHttpMessageHandler(request =>
            request.RequestUri!.AbsolutePath == "/api/Hex/available-devices"
                ? FakeHttpMessageHandler.JsonResponse(
                    """[{"name":"tm-prog","version":"2.003","isBeta":true,"releaseDate":"2024-01-01T00:00:00Z"}]""")
                : new HttpResponseMessage(HttpStatusCode.NotFound));

        var client = new SoftwareCatalogClient(new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") });

        var software = await client.GetAvailableSoftwareAsync();

        var item = Assert.Single(software);
        Assert.Equal("tm-prog", item.Name);
        Assert.Equal("2.003", item.Version);
        Assert.True(item.IsBeta);
    }

    [Fact]
    public async Task DownloadAndDecryptAsync_DecryptsPackageWithTheKeyTheServerReturns()
    {
        // Simulates the server encrypting with a fresh, request-specific key (unlike a
        // local .tmfw package, which always uses the app's baked-in DefaultKey) and
        // handing both the ciphertext and that key back in the same response.
        var key = new byte[32];
        Random.Shared.NextBytes(key);
        var package = FirmwarePackage.Encrypt(SampleHex, key);

        var handler = new FakeHttpMessageHandler(request =>
            request.RequestUri!.AbsolutePath == "/api/Hex/tm-prog"
                ? FakeHttpMessageHandler.JsonResponse(
                    $$"""{"data":"{{Convert.ToBase64String(package)}}","key":"{{Convert.ToBase64String(key)}}"}""")
                : new HttpResponseMessage(HttpStatusCode.NotFound));

        var client = new SoftwareCatalogClient(new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") });

        var hexContent = await client.DownloadAndDecryptAsync("tm-prog");

        Assert.Equal(SampleHex, hexContent);
    }

    [Fact]
    public async Task DownloadAndDecryptAsync_TamperedPackage_ThrowsInvalidDataException()
    {
        var key = new byte[32];
        Random.Shared.NextBytes(key);
        var package = FirmwarePackage.Encrypt(SampleHex, key);
        package[^1] ^= 0xFF;

        var handler = new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.JsonResponse(
            $$"""{"data":"{{Convert.ToBase64String(package)}}","key":"{{Convert.ToBase64String(key)}}"}"""));

        var client = new SoftwareCatalogClient(new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") });

        await Assert.ThrowsAsync<InvalidDataException>(() => client.DownloadAndDecryptAsync("tm-prog"));
    }

    [Fact]
    public async Task DownloadAndDecryptAsync_MalformedBase64_ThrowsInvalidDataException()
    {
        var handler = new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.JsonResponse(
            """{"data":"not-base64!!","key":"not-base64!!"}"""));

        var client = new SoftwareCatalogClient(new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") });

        await Assert.ThrowsAsync<InvalidDataException>(() => client.DownloadAndDecryptAsync("tm-prog"));
    }

    /// <summary>Minimal scripted <see cref="HttpMessageHandler"/> - no mocking library used elsewhere in this suite.</summary>
    private sealed class FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }
}
