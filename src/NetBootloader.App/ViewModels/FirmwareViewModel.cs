using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;

namespace NetBootloader.App.ViewModels;

/// <summary>Which firmware image to flash, and how.</summary>
public sealed partial class FirmwareViewModel : ObservableObject
{
    /// <summary>
    /// Path to either a plain Intel HEX file, or an encrypted <c>.tmfw</c> package
    /// produced by <c>NetBootloader.HexPackager</c> - MainViewModel tells which is
    /// which by extension and decrypts a package in memory before flashing.
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
        var dialog = new OpenFileDialog
        {
            Filter = "Firmware files (*.hex;*.tmfw)|*.hex;*.tmfw|" +
                     "Intel HEX files (*.hex)|*.hex|" +
                     "Encrypted firmware packages (*.tmfw)|*.tmfw|" +
                     "All files (*.*)|*.*",
            Title = "Select firmware image",
        };

        if (dialog.ShowDialog() == true)
        {
            HexFilePath = dialog.FileName;
        }
    }
}
