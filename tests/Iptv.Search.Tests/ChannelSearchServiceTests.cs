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
            CreateChannel("Visible News", "News", importIndex: 0),
            CreateChannel("Hidden Sports", "Sports", isHidden: true, importIndex: 1)
        };
        var service = new ChannelSearchService();

        IReadOnlyList<Channel> results = service.Search(channels, new ChannelSearchQuery
        {
            HiddenFilter = HiddenChannelFilter.HiddenOnly
        });

        Channel result = Assert.Single(results);
        Assert.Equal("Hidden Sports", result.DisplayName);
    }

    [Fact]
    public void Search_FiltersByContentKind()
    {
        var channels = new[]
        {
            CreateChannel("Live News", "News", contentKind: ContentKind.LiveTv),
            CreateChannel("Movie One", "Movies", contentKind: ContentKind.Vod),
            CreateChannel("Series One", "Series", contentKind: ContentKind.Series)
        };
        var service = new ChannelSearchService();

        IReadOnlyList<Channel> results = service.Search(channels, new ChannelSearchQuery
        {
            ContentKind = ContentKind.Vod
        });

        Channel result = Assert.Single(results);
        Assert.Equal("Movie One", result.DisplayName);
    }

    [Fact]
    public void Search_SortsByPlaylistOrder()
    {
        var channels = new[]
        {
            CreateChannel("Second", "News", importIndex: 1),
            CreateChannel("First", "News", importIndex: 0)
        };
        var service = new ChannelSearchService();

        IReadOnlyList<Channel> results = service.Search(channels, new ChannelSearchQuery
        {
            SortMode = ChannelSortMode.PlaylistOrder
        });

        Assert.Equal(["First", "Second"], results.Select(channel => channel.DisplayName));
    }

    [Fact]
    public void Search_SortsByCustomOrderWithinGroup()
    {
        var channels = new[]
        {
            CreateChannel("Later", "News", customSortIndex: 20),
            CreateChannel("Earlier", "News", customSortIndex: 10)
        };
        var service = new ChannelSearchService();

        IReadOnlyList<Channel> results = service.Search(channels, new ChannelSearchQuery
        {
            SortMode = ChannelSortMode.CustomOrder
        });

        Assert.Equal(["Earlier", "Later"], results.Select(channel => channel.DisplayName));
    }

    [Fact]
    public void Search_SortsByRecentlyWatched()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var channels = new[]
        {
            CreateChannel("Older", "News", lastWatchedAt: now.AddMinutes(-5)),
            CreateChannel("Newer", "News", lastWatchedAt: now)
        };
        var service = new ChannelSearchService();

        IReadOnlyList<Channel> results = service.Search(channels, new ChannelSearchQuery
        {
            SortMode = ChannelSortMode.RecentlyWatched
        });

        Assert.Equal(["Newer", "Older"], results.Select(channel => channel.DisplayName));
    }

    private static Channel CreateChannel(
        string name,
        string group,
        bool isFavorite = false,
        bool isHidden = false,
        string? customGroup = null,
        int importIndex = 0,
        int? customSortIndex = null,
        DateTimeOffset? lastWatchedAt = null,
        ContentKind contentKind = ContentKind.LiveTv)
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
            ImportIndex = importIndex,
            GroupTitle = group,
            CustomGroup = customGroup,
            CustomSortIndex = customSortIndex,
            Category = ChannelNormalizer.InferCategory(group, name),
            ContentKind = contentKind,
            IsFavorite = isFavorite,
            IsHidden = isHidden,
            LastWatchedAt = lastWatchedAt
        };
    }
}
