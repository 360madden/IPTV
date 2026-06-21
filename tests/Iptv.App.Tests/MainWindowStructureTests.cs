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
        Assert.Contains("KeyboardNavigation.TabNavigation=\"Cycle\"", xaml, StringComparison.Ordinal);
    }

    private static string GetMainWindowPath([CallerFilePath] string sourcePath = "")
    {
        string repoRoot = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourcePath)!, "..", ".."));
        return Path.Combine(repoRoot, "src", "Iptv.App", "MainWindow.xaml");
    }
}
