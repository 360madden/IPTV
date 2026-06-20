using System.Diagnostics;
using Iptv.Core;
using Iptv.Core.Channels;
using Iptv.Search;

int count = GetIntArgument(args, "--count", 50_000);
count = Math.Clamp(count, 1_000, 500_000);

Channel[] channels = CreateSyntheticChannels(count);
var service = new ChannelSearchService();
var scenarios = new (string Name, ChannelSearchQuery Query)[]
{
    ("name search", new ChannelSearchQuery { Text = "news", Limit = 500 }),
    ("VOD filter", new ChannelSearchQuery { ContentKind = ContentKind.Vod, Limit = 500 }),
    ("group sort", new ChannelSearchQuery { SortMode = ChannelSortMode.GroupThenName, Limit = 1_000 }),
    ("resume sort", new ChannelSearchQuery { SortMode = ChannelSortMode.RecentlyWatched, Limit = 1_000 })
};

Console.WriteLine($"IPTV search benchmark: {channels.Length:N0} synthetic channels");
foreach ((string name, ChannelSearchQuery query) in scenarios)
{
    Stopwatch stopwatch = Stopwatch.StartNew();
    int resultCount = service.Search(channels, query).Count;
    stopwatch.Stop();
    Console.WriteLine($"{name}: {resultCount:N0} results in {stopwatch.ElapsedMilliseconds:N0} ms");
}

static int GetIntArgument(string[] args, string name, int fallback)
{
    for (int index = 0; index < args.Length - 1; index++)
    {
        if (args[index].Equals(name, StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(args[index + 1], out int parsed))
        {
            return parsed;
        }
    }

    return fallback;
}

static Channel[] CreateSyntheticChannels(int count)
{
    SensitiveUri.TryCreate("https://benchmark.example/stream.m3u8", out SensitiveUri? uri, out _);
    SensitiveUri streamUri = uri ?? throw new InvalidOperationException("Benchmark URI could not be created.");
    var channels = new Channel[count];
    for (int index = 0; index < channels.Length; index++)
    {
        string name = index % 3 == 0 ? $"Benchmark Movie {index:000000} (2025)" : $"Benchmark News {index:000000}";
        channels[index] = new Channel
        {
            Id = StableId.Create("benchmark", index.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            SourceId = StableId.Create("benchmark-source", "local"),
            RawName = name,
            DisplayName = name,
            NormalizedName = ChannelNormalizer.NormalizeForSearch(name),
            StreamUrl = streamUri,
            ImportIndex = index,
            GroupTitle = index % 3 == 0 ? "Benchmark VOD" : "Benchmark Live",
            Category = index % 3 == 0 ? "Movies" : "News",
            ContentKind = index % 3 == 0 ? ContentKind.Vod : ContentKind.LiveTv,
            LastWatchedAt = index % 7 == 0 ? DateTimeOffset.UtcNow.AddMinutes(-index) : null,
            ResumeProgressPercent = index % 11 == 0 ? index % 100 : null
        };
    }

    return channels;
}
