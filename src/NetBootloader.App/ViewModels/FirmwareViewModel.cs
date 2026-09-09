using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;

namespace NetBootloader.App.ViewModels;

/// <summary>Which firmware image to flash, and how.</summary>
public sealed partial class FirmwareViewModel : ObservableObject
{
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
            Filter = "Intel HEX files (*.hex)|*.hex|All files (*.*)|*.*",
            Title = "Select firmware image",
        };

        if (dialog.ShowDialog() == true)
        {
            HexFilePath = dialog.FileName;
        }
    }
}
