using Iptv.Core.PlaylistImport;

namespace Iptv.Playlists;

public interface IPlaylistImportService
{
    Task<PlaylistImportResult> ImportFileAsync(
        string path,
        CancellationToken cancellationToken,
        IProgress<PlaylistImportProgress>? progress = null);

    Task<PlaylistImportResult> ImportUrlAsync(
        string url,
        CancellationToken cancellationToken,
        IProgress<PlaylistImportProgress>? progress = null);
}
