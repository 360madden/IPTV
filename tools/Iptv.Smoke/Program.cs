using Iptv.Core.Channels;
using Iptv.Core.Diagnostics;
using Iptv.Core.PlaylistImport;
using Iptv.Playlists;
using LibVLCSharp.Shared;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        SmokeOptions options = SmokeOptions.Parse(args);
        if (options.ShowHelp || (options.Url is null && options.File is null))
        {
            SmokeOptions.PrintHelp();
            return options.ShowHelp ? 0 : 2;
        }

        var importService = new PlaylistImportService(new M3uPlaylistParser());
        PlaylistImportResult result = options.Url is not null
            ? await importService.ImportUrlAsync(options.Url, CancellationToken.None).ConfigureAwait(false)
            : await importService.ImportFileAsync(options.File!, CancellationToken.None).ConfigureAwait(false);

        Console.WriteLine($"IMPORT channels={result.Channels.Count} warnings={result.Summary.WarningCount} errors={result.Summary.ErrorCount} duplicates={result.Summary.DuplicateCount}");
        foreach (var group in result.Channels.GroupBy(channel => channel.GroupTitle).OrderByDescending(group => group.Count()).Take(8))
        {
            Console.WriteLine($"GROUP count={group.Count()} name=\"{group.Key}\"");
        }

        foreach (PlaylistImportIssue issue in result.Issues.Take(10))
        {
            Console.WriteLine($"ISSUE severity={issue.Severity} code={issue.Code} line={issue.LineNumber?.ToString() ?? "-"} message=\"{issue.Message}\"");
        }

        if (result.Channels.Count == 0 || options.ProbeCount <= 0)
        {
            return result.Summary.ErrorCount > 0 ? 1 : 0;
        }

        IReadOnlyList<Channel> probeChannels = result.Channels
            .Where(channel => string.IsNullOrWhiteSpace(options.Search) ||
                              channel.DisplayName.Contains(options.Search, StringComparison.OrdinalIgnoreCase) ||
                              channel.GroupTitle.Contains(options.Search, StringComparison.OrdinalIgnoreCase))
            .Take(options.ProbeCount)
            .ToArray();

        Console.WriteLine($"PROBE_START count={probeChannels.Count} timeoutSeconds={options.TimeoutSeconds}");
        int failures = 0;
        foreach (Channel channel in probeChannels)
        {
            ProbeResult probe = await StreamProbe.ProbeAsync(channel, TimeSpan.FromSeconds(options.TimeoutSeconds)).ConfigureAwait(false);
            if (!probe.Success)
            {
                failures++;
            }

            Console.WriteLine(
                $"PROBE channel=\"{channel.DisplayName}\" group=\"{channel.GroupTitle}\" host=\"{channel.StreamUrl.Host}\" status={probe.Status} message=\"{probe.Message}\"");
        }

        Console.WriteLine($"PROBE_DONE failures={failures}");
        return failures == probeChannels.Count ? 1 : 0;
    }
}

internal sealed record SmokeOptions(
    string? Url,
    string? File,
    int ProbeCount,
    int TimeoutSeconds,
    string? Search,
    bool ShowHelp)
{
    public static SmokeOptions Parse(string[] args)
    {
        string? url = null;
        string? file = null;
        string? search = null;
        int probeCount = 0;
        int timeoutSeconds = 20;
        bool showHelp = false;

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            string? next = i + 1 < args.Length ? args[i + 1] : null;

            switch (arg)
            {
                case "--url" when next is not null:
                    url = next;
                    i++;
                    break;
                case "--file" when next is not null:
                    file = next;
                    i++;
                    break;
                case "--probe-count" when next is not null && int.TryParse(next, out int parsedProbeCount):
                    probeCount = Math.Clamp(parsedProbeCount, 0, 25);
                    i++;
                    break;
                case "--timeout-seconds" when next is not null && int.TryParse(next, out int parsedTimeout):
                    timeoutSeconds = Math.Clamp(parsedTimeout, 5, 90);
                    i++;
                    break;
                case "--search" when next is not null:
                    search = next;
                    i++;
                    break;
                case "-h":
                case "--help":
                    showHelp = true;
                    break;
            }
        }

        return new SmokeOptions(url, file, probeCount, timeoutSeconds, search, showHelp);
    }

    public static void PrintHelp()
    {
        Console.WriteLine("IPTV smoke tester");
        Console.WriteLine("Usage:");
        Console.WriteLine("  dotnet run --project tools/Iptv.Smoke -- --url https://example/playlist.m3u --probe-count 5");
        Console.WriteLine("  dotnet run --project tools/Iptv.Smoke -- --file playlist.m3u8 --search news --probe-count 3");
    }
}

internal static class StreamProbe
{
    public static async Task<ProbeResult> ProbeAsync(Channel channel, TimeSpan timeout)
    {
        try
        {
            LibVLCSharp.Shared.Core.Initialize();
            using var libVlc = new LibVLC(
                "--intf=dummy",
                "--quiet",
                "--vout=dummy",
                "--aout=dummy",
                "--no-video-title-show",
                "--no-stats",
                "--network-caching=1200",
                "--live-caching=1200");
            using var mediaPlayer = new MediaPlayer(libVlc);
            using var media = new Media(libVlc, channel.StreamUrl.Uri);
            media.AddOption(":network-caching=1200");
            media.AddOption(":live-caching=1200");

            var completion = new TaskCompletionSource<ProbeResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            mediaPlayer.Playing += (_, _) => completion.TrySetResult(new ProbeResult(true, "Playing", "Playback reached Playing state."));
            mediaPlayer.EncounteredError += (_, _) => completion.TrySetResult(new ProbeResult(false, "Failed", "LibVLC reported a playback error."));
            mediaPlayer.EndReached += (_, _) => completion.TrySetResult(new ProbeResult(false, "Ended", "Stream ended before playback could be observed."));

            bool accepted = mediaPlayer.Play(media);
            if (!accepted)
            {
                return new ProbeResult(false, "Rejected", "LibVLC rejected the stream.");
            }

            Task winner = await Task.WhenAny(completion.Task, Task.Delay(timeout)).ConfigureAwait(false);
            mediaPlayer.Stop();

            if (winner == completion.Task)
            {
                return await completion.Task.ConfigureAwait(false);
            }

            return new ProbeResult(false, "TimedOut", "No Playing/error event before timeout.");
        }
        catch (Exception ex)
        {
            return new ProbeResult(false, "Exception", SensitiveTextRedactor.RedactText(ex.Message));
        }
    }
}

internal sealed record ProbeResult(bool Success, string Status, string Message);
