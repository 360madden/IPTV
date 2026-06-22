using System.Runtime.CompilerServices;

namespace Iptv.App.Tests;

public sealed class MainWindowStructureTests
{
    [Fact]
    public void MainWindow_ExposesAppearancePresetPreviewResetAndShortcutHelp()
    {
        string xaml = File.ReadAllText(GetMainWindowPath());

        Assert.Contains("Appearance Preset", xaml, StringComparison.Ordinal);
        Assert.Contains("Appearance Preview", xaml, StringComparison.Ordinal);
        Assert.Contains("Reset Appearance Settings", xaml, StringComparison.Ordinal);
        Assert.Contains("Keyboard Shortcut Help Overlay", xaml, StringComparison.Ordinal);
        Assert.Contains("Show Keyboard Shortcuts", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindow_ExposesSourceAppearanceAndKeyboardFocusOrderGuard()
    {
        string xaml = File.ReadAllText(GetMainWindowPath());

        Assert.Contains("Source Appearance Preset", xaml, StringComparison.Ordinal);
        Assert.Contains("Save Source Appearance", xaml, StringComparison.Ordinal);
        Assert.Contains("Use Source Appearance", xaml, StringComparison.Ordinal);
        Assert.Contains("Source Disable Hardware Decoding", xaml, StringComparison.Ordinal);
        Assert.Contains("KeyboardNavigation.TabNavigation=\"Cycle\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindow_ExposesDiscoverableEpgImportActionsInLibraryPanel()
    {
        string xaml = File.ReadAllText(GetMainWindowPath());

        Assert.Contains("EPG Import Panel", xaml, StringComparison.Ordinal);
        Assert.Contains("Polished EPG Guide", xaml, StringComparison.Ordinal);
        Assert.Contains("Upcoming programs", xaml, StringComparison.Ordinal);
        Assert.Contains("MinHeight=\"150\"", xaml, StringComparison.Ordinal);
        Assert.Contains("MaxHeight=\"300\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Import EPG File", xaml, StringComparison.Ordinal);
        Assert.Contains("Import EPG URL", xaml, StringComparison.Ordinal);
        Assert.Contains("Import EPG XMLTV File", xaml, StringComparison.Ordinal);
        Assert.Contains("Import EPG XMLTV URL", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindow_ExposesPlaybackRecoveryControlsAndDiagnostics()
    {
        string xaml = File.ReadAllText(GetMainWindowPath());

        Assert.Contains("Playback Video Surface", xaml, StringComparison.Ordinal);
        Assert.Contains("Player Primary Controls", xaml, StringComparison.Ordinal);
        Assert.Contains("Playback Recovery Panel", xaml, StringComparison.Ordinal);
        Assert.Contains("Retry Playback", xaml, StringComparison.Ordinal);
        Assert.Contains("Disable Hardware Decoding", xaml, StringComparison.Ordinal);
        Assert.Contains("Save Current Playback Settings to Source", xaml, StringComparison.Ordinal);
        Assert.Contains("Playback Troubleshooting", xaml, StringComparison.Ordinal);
        Assert.Contains("Applied Playback Profile", xaml, StringComparison.Ordinal);
        Assert.Contains("Playback Diagnostics", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindow_CollapsesLabStyleToolsBehindDrawers()
    {
        string xaml = File.ReadAllText(GetMainWindowPath());

        Assert.Contains("IPTV Primary Header Actions", xaml, StringComparison.Ordinal);
        Assert.Contains("Recent Playlist Drawer", xaml, StringComparison.Ordinal);
        Assert.Contains("More Library Tools", xaml, StringComparison.Ordinal);
        Assert.Contains("More Channel Filters", xaml, StringComparison.Ordinal);
        Assert.Contains("Header=\"Channel Organization\"", xaml, StringComparison.Ordinal);
        Assert.Contains("IsExpanded=\"False\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Height=\"Auto\"", xaml, StringComparison.Ordinal);
    }

    private static string GetMainWindowPath([CallerFilePath] string sourcePath = "")
    {
        string repoRoot = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourcePath)!, "..", ".."));
        return Path.Combine(repoRoot, "src", "Iptv.App", "MainWindow.xaml");
    }
}
