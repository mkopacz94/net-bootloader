using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using NetBootloader.App.Localization;

namespace NetBootloader.App.ViewModels;

/// <summary>Which firmware image to flash, and how.</summary>
public sealed partial class FirmwareViewModel : ObservableObject
{
    /// <summary>
    /// Path to an encrypted <c>.tmfw</c> package produced by
    /// <c>NetBootloader.HexPackager</c> - MainViewModel decrypts it in memory right
    /// before flashing. Plain <c>.hex</c> files aren't accepted here; that escape
    /// hatch existed only during development and is deliberately closed off now, so
    /// the raw firmware never has to be handed to (or opened by) whoever runs this app.
    /// </summary>
    [ObservableProperty]
    private string? _hexFilePath;

    [ObservableProperty]
    private bool _verifyChecksum = true;

    [ObservableProperty]
    private bool _resetAfterFlash = true;

    [RelayCommand]
    private void BrowseHexFile()
    {
        var strings = Strings.Instance;
        var dialog = new OpenFileDialog
        {
            Filter = $"{strings.PackageFilesFilterLabel} (*.tmfw)|*.tmfw",
            Title = strings.SelectFirmwareDialogTitle,
        };

        if (dialog.ShowDialog() == true)
        {
            HexFilePath = dialog.FileName;
        }
    }
}
