using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace NetBootloader.App.Views;

public partial class ConnectionView : UserControl
{
    public ConnectionView()
    {
        InitializeComponent();
    }

    // Blocks anything but digits from ever reaching the timeout box - the
    // IntRangeValidationRule in ConnectionView.xaml then handles the remaining
    // out-of-range single-digit case (0, 6-9).
    private void TimeoutTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !e.Text.All(char.IsDigit);
    }

    private void TimeoutTextBox_Pasting(object sender, DataObjectPastingEventArgs e)
    {
        if (!e.DataObject.GetDataPresent(typeof(string)) ||
            e.DataObject.GetData(typeof(string)) is not string text ||
            text.Length != 1 || !char.IsDigit(text[0]))
        {
            e.CancelCommand();
        }
    }
}
