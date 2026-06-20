using Iptv.Core.Channels;

namespace Iptv.Search;

public sealed class ChannelSearchService : IChannelSearchService
{
    public IReadOnlyList<Channel> Search(IEnumerable<Channel> channels, ChannelSearchQuery query)
    {
        ArgumentNullException.ThrowIfNull(channels);
        ArgumentNullException.ThrowIfNull(query);

        string normalizedText = ChannelNormalizer.NormalizeForSearch(query.Text);
        IEnumerable<Channel> filtered = channels;

        filtered = query.HiddenFilter switch
        {
            HiddenChannelFilter.VisibleOnly => filtered.Where(channel => !channel.IsHidden),
            HiddenChannelFilter.HiddenOnly => filtered.Where(channel => channel.IsHidden),
            HiddenChannelFilter.IncludeHidden => filtered,
            _ => filtered.Where(channel => !channel.IsHidden)
        };

        if (!string.IsNullOrWhiteSpace(query.Group))
        {
            filtered = filtered.Where(channel => channel.EffectiveGroupTitle.Equals(query.Group, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(query.Category))
        {
            filtered = filtered.Where(channel => channel.Category.Equals(query.Category, StringComparison.OrdinalIgnoreCase));
        }

        if (query.ContentKind is ContentKind contentKind)
        {
            filtered = filtered.Where(channel => channel.ContentKind == contentKind);
        }

        if (query.VodYear is int vodYear)
        {
            filtered = filtered.Where(channel =>
                channel.ContentKind is ContentKind.Vod or ContentKind.Series &&
                ChannelMetadataExtractor.TryInferReleaseYear(channel.DisplayName) == vodYear);
        }

        if (query.FavoritesOnly)
        {
            filtered = filtered.Where(channel => channel.IsFavorite);
        }

        if (!string.IsNullOrWhiteSpace(normalizedText))
        {
            filtered = filtered.Where(channel =>
                channel.NormalizedName.Contains(normalizedText, StringComparison.Ordinal) ||
                ChannelNormalizer.NormalizeForSearch(channel.EffectiveGroupTitle).Contains(normalizedText, StringComparison.Ordinal) ||
                ChannelNormalizer.NormalizeForSearch(channel.GroupTitle).Contains(normalizedText, StringComparison.Ordinal) ||
                ChannelNormalizer.NormalizeForSearch(channel.Category).Contains(normalizedText, StringComparison.Ordinal) ||
                ChannelNormalizer.NormalizeForSearch(channel.TvgId).Contains(normalizedText, StringComparison.Ordinal) ||
                ChannelNormalizer.NormalizeForSearch(channel.TvgName).Contains(normalizedText, StringComparison.Ordinal));
        }

        return ApplySort(filtered, query.SortMode)
            .Take(Math.Max(1, query.Limit))
            .ToArray();
    }

    private static IOrderedEnumerable<Channel> ApplySort(IEnumerable<Channel> channels, ChannelSortMode sortMode)
    {
        return sortMode switch
        {
            ChannelSortMode.PlaylistOrder => channels
                .OrderBy(channel => channel.ImportIndex)
                .ThenBy(channel => channel.DisplayName, StringComparer.OrdinalIgnoreCase),
            ChannelSortMode.NameAscending => channels
                .OrderBy(channel => channel.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(channel => channel.ImportIndex),
            ChannelSortMode.NameDescending => channels
                .OrderByDescending(channel => channel.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(channel => channel.ImportIndex),
            ChannelSortMode.GroupThenName => channels
                .OrderBy(channel => channel.EffectiveGroupTitle, StringComparer.OrdinalIgnoreCase)
                .ThenBy(channel => channel.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(channel => channel.ImportIndex),
            ChannelSortMode.RecentlyWatched => channels
                .OrderByDescending(channel => channel.LastWatchedAt.HasValue)
                .ThenByDescending(channel => channel.LastWatchedAt)
                .ThenBy(channel => channel.DisplayName, StringComparer.OrdinalIgnoreCase),
            ChannelSortMode.HiddenLast => channels
                .OrderBy(channel => channel.IsHidden)
                .ThenBy(channel => channel.EffectiveGroupTitle, StringComparer.OrdinalIgnoreCase)
                .ThenBy(channel => channel.DisplayName, StringComparer.OrdinalIgnoreCase),
            ChannelSortMode.CustomOrder => channels
                .OrderBy(channel => channel.EffectiveGroupTitle, StringComparer.OrdinalIgnoreCase)
                .ThenBy(channel => channel.CustomSortIndex ?? channel.ImportIndex)
                .ThenBy(channel => channel.DisplayName, StringComparer.OrdinalIgnoreCase),
            ChannelSortMode.FavoritesFirst => channels
                .OrderByDescending(channel => channel.IsFavorite)
                .ThenBy(channel => channel.IsHidden)
                .ThenBy(channel => channel.EffectiveGroupTitle, StringComparer.OrdinalIgnoreCase)
                .ThenBy(channel => channel.DisplayName, StringComparer.OrdinalIgnoreCase),
            _ => channels
                .OrderByDescending(channel => channel.IsFavorite)
                .ThenBy(channel => channel.IsHidden)
                .ThenBy(channel => channel.EffectiveGroupTitle, StringComparer.OrdinalIgnoreCase)
                .ThenBy(channel => channel.DisplayName, StringComparer.OrdinalIgnoreCase)
        };
    }
}
