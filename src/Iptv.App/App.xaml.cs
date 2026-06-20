using System.Windows;

namespace Iptv.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
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
}
