namespace Iptv.Persistence.RecentPlaylists;

public interface IRecentPlaylistSourceFileService
{
    Task<RecentPlaylistSourcesExport> ImportAsync(string path, CancellationToken cancellationToken);

    Task ExportAsync(string path, RecentPlaylistSourcesExport export, CancellationToken cancellationToken);
}
