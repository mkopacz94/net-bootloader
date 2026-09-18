using CommunityToolkit.Mvvm.ComponentModel;
using NetBootloader.Core.Api;

namespace NetBootloader.App.ViewModels;

/// <summary>
/// One row of <see cref="SoftwareViewModel.AvailableSoftware"/>: the wire DTO from
/// <see cref="ISoftwareCatalogClient"/> plus per-item UI state - currently just whether
/// it's been downloaded and decrypted this session, shown as a checkmark in SoftwareView's
/// list so a previously downloaded entry stays visibly ready to flash even after the
/// selection moves elsewhere and back.
/// </summary>
public sealed partial class SoftwareCatalogEntry : ObservableObject
{
    public SoftwareCatalogEntry(SoftwareInfo info) => Info = info;

    public SoftwareInfo Info { get; }

    public string Name => Info.Name;

    public string Version => Info.Version;

    public bool IsBeta => Info.IsBeta;

    public DateTime ReleaseDate => Info.ReleaseDate;

    [ObservableProperty]
    private bool _isDownloaded;
}
