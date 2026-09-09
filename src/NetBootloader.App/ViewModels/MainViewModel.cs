using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Input;
using Microsoft.Win32;
using NetBootloader.Core;
using NetBootloader.Core.Communication;
using NetBootloader.Core.Exceptions;

namespace NetBootloader.App.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private static readonly int[] CommonBaudRates = { 9600, 19200, 38400, 57600, 115200, 230400, 460800 };

    private CancellationTokenSource? _cancellationSource;

    public MainViewModel()
    {
        AvailablePorts = new ObservableCollection<string>();
        BaudRates = new ObservableCollection<int>(CommonBaudRates);
        SelectedBaudRate = 460800;
        TimeoutSeconds = 1.0;
        RefreshPorts();

        BrowseHexFileCommand = new RelayCommand(BrowseHexFile);
        RefreshPortsCommand = new RelayCommand(RefreshPorts);
        FlashCommand = new AsyncRelayCommand(FlashAsync, CanFlash);
        CancelCommand = new RelayCommand(Cancel, () => IsBusy);
    }

    public ObservableCollection<string> AvailablePorts { get; }

    public ObservableCollection<int> BaudRates { get; }

    private string? _selectedPort;
    public string? SelectedPort
    {
        get => _selectedPort;
        set => SetProperty(ref _selectedPort, value);
    }

    private int _selectedBaudRate;
    public int SelectedBaudRate
    {
        get => _selectedBaudRate;
        set => SetProperty(ref _selectedBaudRate, value);
    }

    private double _timeoutSeconds;
    public double TimeoutSeconds
    {
        get => _timeoutSeconds;
        set => SetProperty(ref _timeoutSeconds, value);
    }

    private string? _hexFilePath;
    public string? HexFilePath
    {
        get => _hexFilePath;
        set => SetProperty(ref _hexFilePath, value);
    }

    private bool _verifyChecksum = true;
    public bool VerifyChecksum
    {
        get => _verifyChecksum;
        set => SetProperty(ref _verifyChecksum, value);
    }

    private bool _resetAfterFlash = true;
    public bool ResetAfterFlash
    {
        get => _resetAfterFlash;
        set => SetProperty(ref _resetAfterFlash, value);
    }

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    private double _progressPercent;
    public double ProgressPercent
    {
        get => _progressPercent;
        private set => SetProperty(ref _progressPercent, value);
    }

    private string _statusText = "Ready.";
    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    private readonly StringBuilder _log = new();
    private string _logText = "";
    public string LogText
    {
        get => _logText;
        private set => SetProperty(ref _logText, value);
    }

    public ICommand BrowseHexFileCommand { get; }
    public ICommand RefreshPortsCommand { get; }
    public ICommand FlashCommand { get; }
    public ICommand CancelCommand { get; }

    private void RefreshPorts()
    {
        var previouslySelected = SelectedPort;
        AvailablePorts.Clear();

        foreach (var name in SerialBootloaderConnection.GetAvailablePortNames().OrderBy(n => n))
        {
            AvailablePorts.Add(name);
        }

        SelectedPort = AvailablePorts.Contains(previouslySelected ?? "")
            ? previouslySelected
            : AvailablePorts.FirstOrDefault();
    }

    private void BrowseHexFile()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Intel HEX files (*.hex)|*.hex|All files (*.*)|*.*",
            Title = "Select firmware image",
        };

        if (dialog.ShowDialog() == true)
        {
            HexFilePath = dialog.FileName;
        }
    }

    private bool CanFlash() =>
        !IsBusy
        && !string.IsNullOrWhiteSpace(SelectedPort)
        && !string.IsNullOrWhiteSpace(HexFilePath)
        && File.Exists(HexFilePath);

    private async Task FlashAsync()
    {
        _log.Clear();
        LogText = "";
        ProgressPercent = 0;
        IsBusy = true;
        _cancellationSource = new CancellationTokenSource();

        try
        {
            StatusText = $"Connecting to {SelectedPort}...";
            using var connection = new SerialBootloaderConnection(
                SelectedPort!, SelectedBaudRate, (int)(TimeoutSeconds * 1000));

            // DebugLog fires on the background thread FlashAsync runs on; route it
            // through Progress<T> so log lines land back on the UI thread like the
            // progress reports do, instead of touching UI-bound state directly.
            var client = new BootloaderClient(connection);
            var logProgress = new Progress<string>(AppendLog);
            client.DebugLog += line => logProgress.Report(line);
            var flasher = new FirmwareFlasher(client);

            var progress = new Progress<FlashProgressReport>(OnProgress);
            await flasher.FlashAsync(
                HexFilePath!,
                VerifyChecksum,
                ResetAfterFlash,
                progress,
                _cancellationSource.Token);

            StatusText = "Flashing complete. Self-verify OK.";
            ProgressPercent = 100;
        }
        catch (OperationCanceledException)
        {
            StatusText = "Flashing cancelled.";
        }
        catch (VerifyFailException)
        {
            StatusText = "Error: flashing completed, but the bootloader reports no bootable application.";
        }
        catch (BootloaderException ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
        catch (Exception ex) when (ex is IOException or TimeoutException)
        {
            StatusText = $"Connection error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            _cancellationSource?.Dispose();
            _cancellationSource = null;
        }
    }

    private void Cancel() => _cancellationSource?.Cancel();

    private void OnProgress(FlashProgressReport report)
    {
        StatusText = report.Stage switch
        {
            FlashStage.Handshaking => "Reading bootloader attributes...",
            FlashStage.Erasing => $"Erasing program memory... {FormatBytes(report.BytesDone)} / {FormatBytes(report.BytesTotal)}",
            FlashStage.Writing => $"Writing firmware... {FormatBytes(report.BytesDone)} / {FormatBytes(report.BytesTotal)}",
            FlashStage.SelfVerifying => "Running self-verification...",
            FlashStage.Resetting => "Resetting device...",
            _ => StatusText,
        };

        if (report.BytesTotal > 0)
        {
            // Erasing and writing each cover half of the overall progress bar.
            var stageFraction = (double)report.BytesDone / report.BytesTotal;
            var baseFraction = report.Stage == FlashStage.Erasing ? 0.0 : 0.5;
            ProgressPercent = (baseFraction + (stageFraction * 0.5)) * 100;
        }
    }

    private void AppendLog(string line)
    {
        _log.AppendLine(line);
        LogText = _log.ToString();
    }

    private static string FormatBytes(long bytes) => bytes < 1024
        ? $"{bytes} B"
        : $"{bytes / 1024.0:0.#} KiB";
}
