using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetBootloader.App.Localization;
using NetBootloader.App.Views;
using NetBootloader.Core;
using NetBootloader.Core.Api;
using NetBootloader.Core.Communication;
using NetBootloader.Core.Exceptions;
using NetBootloader.Core.Security;

namespace NetBootloader.App.ViewModels;

/// <summary>
/// Composition root for the main window: owns the sub-viewmodels for each concern
/// (<see cref="Connection"/>, <see cref="Firmware"/>, <see cref="Log"/>) and drives the
/// flash operation, since that's the one thing that needs data from all three. Also owns
/// downloading firmware from the software catalog API, as an alternative to
/// <see cref="Firmware"/>'s local <c>.tmfw</c> file picker.
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    private CancellationTokenSource? _cancellationSource;

    public MainViewModel() : this(new SoftwareCatalogClient(new HttpClient { BaseAddress = new Uri(ApiSettings.BaseUrl) }))
    {
    }

    internal MainViewModel(ISoftwareCatalogClient softwareCatalogClient)
    {
        Connection = new ConnectionViewModel();
        Firmware = new FirmwareViewModel();
        Log = new FlashLogViewModel();
        Software = new SoftwareViewModel(softwareCatalogClient);

        // CanFlash depends on properties of the child viewmodels, which the source
        // generator can't wire up automatically (NotifyCanExecuteChangedFor only
        // covers properties on this class) - so re-check on any change to any of them.
        Connection.PropertyChanged += (_, _) => FlashCommand.NotifyCanExecuteChanged();
        Firmware.PropertyChanged += (_, _) => FlashCommand.NotifyCanExecuteChanged();
        Software.PropertyChanged += (_, _) => FlashCommand.NotifyCanExecuteChanged();
    }

    public ConnectionViewModel Connection { get; }

    public FirmwareViewModel Firmware { get; }

    public FlashLogViewModel Log { get; }

    public SoftwareViewModel Software { get; }

    public ObservableCollection<LanguageOption> AvailableLanguages { get; } = new()
    {
        new LanguageOption(AppLanguage.English, "English", "🇬🇧"),
        new LanguageOption(AppLanguage.Polish, "Polski", "🇵🇱"),
        new LanguageOption(AppLanguage.French, "Français", "🇫🇷"),
        new LanguageOption(AppLanguage.German, "Deutsch", "🇩🇪"),
    };

    /// <summary>
    /// Thin wrapper around <see cref="Strings.Language"/> so the language picker's
    /// SelectedItem binding has something to read/write - the actual UI text lives on
    /// the Strings singleton itself and updates live via its own PropertyChanged.
    /// </summary>
    public LanguageOption SelectedLanguage
    {
        get => AvailableLanguages.First(language => language.Value == Strings.Instance.Language);
        set
        {
            if (value.Value == Strings.Instance.Language)
            {
                return;
            }

            Strings.Instance.Language = value.Value;
            OnPropertyChanged();
        }
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(FlashCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
    private bool _isBusy;

    partial void OnIsBusyChanged(bool value) => Software.IsFlashing = value;

    // ----- Flashing -----

    private bool CanFlash() =>
        !IsBusy
        && !Software.IsDownloading
        && !string.IsNullOrWhiteSpace(Connection.SelectedPort)
        && HasFirmwareToFlash();

    private bool HasFirmwareToFlash() =>
        Software.DownloadedHexContent is not null
        || (!string.IsNullOrWhiteSpace(Firmware.HexFilePath)
            && File.Exists(Firmware.HexFilePath)
            && string.Equals(Path.GetExtension(Firmware.HexFilePath), ".tmfw", StringComparison.OrdinalIgnoreCase));

    [RelayCommand(CanExecute = nameof(CanFlash))]
    private async Task FlashAsync()
    {
        Log.Reset();
        IsBusy = true;
        _cancellationSource = new CancellationTokenSource();

        try
        {
            // Load (and, for a package, decrypt) the firmware before touching the
            // serial port at all - no point opening a connection to hardware if the
            // file turns out not to be usable.
            var hexContent = await LoadHexContentAsync(_cancellationSource.Token);

            Log.SetStatus(strings => strings.StatusConnectingTo(Connection.SelectedPort!));
            using var connection = new SerialBootloaderConnection(
                Connection.SelectedPort!, Connection.SelectedBaudRate, Connection.TimeoutSeconds * 1000);

            // DebugLog fires on the background thread FlashAsync runs on; route it
            // through Progress<T> so log lines land back on the UI thread like the
            // progress reports do, instead of touching UI-bound state directly.
            var client = new BootloaderClient(connection);
            // Typed as the interface: Progress<T> implements IProgress<T>.Report
            // explicitly, so it isn't callable through a Progress<T>-typed reference.
            IProgress<string> logProgress = new Progress<string>(Log.AppendLog);
            client.DebugLog += line => logProgress.Report(line);
            var flasher = new FirmwareFlasher(client);

            var progress = new Progress<FlashProgressReport>(Log.ReportProgress);

            using var reader = new StringReader(hexContent);
            await flasher.FlashAsync(
                reader,
                Firmware.VerifyChecksum,
                Firmware.ResetAfterFlash,
                progress,
                _cancellationSource.Token);

            Log.SetStatus(strings => strings.StatusFlashComplete);
            Log.ProgressPercent = 100;

            MessageDialog.ShowSuccess(
                Strings.Instance.MessageFlashingSuccessTitle,
                Strings.Instance.MessageFlashingSuccessMessage);
        }
        catch (OperationCanceledException)
        {
            Log.SetStatus(strings => strings.StatusFlashCancelled);
        }
        // Covers every way the selected file can turn out not to be flashable
        // firmware: a corrupted/wrong-key .tmfw package (InvalidDataException), HEX
        // content that isn't validly formatted (FormatException - e.g. someone picked
        // an unrelated file, or the extension lied), or HEX that parses fine but has
        // no data in this device's flash range (InvalidOperationException).
        catch (Exception ex) when (ex is InvalidDataException or FormatException or InvalidOperationException)
        {
            var fileName = Software.DownloadedHexContent is not null
                ? Software.DownloadedLabel ?? Strings.Instance.SelectedFileFallback
                : string.IsNullOrEmpty(Firmware.HexFilePath)
                    ? Strings.Instance.SelectedFileFallback
                    : $"\"{Path.GetFileName(Firmware.HexFilePath)}\"";
            Log.SetStatus(strings => strings.StatusInvalidFirmwareFile);
            MessageDialog.ShowError(
                Strings.Instance.InvalidFirmwareDialogTitle,
                Strings.Instance.InvalidFirmwareDialogMessage(fileName, ex.Message));
        }
        catch (VerifyFailException)
        {
            Log.SetStatus(strings => strings.StatusVerifyFailed);
            MessageDialog.ShowError(Strings.Instance.ErrorDialogTitle, Strings.Instance.StatusVerifyFailed);
        }
        catch (BootloaderException ex)
        {
            Log.SetStatus(strings => strings.StatusError(ex.Message));
            MessageDialog.ShowError(Strings.Instance.ErrorDialogTitle, Strings.Instance.StatusError(ex.Message));
        }
        catch (Exception ex) when (ex is IOException or TimeoutException)
        {
            Log.SetStatus(strings => strings.StatusConnectionError(ex.Message));
            MessageDialog.ShowError(Strings.Instance.ErrorDialogTitle, Strings.Instance.StatusConnectionError(ex.Message));
        }
        finally
        {
            IsBusy = false;
            _cancellationSource?.Dispose();
            _cancellationSource = null;
        }
    }

    /// <summary>
    /// Gets the HEX content to flash: a downloaded package already sitting decrypted in
    /// memory takes precedence; otherwise falls back to <see cref="Firmware"/>'s local
    /// <c>.tmfw</c> file - only that extension is accepted, read as bytes and decrypted
    /// in memory, so the plaintext HEX never touches disk either way. Anything else is
    /// rejected outright, rather than silently falling through to being treated as plain
    /// HEX and only failing later once it's parsed.
    /// </summary>
    /// <exception cref="InvalidDataException">If the local file's extension isn't <c>.tmfw</c>.</exception>
    private async Task<string> LoadHexContentAsync(CancellationToken cancellationToken)
    {
        if (Software.DownloadedHexContent is not null)
        {
            return Software.DownloadedHexContent;
        }

        var filePath = Firmware.HexFilePath!;
        var extension = Path.GetExtension(filePath);

        if (!string.Equals(extension, ".tmfw", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(Strings.Instance.UnsupportedFirmwareFileType(extension));
        }

        Log.AppendLog(Strings.Instance.StatusDecryptingPackage);
        var package = await File.ReadAllBytesAsync(filePath, cancellationToken);
        return FirmwarePackage.Decrypt(package);
    }

    [RelayCommand(CanExecute = nameof(IsBusy))]
    private void Cancel() => _cancellationSource?.Cancel();
}
