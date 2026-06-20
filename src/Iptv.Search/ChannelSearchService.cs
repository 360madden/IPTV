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

        if (!string.IsNullOrWhiteSpace(query.Group))
        {
            filtered = filtered.Where(channel => channel.GroupTitle.Equals(query.Group, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(query.Category))
        {
            filtered = filtered.Where(channel => channel.Category.Equals(query.Category, StringComparison.OrdinalIgnoreCase));
        }

        if (query.FavoritesOnly)
        {
            filtered = filtered.Where(channel => channel.IsFavorite);
        }

        if (!string.IsNullOrWhiteSpace(normalizedText))
        {
            filtered = filtered.Where(channel =>
                channel.NormalizedName.Contains(normalizedText, StringComparison.Ordinal) ||
                ChannelNormalizer.NormalizeForSearch(channel.GroupTitle).Contains(normalizedText, StringComparison.Ordinal) ||
                ChannelNormalizer.NormalizeForSearch(channel.Category).Contains(normalizedText, StringComparison.Ordinal) ||
                ChannelNormalizer.NormalizeForSearch(channel.TvgId).Contains(normalizedText, StringComparison.Ordinal) ||
                ChannelNormalizer.NormalizeForSearch(channel.TvgName).Contains(normalizedText, StringComparison.Ordinal));
        }

        return filtered
            .OrderByDescending(channel => channel.IsFavorite)
            .ThenBy(channel => channel.GroupTitle, StringComparer.OrdinalIgnoreCase)
            .ThenBy(channel => channel.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Max(1, query.Limit))
            .ToArray();
    }
}
