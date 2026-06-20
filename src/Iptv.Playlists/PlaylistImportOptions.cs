namespace Iptv.Playlists;

public sealed record PlaylistImportOptions
{
    public long MaxPlaylistBytes { get; init; } = 25 * 1024 * 1024;

    public TimeSpan RemoteTimeout { get; init; } = TimeSpan.FromSeconds(20);
}
