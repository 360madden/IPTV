using System.IO;
using System.Windows;
using Iptv.App.Services;
using Iptv.Persistence;

namespace Iptv.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        ApplyPersistedAppearancePreferences();

        base.OnStartup(e);

        var window = new MainWindow(GetStartupPlaylistUrl(e.Args), GetStartupPlaylistFile(e.Args));
        MainWindow = window;
        window.Show();
    }

    private static string? GetStartupPlaylistUrl(IReadOnlyList<string> args)
    {
        for (int index = 0; index < args.Count; index++)
        {
            string argument = args[index];
            if (argument.StartsWith("--playlist-url=", StringComparison.OrdinalIgnoreCase))
            {
                return argument["--playlist-url=".Length..].Trim();
            }

            if (string.Equals(argument, "--playlist-url", StringComparison.OrdinalIgnoreCase) &&
                index + 1 < args.Count)
            {
                return args[index + 1].Trim();
            }
        }

        return null;
    }

    private static string? GetStartupPlaylistFile(IReadOnlyList<string> args)
    {
        for (int index = 0; index < args.Count; index++)
        {
            string argument = args[index];
            if (argument.StartsWith("--playlist-file=", StringComparison.OrdinalIgnoreCase))
            {
                return argument["--playlist-file=".Length..].Trim();
            }

            if (string.Equals(argument, "--playlist-file", StringComparison.OrdinalIgnoreCase) &&
                index + 1 < args.Count)
            {
                return args[index + 1].Trim();
            }
        }

        return null;
    }

    private static void ApplyPersistedAppearancePreferences()
    {
        try
        {
            var store = new JsonUiPreferencesStore(AppServices.GetAppDataDirectoryOverride());
            UiPreferences preferences = store.LoadAsync(CancellationToken.None).GetAwaiter().GetResult();
            ThemeService.ApplyTheme(Current.Resources, preferences.AppTheme, preferences.AppUiScale);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or System.Windows.Markup.XamlParseException)
        {
            ThemeService.ApplyTheme(Current.Resources, AppTheme.Dark, AppUiScale.Normal);
        }
    }
}
