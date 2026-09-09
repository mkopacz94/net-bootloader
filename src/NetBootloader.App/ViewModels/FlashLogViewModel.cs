using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using NetBootloader.App.Localization;
using NetBootloader.Core;

namespace NetBootloader.App.ViewModels;

/// <summary>Live status, progress, and packet trace for an in-progress or just-finished flash operation.</summary>
public sealed partial class FlashLogViewModel : ObservableObject
{
    private readonly StringBuilder _log = new();

    [ObservableProperty]
    private double _progressPercent;

    [ObservableProperty]
    private string _statusText = Strings.Instance.StatusReady;

    [ObservableProperty]
    private string _logText = "";

    /// <summary>Clears progress, status, and the log, ready for a new flash operation.</summary>
    public void Reset()
    {
        _log.Clear();
        LogText = "";
        ProgressPercent = 0;
        StatusText = Strings.Instance.StatusReady;
    }

    /// <summary>Appends one packet-trace line, as forwarded from <see cref="BootloaderClient.DebugLog"/>.</summary>
    public void AppendLog(string line)
    {
        _log.AppendLine(line);
        LogText = _log.ToString();
    }

    /// <summary>Updates status text and the progress bar from a <see cref="FirmwareFlasher"/> progress report.</summary>
    public void ReportProgress(FlashProgressReport report)
    {
        var strings = Strings.Instance;
        StatusText = report.Stage switch
        {
            FlashStage.Handshaking => strings.StatusReadingBootAttrs,
            FlashStage.Erasing => strings.StatusErasing(FormatBytes(report.BytesDone), FormatBytes(report.BytesTotal)),
            FlashStage.Writing => strings.StatusWriting(FormatBytes(report.BytesDone), FormatBytes(report.BytesTotal)),
            FlashStage.SelfVerifying => strings.StatusSelfVerifying,
            FlashStage.Resetting => strings.StatusResetting,
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

    private static string FormatBytes(long bytes) => bytes < 1024
        ? $"{bytes} B"
        : $"{bytes / 1024.0:0.#} KiB";
}
