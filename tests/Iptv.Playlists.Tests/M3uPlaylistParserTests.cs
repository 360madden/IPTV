using System.Text;
using Iptv.Core;
using Iptv.Core.PlaylistImport;
using Iptv.Playlists;

namespace Iptv.Playlists.Tests;

public sealed class M3uPlaylistParserTests
{
    [Fact]
    public async Task ParseAsync_ParsesCommonExtInfAttributes()
    {
        const string playlist = """
            #EXTM3U
            #EXTINF:-1 tvg-id="news.us" tvg-name="Example News" tvg-logo="https://img.example/logo.png" group-title="News",Example News HD
            https://stream.example/live/news.m3u8?token=secret
            #EXTINF:-1 group-title="Sports",Example Sports
            https://stream.example/live/sports.ts
            """;

        PlaylistImportResult result = await ParseAsync(playlist);

        Assert.Equal(2, result.Channels.Count);
        Assert.DoesNotContain(result.Issues, issue => issue.Severity == ImportIssueSeverity.Error);
        Assert.Equal("Example News", result.Channels[0].DisplayName);
        Assert.Equal("News", result.Channels[0].GroupTitle);
        Assert.Equal("news.us", result.Channels[0].TvgId);
        Assert.Equal("Sports", result.Channels[1].Category);
    }

    [Fact]
    public async Task ParseAsync_SkipsInvalidUrlsWithoutThrowing()
    {
        const string playlist = """
            #EXTM3U
            #EXTINF:-1 group-title="News",Bad Channel
            not-a-url
            """;

        PlaylistImportResult result = await ParseAsync(playlist);

        Assert.Empty(result.Channels);
        Assert.Contains(result.Issues, issue => issue.Code == "invalid-stream-url");
        Assert.Contains(result.Issues, issue => issue.Code == "empty-playlist");
    }

    [Fact]
    public async Task ParseAsync_DetectsDuplicatesButKeepsAlternateEntry()
    {
        const string playlist = """
            #EXTM3U
            #EXTINF:-1 group-title="News",Example
            https://stream.example/live/news.m3u8
            #EXTINF:-1 group-title="News",Example
            https://stream.example/live/news.m3u8
            """;

        PlaylistImportResult result = await ParseAsync(playlist);

        Assert.Equal(2, result.Channels.Count);
        Assert.Contains(result.Issues, issue => issue.Code == "duplicate-channel");
    }

    [Fact]
    public async Task ImportFileAsync_RejectsUnsupportedExtensions()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"iptv-{Guid.NewGuid():N}.txt");
        try
        {
            await File.WriteAllTextAsync(tempFile, "#EXTM3U", CancellationToken.None);
            var service = new PlaylistImportService(new M3uPlaylistParser());

            PlaylistImportResult result = await service.ImportFileAsync(tempFile, CancellationToken.None);

            Assert.Empty(result.Channels);
            Assert.Contains(result.Issues, issue => issue.Code == "unsupported-file-type");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    private static async Task<PlaylistImportResult> ParseAsync(string playlist)
    {
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(playlist));
        var parser = new M3uPlaylistParser();
        var source = new PlaylistSource
        {
            Id = Guid.CreateVersion7(),
            DisplayName = "test",
            Kind = PlaylistSourceKind.InMemory
        };

        return await parser.ParseAsync(stream, source, CancellationToken.None);
    }
}
