using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetBootloader.Core.Communication;

namespace NetBootloader.App.ViewModels;

/// <summary>Serial connection settings: which port, baud rate, and read timeout to use.</summary>
public sealed partial class ConnectionViewModel : ObservableObject
{
    private static readonly int[] CommonBaudRates = { 9600, 19200, 38400, 57600, 115200, 230400, 460800 };

    public ConnectionViewModel()
    {
        AvailablePorts = new ObservableCollection<string>();
        BaudRates = new ObservableCollection<int>(CommonBaudRates);
        SelectedBaudRate = 460800;
        TimeoutSeconds = 1.0;
        RefreshPorts();
    }

    public ObservableCollection<string> AvailablePorts { get; }

    public ObservableCollection<int> BaudRates { get; }

    [ObservableProperty]
    private string? _selectedPort;

    [ObservableProperty]
    private int _selectedBaudRate;

    [ObservableProperty]
    private double _timeoutSeconds;

    [RelayCommand]
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
}
