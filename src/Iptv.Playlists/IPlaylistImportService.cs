using Iptv.Core.PlaylistImport;

namespace Iptv.Playlists;

public interface IPlaylistImportService
{
    Task<PlaylistImportResult> ImportFileAsync(string path, CancellationToken cancellationToken);

    Task<PlaylistImportResult> ImportUrlAsync(string url, CancellationToken cancellationToken);
}
