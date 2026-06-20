using Iptv.Core;
using Iptv.Core.Diagnostics;
using Iptv.Core.PlaylistImport;

namespace Iptv.Playlists;

public sealed class PlaylistImportService : IPlaylistImportService
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".m3u",
        ".m3u8"
    };

    private readonly IPlaylistParser parser;
    private readonly HttpClient httpClient;
    private readonly PlaylistImportOptions options;

    public PlaylistImportService(IPlaylistParser parser, HttpClient? httpClient = null, PlaylistImportOptions? options = null)
    {
        this.parser = parser;
        this.httpClient = httpClient ?? new HttpClient();
        this.options = options ?? new PlaylistImportOptions();
    }

    public async Task<PlaylistImportResult> ImportFileAsync(string path, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return Failure("invalid-path", "Playlist path is empty.");
        }

        var file = new FileInfo(path);
        if (!file.Exists)
        {
            return Failure("file-not-found", "Playlist file was not found.");
        }

        if (!SupportedExtensions.Contains(file.Extension))
        {
            return Failure("unsupported-file-type", "Only .m3u and .m3u8 playlists are supported.");
        }

        if (file.Length > options.MaxPlaylistBytes)
        {
            return Failure("playlist-too-large", $"Playlist exceeds the configured {options.MaxPlaylistBytes:N0} byte limit.");
        }

        var source = new PlaylistSource
        {
            Id = StableId.Create("file", file.FullName),
            DisplayName = Path.GetFileNameWithoutExtension(file.Name),
            Kind = PlaylistSourceKind.LocalFile,
            LocalPath = file.FullName
        };

        await using FileStream stream = file.OpenRead();
        return await parser.ParseAsync(stream, source, cancellationToken).ConfigureAwait(false);
    }

    public async Task<PlaylistImportResult> ImportUrlAsync(string url, CancellationToken cancellationToken)
    {
        if (!SensitiveUri.TryCreate(url, out SensitiveUri? remoteUrl, out string? error))
        {
            return Failure("invalid-playlist-url", $"Playlist URL is invalid: {error}");
        }

        SensitiveUri playlistUri = remoteUrl ?? throw new InvalidOperationException("Playlist URI unexpectedly missing after successful validation.");

        if (playlistUri.Uri.Scheme is not "http" and not "https")
        {
            return Failure("unsupported-playlist-url", "Remote playlists must use http:// or https:// URLs.");
        }

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(options.RemoteTimeout);

            using var request = new HttpRequestMessage(HttpMethod.Get, playlistUri.Uri);
            request.Headers.UserAgent.ParseAdd("IptvViewer/1.0");

            using HttpResponseMessage response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                timeoutCts.Token).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return Failure("remote-playlist-unavailable", $"Remote playlist returned {(int)response.StatusCode} {response.ReasonPhrase}.");
            }

            if (response.Content.Headers.ContentLength is long contentLength && contentLength > options.MaxPlaylistBytes)
            {
                return Failure("playlist-too-large", $"Remote playlist exceeds the configured {options.MaxPlaylistBytes:N0} byte limit.");
            }

            await using Stream remoteStream = await response.Content.ReadAsStreamAsync(timeoutCts.Token).ConfigureAwait(false);
            await using var bounded = new MemoryStream();
            await CopyWithLimitAsync(remoteStream, bounded, options.MaxPlaylistBytes, timeoutCts.Token).ConfigureAwait(false);
            bounded.Position = 0;

            var source = new PlaylistSource
            {
                Id = StableId.Create("remote", playlistUri.Host),
                DisplayName = playlistUri.Host,
                Kind = PlaylistSourceKind.RemoteUrl,
                RemoteUrl = playlistUri
            };

            return await parser.ParseAsync(bounded, source, timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Failure("remote-playlist-timeout", "Remote playlist request timed out.");
        }
        catch (InvalidDataException ex)
        {
            return Failure("playlist-too-large", SensitiveTextRedactor.RedactText(ex.Message));
        }
        catch (HttpRequestException ex)
        {
            return Failure("remote-playlist-unavailable", SensitiveTextRedactor.RedactText(ex.Message));
        }
        catch (IOException ex)
        {
            return Failure("remote-playlist-read-failed", SensitiveTextRedactor.RedactText(ex.Message));
        }
    }

    private static async Task CopyWithLimitAsync(Stream source, Stream destination, long maxBytes, CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[64 * 1024];
        long totalBytes = 0;
        int read;

        while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            totalBytes += read;
            if (totalBytes > maxBytes)
            {
                throw new InvalidDataException($"Playlist exceeds the configured {maxBytes:N0} byte limit.");
            }

            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }
    }

    private static PlaylistImportResult Failure(string code, string message)
    {
        return new PlaylistImportResult(
            [],
            [new PlaylistImportIssue(ImportIssueSeverity.Error, code, message)]);
    }
}
