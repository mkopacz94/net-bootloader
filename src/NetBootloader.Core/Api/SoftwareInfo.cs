namespace NetBootloader.Core.Api;

/// <summary>
/// One entry from the software catalog's <c>GET api/Hex/available-devices</c> endpoint.
/// Property names/order mirror the server's <c>Software</c> record exactly, since
/// <see cref="SoftwareCatalogClient"/> deserializes the JSON response straight into this
/// type with case-insensitive camelCase matching.
/// </summary>
public sealed record SoftwareInfo(string Name, string Version, bool IsBeta, DateTime ReleaseDate);
