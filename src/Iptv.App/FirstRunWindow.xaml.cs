using System.Windows;

namespace Iptv.App;

public enum FirstRunAction
{
    None,
    LoadSample,
    OpenPlaylistFile,
    ImportPlaylistUrl
}

public partial class FirstRunWindow : Window
{
    public FirstRunWindow()
    {
        InitializeComponent();
    }

    public FirstRunAction SelectedAction { get; private set; }

    private void LoadSample_Click(object sender, RoutedEventArgs e)
    {
        Complete(FirstRunAction.LoadSample);
    }

    private void OpenFile_Click(object sender, RoutedEventArgs e)
    {
        Complete(FirstRunAction.OpenPlaylistFile);
    }

    private void ImportUrl_Click(object sender, RoutedEventArgs e)
    {
        Complete(FirstRunAction.ImportPlaylistUrl);
    }

    private void Continue_Click(object sender, RoutedEventArgs e)
    {
        Complete(FirstRunAction.None);
    }

    private void Complete(FirstRunAction action)
    {
        SelectedAction = action;
        DialogResult = true;
    }
}
