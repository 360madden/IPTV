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

    [Fact]
    public void Search_UsesCustomGroupsAndSkipsHiddenByDefault()
    {
        var channels = new[]
        {
            CreateChannel("Local News", "News", customGroup: "My Favorites"),
            CreateChannel("Hidden Sports", "Sports", isHidden: true)
        };
        var service = new ChannelSearchService();

        IReadOnlyList<Channel> grouped = service.Search(channels, new ChannelSearchQuery
        {
            Group = "My Favorites"
        });
        IReadOnlyList<Channel> visibleOnly = service.Search(channels, new ChannelSearchQuery());

        Channel result = Assert.Single(grouped);
        Assert.Equal("Local News", result.DisplayName);
        Assert.DoesNotContain(visibleOnly, channel => channel.DisplayName == "Hidden Sports");
    }

    [Fact]
    public void Search_CanShowHiddenOnly()
    {
        var channels = new[]
        {
            CreateChannel("Visible News", "News"),
            CreateChannel("Hidden Sports", "Sports", isHidden: true)
        };
        var service = new ChannelSearchService();

        IReadOnlyList<Channel> results = service.Search(channels, new ChannelSearchQuery
        {
            HiddenFilter = HiddenChannelFilter.HiddenOnly
        });

        Channel result = Assert.Single(results);
        Assert.Equal("Hidden Sports", result.DisplayName);
    }

    private static Channel CreateChannel(
        string name,
        string group,
        bool isFavorite = false,
        bool isHidden = false,
        string? customGroup = null)
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
            CustomGroup = customGroup,
            Category = ChannelNormalizer.InferCategory(group, name),
            IsFavorite = isFavorite,
            IsHidden = isHidden
        };
    }
}
