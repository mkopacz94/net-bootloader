using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetBootloader.Core;
using NetBootloader.Core.Communication;
using NetBootloader.Core.Exceptions;

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

    private bool CanFlash() =>
        !IsBusy
        && !string.IsNullOrWhiteSpace(Connection.SelectedPort)
        && !string.IsNullOrWhiteSpace(Firmware.HexFilePath)
        && File.Exists(Firmware.HexFilePath);

    [RelayCommand(CanExecute = nameof(CanFlash))]
    private async Task FlashAsync()
    {
        Log.Reset();
        IsBusy = true;
        _cancellationSource = new CancellationTokenSource();

        try
        {
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
            await flasher.FlashAsync(
                Firmware.HexFilePath!,
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

    [RelayCommand(CanExecute = nameof(IsBusy))]
    private void Cancel() => _cancellationSource?.Cancel();
}
