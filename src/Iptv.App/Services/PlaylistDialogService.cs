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

    public string? PickOrganizationImportFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Import channel organization",
            Filter = "IPTV organization backups (*.json)|*.json|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? PickOrganizationExportFile()
    {
        var dialog = new SaveFileDialog
        {
            Title = "Export channel organization",
            Filter = "IPTV organization backups (*.json)|*.json|All files (*.*)|*.*",
            DefaultExt = ".json",
            AddExtension = true,
            CheckPathExists = true,
            OverwritePrompt = true,
            FileName = $"iptv-organization-{DateTime.Now:yyyyMMdd-HHmmss}.json"
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? PickSmartGroupPresetImportFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Import smart group presets",
            Filter = "IPTV smart group presets (*.json)|*.json|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? PickSmartGroupPresetExportFile()
    {
        var dialog = new SaveFileDialog
        {
            Title = "Export smart group presets",
            Filter = "IPTV smart group presets (*.json)|*.json|All files (*.*)|*.*",
            DefaultExt = ".json",
            AddExtension = true,
            CheckPathExists = true,
            OverwritePrompt = true,
            FileName = $"iptv-smart-groups-{DateTime.Now:yyyyMMdd-HHmmss}.json"
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
