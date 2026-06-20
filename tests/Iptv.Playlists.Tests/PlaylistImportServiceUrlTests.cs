using System.Net;
using System.Text;
using Iptv.Core.PlaylistImport;

namespace Iptv.Playlists.Tests;

public sealed class PlaylistImportServiceUrlTests
{
    [Fact]
    public async Task ImportUrlAsync_ImportsRemoteM3uPlaylist()
    {
        const string playlist = """
            #EXTM3U
            #EXTINF:-1 tvg-name="Remote News" group-title="News",Remote News
            https://stream.example/live/news.m3u8?token=private-token
            """;
        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(playlist, Encoding.UTF8, "audio/x-mpegurl")
            }));
        var service = new PlaylistImportService(new M3uPlaylistParser(), httpClient);

        PlaylistImportResult result = await service.ImportUrlAsync(
            "https://playlist.example/user-playlist.m3u8?token=playlist-secret",
            CancellationToken.None);

        Assert.Single(result.Channels);
        Assert.Equal("Remote News", result.Channels[0].DisplayName);
        Assert.Equal("News", result.Channels[0].GroupTitle);
        Assert.DoesNotContain("private-token", result.Channels[0].StreamUrl.ToString());
        Assert.DoesNotContain(result.Issues, issue => issue.Severity == ImportIssueSeverity.Error);
    }

    [Fact]
    public async Task ImportUrlAsync_ReturnsRedactedNetworkFailure()
    {
        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ =>
            throw new HttpRequestException("Failed for https://playlist.example/path?token=abc123&username=bob")));
        var service = new PlaylistImportService(new M3uPlaylistParser(), httpClient);

        PlaylistImportResult result = await service.ImportUrlAsync(
            "https://playlist.example/user-playlist.m3u8?token=playlist-secret",
            CancellationToken.None);

        PlaylistImportIssue issue = Assert.Single(result.Issues);
        Assert.Equal("remote-playlist-unavailable", issue.Code);
        Assert.DoesNotContain("abc123", issue.Message);
        Assert.DoesNotContain("bob", issue.Message);
        Assert.Contains("token=REDACTED", issue.Message);
    }

    [Fact]
    public async Task ImportUrlAsync_ReturnsUnavailableForHttpFailure()
    {
        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.Forbidden)
            {
                ReasonPhrase = "Forbidden"
            }));
        var service = new PlaylistImportService(new M3uPlaylistParser(), httpClient);

        PlaylistImportResult result = await service.ImportUrlAsync(
            "https://playlist.example/user-playlist.m3u8",
            CancellationToken.None);

        PlaylistImportIssue issue = Assert.Single(result.Issues);
        Assert.Equal("remote-playlist-unavailable", issue.Code);
        Assert.Contains("403", issue.Message);
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> handle;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handle)
        {
            this.handle = handle;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(handle(request));
        }
    }
}
