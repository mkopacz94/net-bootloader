namespace NetBootloader.App;

/// <summary>Where to reach the software catalog API (<c>HexController</c>) that backs software download.</summary>
public static class ApiSettings
{
    // Must include a scheme (http:// or https://) - a bare "host:port" string gets
    // parsed as an absolute URI whose "scheme" is the host name, which is what
    // produced "The 'localhost' scheme is not supported." Match this to whatever
    // profile/port your HexController API is actually listening on (check its
    // Properties/launchSettings.json for the exact scheme and port).
    public const string BaseUrl = "http://localhost:5281/";
}
