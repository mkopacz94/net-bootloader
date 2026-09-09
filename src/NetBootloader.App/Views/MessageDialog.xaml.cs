using System.Windows;
using System.Windows.Input;

namespace NetBootloader.App.Views;

/// <summary>
/// A small modal alert styled to match the app's theme, instead of the OS-chrome
/// default WPF <see cref="MessageBox"/>. Currently just an "OK to dismiss" error
/// alert; extend with more buttons/severities if another caller needs them.
/// </summary>
public partial class MessageDialog : Window
{
    public MessageDialog(string title, string message)
    {
        InitializeComponent();
        TitleTextBlock.Text = title;
        MessageTextBlock.Text = message;
    }

    /// <summary>Shows a themed modal error dialog, owned by the app's main window, with a single OK button.</summary>
    public static void ShowError(string title, string message)
    {
        var dialog = new MessageDialog(title, message)
        {
            Owner = Application.Current?.MainWindow,
        };
        dialog.ShowDialog();
    }

    private void OkButton_Click(object sender, RoutedEventArgs e) => Close();

    private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => DragMove();
}
