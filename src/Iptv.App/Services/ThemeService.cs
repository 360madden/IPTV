using System.Windows;
using Iptv.Persistence;

namespace Iptv.App.Services;

public interface IThemeService
{
    void ApplyTheme(AppTheme theme, AppUiScale uiScale);
}

public sealed class ThemeService : IThemeService
{
    private const string AssemblyResourcePrefix = "pack://application:,,,/Iptv.App;component/Themes/";

    private static readonly IReadOnlyDictionary<AppTheme, string> ThemeFiles = new Dictionary<AppTheme, string>
    {
        [AppTheme.Dark] = "Dark.xaml",
        [AppTheme.Light] = "Light.xaml",
        [AppTheme.HighContrast] = "HighContrast.xaml"
    };

    private static readonly IReadOnlyDictionary<AppUiScale, string> ScaleFiles = new Dictionary<AppUiScale, string>
    {
        [AppUiScale.Normal] = "ScaleNormal.xaml",
        [AppUiScale.Large] = "ScaleLarge.xaml",
        [AppUiScale.Tv] = "ScaleTv.xaml"
    };

    public void ApplyTheme(AppTheme theme, AppUiScale uiScale)
    {
        ApplyTheme(Application.Current?.Resources, theme, uiScale);
    }

    public static void ApplyTheme(ResourceDictionary? resources, AppTheme theme, AppUiScale uiScale)
    {
        if (resources is null)
        {
            return;
        }

        AppTheme normalizedTheme = NormalizeTheme(theme);
        AppUiScale normalizedScale = NormalizeUiScale(uiScale);

        RemoveDictionaries(resources, ThemeFiles.Values);
        RemoveDictionaries(resources, ScaleFiles.Values);

        resources.MergedDictionaries.Insert(0, CreateDictionary(ThemeFiles[normalizedTheme]));
        resources.MergedDictionaries.Insert(1, CreateDictionary(ScaleFiles[normalizedScale]));
    }

    public static AppTheme NormalizeTheme(AppTheme theme) => Enum.IsDefined(theme) ? theme : AppTheme.Dark;

    public static AppUiScale NormalizeUiScale(AppUiScale uiScale) => Enum.IsDefined(uiScale) ? uiScale : AppUiScale.Normal;

    private static ResourceDictionary CreateDictionary(string fileName) => new()
    {
        Source = new Uri(AssemblyResourcePrefix + fileName, UriKind.Absolute)
    };

    private static void RemoveDictionaries(ResourceDictionary resources, IEnumerable<string> fileNames)
    {
        HashSet<string> normalizedFileNames = fileNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        for (int index = resources.MergedDictionaries.Count - 1; index >= 0; index--)
        {
            Uri? source = resources.MergedDictionaries[index].Source;
            if (source is null)
            {
                continue;
            }

            string fileName = GetSourceFileName(source);
            if (normalizedFileNames.Contains(fileName))
            {
                resources.MergedDictionaries.RemoveAt(index);
            }
        }
    }

    private static string GetSourceFileName(Uri source)
    {
        string value = source.OriginalString.Replace('\\', '/');
        int separator = value.LastIndexOf('/');
        return separator >= 0 ? value[(separator + 1)..] : value;
    }
}
