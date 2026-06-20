using System.Windows;
using Microsoft.Win32;

namespace Iptv.App.Services;

public sealed class PlaylistDialogService : IPlaylistDialogService
{
    public string? PickPlaylistFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Import IPTV playlist",
            Filter = "IPTV playlists (*.m3u;*.m3u8)|*.m3u;*.m3u8|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? PickXmltvFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Import XMLTV guide",
            Filter = "XMLTV files (*.xml;*.xmltv)|*.xml;*.xmltv|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? PromptPlaylistUrl()
    {
        var dialog = new UrlPromptWindow
        {
            Owner = Application.Current.MainWindow
        };

        return dialog.ShowDialog() == true ? dialog.PlaylistUrl : null;
    }

    public void ShowError(string title, string message)
    {
        MessageBox.Show(Application.Current.MainWindow, message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}
