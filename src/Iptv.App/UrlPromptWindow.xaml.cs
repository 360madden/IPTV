using System.Windows;

namespace Iptv.App;

public partial class UrlPromptWindow : Window
{
    public UrlPromptWindow()
    {
        InitializeComponent();
        UrlTextBox.SelectAll();
        UrlTextBox.Focus();
    }

    public string? PlaylistUrl { get; private set; }

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        PlaylistUrl = UrlTextBox.Text.Trim();
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
