using Iptv.Core.Channels;

namespace Iptv.Persistence;

public sealed record ChannelOrganizationPreferences
{
    public ChannelSortMode SortMode { get; init; } = ChannelSortMode.FavoritesFirst;

    public string[] CustomGroups { get; init; } = [];

    public bool LargeLibraryMode { get; init; }
}
