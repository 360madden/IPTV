using System.Diagnostics;
using Iptv.Core;
using Iptv.Core.Channels;
using Iptv.Search;

namespace Iptv.Search.Tests;

public sealed class LargeSearchPerformanceTests
{
    [Fact]
    public void Search_FiltersFiftyThousandChannelsWithinReasonableTime()
    {
        const int channelCount = 50_000;
        Channel[] channels = Enumerable.Range(0, channelCount)
            .Select(index => CreateChannel(index))
            .ToArray();
        var service = new ChannelSearchService();

        var stopwatch = Stopwatch.StartNew();
        IReadOnlyList<Channel> results = service.Search(channels, new ChannelSearchQuery
        {
            Text = "Channel 49999",
            Limit = 50
        });
        stopwatch.Stop();

        Channel result = Assert.Single(results);
        Assert.Equal("Channel 49999", result.DisplayName);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(8), $"50k search took {stopwatch.Elapsed}.");
    }

    private static Channel CreateChannel(int index)
    {
        string name = $"Channel {index}";
        Assert.True(SensitiveUri.TryCreate($"https://stream.example/live/{index}.m3u8", out SensitiveUri? uri, out string? error), error);

        return new Channel
        {
            Id = Guid.CreateVersion7(),
            SourceId = Guid.CreateVersion7(),
            RawName = name,
            DisplayName = name,
            NormalizedName = ChannelNormalizer.NormalizeForSearch(name),
            StreamUrl = uri!,
            ImportIndex = index,
            GroupTitle = index % 2 == 0 ? "News" : "Sports",
            Category = index % 2 == 0 ? "News" : "Sports"
        };
    }
}
