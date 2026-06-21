using Iptv.Core;
using Iptv.Core.PlaylistImport;

namespace Iptv.Playlists;

public interface IPlaylistParser
{
    Task<PlaylistImportResult> ParseAsync(
        Stream content,
        PlaylistSource source,
        CancellationToken cancellationToken,
        IProgress<PlaylistImportProgress>? progress = null);
}
