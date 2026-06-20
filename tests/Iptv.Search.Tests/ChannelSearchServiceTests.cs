using Iptv.Core;
using Iptv.Core.Channels;
using Iptv.Search;

namespace Iptv.Search.Tests;

public sealed class ChannelSearchServiceTests
{
    [Fact]
    public void Search_FiltersByTextGroupAndFavorites()
    {
        var channels = new[]
        {
            CreateChannel("Example News", "News", isFavorite: true),
            CreateChannel("Example Sports", "Sports", isFavorite: false)
        };
        var service = new ChannelSearchService();

        IReadOnlyList<Channel> results = service.Search(channels, new ChannelSearchQuery
        {
            Text = "news",
            Group = "News",
            FavoritesOnly = true
        });

        Channel result = Assert.Single(results);
        Assert.Equal("Example News", result.DisplayName);
    }

    private static Channel CreateChannel(string name, string group, bool isFavorite)
    {
        Assert.True(SensitiveUri.TryCreate($"https://stream.example/{Uri.EscapeDataString(name)}.m3u8", out SensitiveUri? uri, out string? error), error);

        return new Channel
        {
            Id = Guid.CreateVersion7(),
            SourceId = Guid.CreateVersion7(),
            RawName = name,
            DisplayName = name,
            NormalizedName = ChannelNormalizer.NormalizeForSearch(name),
            StreamUrl = uri!,
            GroupTitle = group,
            Category = ChannelNormalizer.InferCategory(group, name),
            IsFavorite = isFavorite
        };
    }
}
