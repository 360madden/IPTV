using Iptv.Persistence;
namespace Iptv.Persistence.RecentPlaylists;

public sealed record RecentPlaylistSourcesExport
{
    public int Version { get; init; } = 1;

    public DateTimeOffset ExportedAt { get; init; } = DateTimeOffset.UtcNow;

    public RecentPlaylistSourcePreference[] Sources { get; init; } = [];
}
