using Iptv.Core.Playback;

namespace Iptv.Persistence.SourceProfiles;

public sealed record SourceProfileExport
{
    public int Version { get; init; } = 1;

    public Dictionary<string, string> SourceProfileNames { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, ProviderPlaybackProfile> SourcePlaybackProfiles { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, string[]> SourceDefaultHiddenGroups { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}
