using System.Runtime.CompilerServices;
using System.Xml.Linq;

namespace Iptv.App.Tests;

public sealed class ThemeResourceStructureTests
{
    [Fact]
    public void ControlsDictionary_DefinesAuditedInteractiveControlStyles()
    {
        XDocument controls = LoadControlsDictionary();
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

        HashSet<string> targetTypes = controls
            .Descendants(presentation + "Style")
            .Select(style => style.Attribute("TargetType")?.Value?.Trim('{', '}'))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .ToHashSet(StringComparer.Ordinal);

        string[] expectedStyles =
        [
            "TextBlock",
            "Button",
            "TextBox",
            "CheckBox",
            "Slider",
            "ComboBox",
            "ComboBoxItem",
            "ListBoxItem",
            "TabControl",
            "TabItem",
            "Expander",
            "Menu",
            "MenuItem",
            "ToolTip"
        ];

        foreach (string expectedStyle in expectedStyles)
        {
            Assert.Contains(expectedStyle, targetTypes);
        }
    }

    [Fact]
    public void ControlsDictionary_DefinesReusableKeyboardFocusVisuals()
    {
        string text = File.ReadAllText(GetControlsDictionaryPath());

        Assert.Contains("AppFocusVisualStyle", text, StringComparison.Ordinal);
        Assert.True(
            CountOccurrences(text, "FocusVisualStyle") >= 8,
            "Expected keyboard focus visuals on the common interactive controls.");
    }

    [Fact]
    public void ControlsDictionary_UsesCustomButtonTemplateForReadableDisabledButtons()
    {
        string text = File.ReadAllText(GetControlsDictionaryPath());

        Assert.Contains("ButtonChrome", text, StringComparison.Ordinal);
        Assert.Contains("DisabledControlSurfaceBrush", text, StringComparison.Ordinal);
        Assert.Contains("DisabledControlBorderBrush", text, StringComparison.Ordinal);
    }

    private static XDocument LoadControlsDictionary() => XDocument.Load(GetControlsDictionaryPath());

    private static string GetControlsDictionaryPath([CallerFilePath] string sourcePath = "")
    {
        string repoRoot = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourcePath)!, "..", ".."));
        return Path.Combine(repoRoot, "src", "Iptv.App", "Themes", "Controls.xaml");
    }

    private static int CountOccurrences(string value, string needle)
    {
        int count = 0;
        int index = 0;
        while ((index = value.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }

        return count;
    }
}
