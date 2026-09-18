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

    public SoftwareViewModel(ISoftwareCatalogClient catalogClient)
    {
        _catalogClient = catalogClient;

        // Best-effort: a server that's unreachable at startup just leaves the catalog
        // empty rather than blocking the window from opening - LoadAvailableSoftwareAsync
        // already reports the failure via the log/dialog.
        _ = LoadAvailableSoftwareCommand.ExecuteAsync(null);
    }

    public ObservableCollection<SoftwareInfo> AvailableSoftware { get; } = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DownloadSoftwareCommand))]
    private SoftwareInfo? _selectedSoftware;

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
    /// Plaintext HEX decrypted from the last successful download, kept in memory only -
    /// never written to disk, the same guarantee a local .tmfw package gets right before
    /// flashing. Null until a download succeeds; cleared whenever the selection changes, so
    /// a stale package can't get flashed under a different selection's name.
    /// </summary>
    public string? DownloadedHexContent { get; private set; }

    partial void OnSelectedSoftwareChanged(SoftwareInfo? value)
    {
        DownloadedHexContent = null;
        DownloadedLabel = null;
    }

    private bool CanLoadAvailableSoftware() => !IsLoadingCatalog;

    [RelayCommand(CanExecute = nameof(CanLoadAvailableSoftware))]
    private async Task LoadAvailableSoftwareAsync()
    {
        IsLoadingCatalog = true;

        try
        {
            var software = await _catalogClient.GetAvailableSoftwareAsync();
            var previouslySelected = SelectedSoftware?.Name;

            AvailableSoftware.Clear();
            foreach (var item in software)
            {
                AvailableSoftware.Add(item);
            }

            SelectedSoftware = AvailableSoftware.FirstOrDefault(item => item.Name == previouslySelected)
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
        var software = SelectedSoftware!;
        IsDownloading = true;

        try
        {
            DownloadedHexContent = await _catalogClient.DownloadAndDecryptAsync(software.Name);
            DownloadedLabel = Strings.Instance.StatusDownloadComplete(software.Name);
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidDataException)
        {
            DownloadedHexContent = null;
            DownloadedLabel = null;
            MessageDialog.ShowError(
                Strings.Instance.ErrorDialogTitle,
                Strings.Instance.StatusDownloadFailedMessage(software.Name, ex.Message));
        }
        finally
        {
            IsDownloading = false;
        }
    }
}
