using System.Runtime.CompilerServices;
using System.Xml.Linq;

namespace Iptv.App.Tests;

public sealed class ThemeContrastTests
{
    private static readonly string[] ThemeFiles = ["Dark.xaml", "Light.xaml", "HighContrast.xaml"];

    [Theory]
    [MemberData(nameof(Themes))]
    public void ThemeColorTokens_MaintainReadableTextContrast(string themeFile)
    {
        Dictionary<string, RgbColor> colors = LoadThemeColors(themeFile);

        AssertContrast(themeFile, colors, "TextPrimaryColor", "AppBackgroundColor", 4.5);
        AssertContrast(themeFile, colors, "TextPrimaryColor", "PanelColor", 4.5);
        AssertContrast(themeFile, colors, "TextPrimaryColor", "PanelAltColor", 4.5);
        AssertContrast(themeFile, colors, "TextPrimaryColor", "ControlSurfaceColor", 4.5);
        AssertContrast(themeFile, colors, "TextPrimaryColor", "DropdownSurfaceColor", 4.5);
        AssertContrast(themeFile, colors, "TextPrimaryColor", "PlayerPlaceholderColor", 4.5);
        AssertContrast(themeFile, colors, "TextSecondaryColor", "AppBackgroundColor", 4.5);
        AssertContrast(themeFile, colors, "TextSecondaryColor", "PanelColor", 4.5);
        AssertContrast(themeFile, colors, "TextSecondaryColor", "PanelAltColor", 4.5);
        AssertContrast(themeFile, colors, "AccentTextColor", "AccentColor", 4.5);
        AssertContrast(themeFile, colors, "SelectionTextColor", "DropdownSelectedColor", 4.5);
    }

    [Theory]
    [MemberData(nameof(Themes))]
    public void ThemeColorTokens_MaintainVisibleBordersAndFocus(string themeFile)
    {
        Dictionary<string, RgbColor> colors = LoadThemeColors(themeFile);

        AssertContrast(themeFile, colors, "ControlBorderColor", "ControlSurfaceColor", 3.0);
        AssertContrast(themeFile, colors, "FocusRingColor", "ControlSurfaceColor", 3.0);
        AssertContrast(themeFile, colors, "PlayerPlaceholderBorderColor", "PlayerPlaceholderColor", 3.0);
    }

    public static IEnumerable<object[]> Themes() => ThemeFiles.Select(theme => new object[] { theme });

    private static Dictionary<string, RgbColor> LoadThemeColors(string themeFile, [CallerFilePath] string sourcePath = "")
    {
        string repoRoot = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourcePath)!, "..", ".."));
        string themePath = Path.Combine(repoRoot, "src", "Iptv.App", "Themes", themeFile);
        Assert.True(File.Exists(themePath), $"Theme dictionary not found: {themePath}");

        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
        XDocument document = XDocument.Load(themePath);
        return document
            .Descendants()
            .Where(element => element.Name.LocalName == "Color")
            .Select(element => new
            {
                Key = element.Attribute(xaml + "Key")?.Value,
                Value = element.Value.Trim()
            })
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Key))
            .ToDictionary(entry => entry.Key!, entry => ParseColor(entry.Value), StringComparer.Ordinal);
    }

    private static void AssertContrast(
        string themeFile,
        IReadOnlyDictionary<string, RgbColor> colors,
        string foregroundKey,
        string backgroundKey,
        double minimum)
    {
        Assert.True(colors.TryGetValue(foregroundKey, out RgbColor foreground), $"{themeFile} missing {foregroundKey}.");
        Assert.True(colors.TryGetValue(backgroundKey, out RgbColor background), $"{themeFile} missing {backgroundKey}.");

        double ratio = ContrastRatio(foreground, background);
        Assert.True(
            ratio >= minimum,
            $"{themeFile} contrast {foregroundKey} on {backgroundKey} was {ratio:F2}; expected at least {minimum:F1}.");
    }

    private static RgbColor ParseColor(string value)
    {
        string hex = value.Trim().TrimStart('#');
        if (hex.Length == 8)
        {
            hex = hex[2..];
        }

        Assert.Equal(6, hex.Length);
        return new RgbColor(
            Convert.ToByte(hex[0..2], 16),
            Convert.ToByte(hex[2..4], 16),
            Convert.ToByte(hex[4..6], 16));
    }

    private static double ContrastRatio(RgbColor first, RgbColor second)
    {
        double firstLuminance = RelativeLuminance(first);
        double secondLuminance = RelativeLuminance(second);
        double lighter = Math.Max(firstLuminance, secondLuminance);
        double darker = Math.Min(firstLuminance, secondLuminance);
        return (lighter + 0.05) / (darker + 0.05);
    }

    private static double RelativeLuminance(RgbColor color) =>
        0.2126 * Linearize(color.Red) +
        0.7152 * Linearize(color.Green) +
        0.0722 * Linearize(color.Blue);

    private static double Linearize(byte value)
    {
        double normalized = value / 255.0;
        return normalized <= 0.03928
            ? normalized / 12.92
            : Math.Pow((normalized + 0.055) / 1.055, 2.4);
    }

    private readonly record struct RgbColor(byte Red, byte Green, byte Blue);
}
