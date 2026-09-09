using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace NetBootloader.App.Views;

/// <summary>Which icon/accent color a <see cref="MessageDialog"/> shows.</summary>
public enum MessageDialogSeverity
{
    Error,
    Success,
    Info,
}

/// <summary>
/// A small modal alert styled to match the app's theme, instead of the OS-chrome
/// default WPF <see cref="MessageBox"/>. Single "OK to dismiss" button regardless
/// of severity - only the icon badge's glyph and color change.
/// </summary>
public partial class MessageDialog : Window
{
    public MessageDialog(string title, string message, MessageDialogSeverity severity)
    {
        InitializeComponent();
        TitleTextBlock.Text = title;
        MessageTextBlock.Text = message;

        var (glyph, brushKey) = severity switch
        {
            MessageDialogSeverity.Success => ("✓", "Brush.Success"),
            MessageDialogSeverity.Info => ("i", "Brush.Primary"),
            MessageDialogSeverity.Error => ("!", "Brush.Danger"),
            _ => throw new ArgumentOutOfRangeException(nameof(severity), severity, null),
        };

        IconGlyphText.Text = glyph;
        IconBadge.Background = (Brush)FindResource(brushKey);
    }

    /// <summary>Shows a themed modal error dialog, owned by the app's main window, with a single OK button.</summary>
    public static void ShowError(string title, string message) => Show(title, message, MessageDialogSeverity.Error);

    /// <summary>Shows a themed modal success dialog, owned by the app's main window, with a single OK button.</summary>
    public static void ShowSuccess(string title, string message) => Show(title, message, MessageDialogSeverity.Success);

    /// <summary>Shows a themed modal info dialog, owned by the app's main window, with a single OK button.</summary>
    public static void ShowInfo(string title, string message) => Show(title, message, MessageDialogSeverity.Info);

    private static void Show(string title, string message, MessageDialogSeverity severity)
    {
        var dialog = new MessageDialog(title, message, severity)
        {
            Owner = Application.Current?.MainWindow,
        };
        dialog.ShowDialog();
    }

    private void OkButton_Click(object sender, RoutedEventArgs e) => Close();

    private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => DragMove();
}
