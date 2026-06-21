using System.Net;
using Iptv.Core.PlaylistImport;

namespace Iptv.Playlists.Tests;

public sealed class PlaylistImportProgressTests
{
    [Fact]
    public async Task ImportFileAsync_ReportsParseProgress()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"iptv-progress-{Guid.NewGuid():N}.m3u");
        try
        {
            await File.WriteAllTextAsync(
                tempFile,
                """
                #EXTM3U
                #EXTINF:-1,One
                https://example.test/one.m3u8
                """);
            var service = new PlaylistImportService(new M3uPlaylistParser());
            var progress = new CapturingProgress();

            await service.ImportFileAsync(tempFile, CancellationToken.None, progress);

            Assert.Contains(progress.Events, item => item.Stage.Contains("Opening", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(progress.Events, item => item.Stage.Contains("Parsed", StringComparison.OrdinalIgnoreCase) && item.ParsedChannels == 1);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task ImportUrlAsync_ReportsDownloadProgress()
    {
        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    #EXTM3U
                    #EXTINF:-1,One
                    https://example.test/one.m3u8
                    """)
            };
            response.Content.Headers.ContentLength = 60;
            return response;
        }));
        var service = new PlaylistImportService(new M3uPlaylistParser(), httpClient);
        var progress = new CapturingProgress();

        await service.ImportUrlAsync(
            "https://provider.example.test/list.m3u",
            CancellationToken.None,
            progress);

        Assert.Contains(progress.Events, item => item.Stage.Contains("Connecting", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(progress.Events, item => item.Stage.Contains("Downloading", StringComparison.OrdinalIgnoreCase));
    }

    private sealed class CapturingProgress : IProgress<PlaylistImportProgress>
    {
        public List<PlaylistImportProgress> Events { get; } = [];

        public void Report(PlaylistImportProgress value)
        {
            Events.Add(value);
        }
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(responseFactory(request));
        }
    }
}
