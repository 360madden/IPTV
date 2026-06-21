using Iptv.Core.Channels;
using Iptv.Core.Playback;

namespace Iptv.Persistence;

public enum ChannelViewDensity
{
    Comfortable = 0,
    Compact = 1,
    Dense = 2
}

public sealed record ProviderPlaybackProfile
{
    public int RetryCount { get; init; }

    public BufferingPreset BufferingPreset { get; init; } = BufferingPreset.Balanced;
}

public sealed record ChannelOrganizationPreferences
{
    public ChannelSortMode SortMode { get; init; } = ChannelSortMode.FavoritesFirst;

    public string[] CustomGroups { get; init; } = [];

    public bool LargeLibraryMode { get; init; }

    public ChannelViewDensity ChannelViewDensity { get; init; } = ChannelViewDensity.Comfortable;

    public Dictionary<string, string> SourceProfileNames { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, ProviderPlaybackProfile> SourcePlaybackProfiles { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, string[]> SourceDefaultHiddenGroups { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public bool RefreshScheduleEnabled { get; init; }

    public int RefreshIntervalMinutes { get; init; } = 60;

    public string? ParentalPinSalt { get; init; }

    public string? ParentalPinHash { get; init; }

    public string[] LockedGroups { get; init; } = [];

    public string? XmltvGuideUrl { get; init; }

    public bool AutoLoadXmltvOnPlaylistImport { get; init; }
}
