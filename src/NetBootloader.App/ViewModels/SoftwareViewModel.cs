using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetBootloader.App.Localization;
using NetBootloader.App.Views;
using NetBootloader.Core.Api;

namespace NetBootloader.App.ViewModels;

/// <summary>
/// Available software catalog: lists what <see cref="ISoftwareCatalogClient"/> offers and
/// downloads/decrypts the selected entry. <see cref="MainViewModel"/> reads
/// <see cref="DownloadedHexContent"/> as an alternative flash source to a locally browsed
/// <c>.tmfw</c> file.
/// </summary>
public sealed partial class SoftwareViewModel : ObservableObject
{
    private readonly ISoftwareCatalogClient _catalogClient;

    // Keyed by SoftwareInfo.Name. Downloads survive moving the selection away and back -
    // and a catalog refresh - so the user doesn't have to re-download something they
    // already fetched just because they looked at something else in between.
    private readonly Dictionary<string, string> _downloadedHexByName = new();

    public SoftwareViewModel(ISoftwareCatalogClient catalogClient)
    {
        _catalogClient = catalogClient;

        // Best-effort: a server that's unreachable at startup just leaves the catalog
        // empty rather than blocking the window from opening - LoadAvailableSoftwareAsync
        // already reports the failure via the log/dialog.
        _ = LoadAvailableSoftwareCommand.ExecuteAsync(null);
    }

    public ObservableCollection<SoftwareCatalogEntry> AvailableSoftware { get; } = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DownloadSoftwareCommand))]
    private SoftwareCatalogEntry? _selectedSoftware;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoadAvailableSoftwareCommand))]
    private bool _isLoadingCatalog;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DownloadSoftwareCommand))]
    private bool _isDownloading;

    /// <summary>Name/version of the software currently held decrypted in memory, or null if none.</summary>
    [ObservableProperty]
    private string? _downloadedLabel;

    /// <summary>
    /// Set by <see cref="MainViewModel"/> while a flash is in progress, so a download can't
    /// start mid-flash and overwrite <see cref="DownloadedHexContent"/> underneath it.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DownloadSoftwareCommand))]
    private bool _isFlashing;

    /// <summary>
    /// Plaintext HEX for the currently selected entry, decrypted in memory only - never
    /// written to disk, the same guarantee a local .tmfw package gets right before
    /// flashing. Null unless the selected entry has been downloaded (this session, via
    /// <see cref="_downloadedHexByName"/>).
    /// </summary>
    public string? DownloadedHexContent { get; private set; }

    partial void OnSelectedSoftwareChanged(SoftwareCatalogEntry? value)
    {
        if (value is not null && _downloadedHexByName.TryGetValue(value.Name, out var cachedHexContent))
        {
            DownloadedHexContent = cachedHexContent;
            DownloadedLabel = Strings.Instance.StatusDownloadComplete(value.Name);
        }
        else
        {
            DownloadedHexContent = null;
            DownloadedLabel = null;
        }
    }

    private bool CanLoadAvailableSoftware() => !IsLoadingCatalog;

    [RelayCommand(CanExecute = nameof(CanLoadAvailableSoftware))]
    private async Task LoadAvailableSoftwareAsync()
    {
        IsLoadingCatalog = true;

        try
        {
            var software = await _catalogClient.GetAvailableSoftwareAsync();
            var previouslySelectedName = SelectedSoftware?.Name;

            AvailableSoftware.Clear();
            foreach (var item in software)
            {
                AvailableSoftware.Add(new SoftwareCatalogEntry(item)
                {
                    IsDownloaded = _downloadedHexByName.ContainsKey(item.Name),
                });
            }

            SelectedSoftware = AvailableSoftware.FirstOrDefault(entry => entry.Name == previouslySelectedName)
                ?? AvailableSoftware.FirstOrDefault();
        }
        catch (HttpRequestException ex)
        {
            MessageDialog.ShowError(Strings.Instance.ErrorDialogTitle, Strings.Instance.StatusConnectionError(ex.Message));
        }
        finally
        {
            IsLoadingCatalog = false;
        }
    }

    // !IsFlashing too: downloading while a flash is in progress could overwrite
    // DownloadedHexContent underneath the in-flight FlashAsync call.
    private bool CanDownloadSoftware() => !IsFlashing && !IsDownloading && SelectedSoftware is not null;

    [RelayCommand(CanExecute = nameof(CanDownloadSoftware))]
    private async Task DownloadSoftwareAsync()
    {
        var entry = SelectedSoftware!;
        IsDownloading = true;

        try
        {
            var hexContent = await _catalogClient.DownloadAndDecryptAsync(entry.Name);
            _downloadedHexByName[entry.Name] = hexContent;
            entry.IsDownloaded = true;
            DownloadedHexContent = hexContent;
            DownloadedLabel = Strings.Instance.StatusDownloadComplete(entry.Name);
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidDataException)
        {
            // Deliberately leaves _downloadedHexByName/DownloadedHexContent/entry.IsDownloaded
            // untouched: a failed re-download attempt shouldn't invalidate firmware that's
            // already sitting decrypted in memory from an earlier successful one.
            MessageDialog.ShowError(
                Strings.Instance.ErrorDialogTitle,
                Strings.Instance.StatusDownloadFailedMessage(entry.Name, ex.Message));
        }
        finally
        {
            IsDownloading = false;
        }
    }
}
