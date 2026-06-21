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
            Filter = "XMLTV files (*.xml;*.xmltv;*.gz;*.zip)|*.xml;*.xmltv;*.gz;*.zip|All files (*.*)|*.*",
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

    public string? PickSourceProfileImportFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Import source profiles",
            Filter = "IPTV source profiles (*.json)|*.json|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? PickSourceProfileExportFile()
    {
        var dialog = new SaveFileDialog
        {
            Title = "Export source profiles",
            Filter = "IPTV source profiles (*.json)|*.json|All files (*.*)|*.*",
            DefaultExt = ".json",
            AddExtension = true,
            CheckPathExists = true,
            OverwritePrompt = true,
            FileName = $"iptv-source-profiles-{DateTime.Now:yyyyMMdd-HHmmss}.json"
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? PickRecentPlaylistSourcesImportFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Import recent playlist sources",
            Filter = "IPTV recent playlist sources (*.json)|*.json|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? PickRecentPlaylistSourcesExportFile()
    {
        var dialog = new SaveFileDialog
        {
            Title = "Export recent playlist sources",
            Filter = "IPTV recent playlist sources (*.json)|*.json|All files (*.*)|*.*",
            DefaultExt = ".json",
            AddExtension = true,
            CheckPathExists = true,
            OverwritePrompt = true,
            FileName = $"iptv-recent-playlists-{DateTime.Now:yyyyMMdd-HHmmss}.json"
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? PickCustomGroupCsvImportFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Import custom group CSV",
            Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? PickCustomGroupCsvExportFile()
    {
        var dialog = new SaveFileDialog
        {
            Title = "Export custom group CSV",
            Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
            DefaultExt = ".csv",
            AddExtension = true,
            CheckPathExists = true,
            OverwritePrompt = true,
            FileName = $"iptv-custom-groups-{DateTime.Now:yyyyMMdd-HHmmss}.csv"
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? PickDiagnosticsExportFile()
    {
        var dialog = new SaveFileDialog
        {
            Title = "Export redacted diagnostics",
            Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
            DefaultExt = ".txt",
            AddExtension = true,
            CheckPathExists = true,
            OverwritePrompt = true,
            FileName = $"iptv-diagnostics-{DateTime.Now:yyyyMMdd-HHmmss}.txt"
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

    public string? PromptXmltvUrl()
    {
        var dialog = new UrlPromptWindow
        {
            Owner = Application.Current.MainWindow,
            Title = "Import XMLTV URL"
        };

        return dialog.ShowDialog() == true ? dialog.PlaylistUrl : null;
    }

    public bool ConfirmDuplicateHide(string title, IReadOnlyList<string> previewLines)
    {
        var dialog = new DuplicatePreviewWindow(title, previewLines)
        {
            Owner = Application.Current.MainWindow
        };

        return dialog.ShowDialog() == true;
    }

    public bool ConfirmSourceProfileImport(string title, IReadOnlyList<string> previewLines)
    {
        var dialog = new PreviewConfirmWindow(
            title,
            "The imported source profile file updates existing profile settings. Review the conflicts before applying.",
            "Import Profiles",
            previewLines)
        {
            Owner = Application.Current.MainWindow
        };

        return dialog.ShowDialog() == true;
    }

    public void ShowError(string title, string message)
    {
        MessageBox.Show(Application.Current.MainWindow, message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}
