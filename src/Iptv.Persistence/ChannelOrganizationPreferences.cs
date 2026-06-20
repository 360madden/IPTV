using Iptv.Core.Channels;

namespace Iptv.Persistence;

public enum ChannelViewDensity
{
    Comfortable = 0,
    Compact = 1,
    Dense = 2
}

public sealed record ChannelOrganizationPreferences
{
    public ChannelSortMode SortMode { get; init; } = ChannelSortMode.FavoritesFirst;

    public string[] CustomGroups { get; init; } = [];

    public bool LargeLibraryMode { get; init; }

    public ChannelViewDensity ChannelViewDensity { get; init; } = ChannelViewDensity.Comfortable;

    public Dictionary<string, string> SourceProfileNames { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}
