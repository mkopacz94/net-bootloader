using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetBootloader.App.Views;
using NetBootloader.Core;
using NetBootloader.Core.Communication;
using NetBootloader.Core.Exceptions;
using NetBootloader.Core.Security;

namespace NetBootloader.App.ViewModels;

/// <summary>
/// Composition root for the main window: owns the sub-viewmodels for each concern
/// (<see cref="Connection"/>, <see cref="Firmware"/>, <see cref="Log"/>) and drives the
/// flash operation, since that's the one thing that needs data from all three.
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    private CancellationTokenSource? _cancellationSource;

    public MainViewModel()
    {
        Connection = new ConnectionViewModel();
        Firmware = new FirmwareViewModel();
        Log = new FlashLogViewModel();

        // CanFlash depends on properties of the child viewmodels, which the source
        // generator can't wire up automatically (NotifyCanExecuteChangedFor only
        // covers properties on this class) - so re-check on any change to either.
        Connection.PropertyChanged += (_, _) => FlashCommand.NotifyCanExecuteChanged();
        Firmware.PropertyChanged += (_, _) => FlashCommand.NotifyCanExecuteChanged();
    }

    public ConnectionViewModel Connection { get; }

    public FirmwareViewModel Firmware { get; }

    public FlashLogViewModel Log { get; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(FlashCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
    private bool _isBusy;

    private static readonly string[] SupportedFirmwareExtensions = { ".hex", ".tmfw" };

    private bool CanFlash() =>
        !IsBusy
        && !string.IsNullOrWhiteSpace(Connection.SelectedPort)
        && !string.IsNullOrWhiteSpace(Firmware.HexFilePath)
        && File.Exists(Firmware.HexFilePath)
        && SupportedFirmwareExtensions.Contains(Path.GetExtension(Firmware.HexFilePath), StringComparer.OrdinalIgnoreCase);

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
            var hexContent = await LoadHexContentAsync(Firmware.HexFilePath!, _cancellationSource.Token);

            Log.StatusText = $"Connecting to {Connection.SelectedPort}...";
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

            Log.StatusText = "Flashing complete. Self-verify OK.";
            Log.ProgressPercent = 100;
        }
        catch (OperationCanceledException)
        {
            Log.StatusText = "Flashing cancelled.";
        }
        // Covers every way the selected file can turn out not to be flashable
        // firmware: a corrupted/wrong-key .tmfw package (InvalidDataException), HEX
        // content that isn't validly formatted (FormatException - e.g. someone picked
        // an unrelated file, or the extension lied), or HEX that parses fine but has
        // no data in this device's flash range (InvalidOperationException).
        catch (Exception ex) when (ex is InvalidDataException or FormatException or InvalidOperationException)
        {
            var fileName = string.IsNullOrEmpty(Firmware.HexFilePath)
                ? "The selected file"
                : $"\"{Path.GetFileName(Firmware.HexFilePath)}\"";
            Log.StatusText = "Error: the selected file isn't valid firmware.";
            MessageDialog.ShowError("Invalid firmware file", $"{fileName} doesn't look like valid firmware.\n\n{ex.Message}");
        }
        catch (VerifyFailException)
        {
            Log.StatusText = "Error: flashing completed, but the bootloader reports no bootable application.";
        }
        catch (BootloaderException ex)
        {
            Log.StatusText = $"Error: {ex.Message}";
        }
        catch (Exception ex) when (ex is IOException or TimeoutException)
        {
            Log.StatusText = $"Connection error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            _cancellationSource?.Dispose();
            _cancellationSource = null;
        }
    }

    /// <summary>
    /// Loads a firmware file's HEX content by extension: <c>.tmfw</c> is read as bytes
    /// and decrypted in memory (the plaintext HEX never touches disk); <c>.hex</c> is
    /// read as-is. Anything else is rejected outright, rather than silently falling
    /// through to being treated as plain HEX and only failing later once it's parsed.
    /// </summary>
    /// <exception cref="InvalidDataException">If the extension isn't <c>.hex</c> or <c>.tmfw</c>.</exception>
    private async Task<string> LoadHexContentAsync(string filePath, CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(filePath);

        if (string.Equals(extension, ".tmfw", StringComparison.OrdinalIgnoreCase))
        {
            Log.AppendLog("Decrypting firmware package in memory...");
            var package = await File.ReadAllBytesAsync(filePath, cancellationToken);
            return FirmwarePackage.Decrypt(package);
        }

        if (string.Equals(extension, ".hex", StringComparison.OrdinalIgnoreCase))
        {
            return await File.ReadAllTextAsync(filePath, cancellationToken);
        }

        throw new InvalidDataException(
            $"Unsupported firmware file type \"{extension}\" - expected .hex or .tmfw.");
    }

    [RelayCommand(CanExecute = nameof(IsBusy))]
    private void Cancel() => _cancellationSource?.Cancel();
}
