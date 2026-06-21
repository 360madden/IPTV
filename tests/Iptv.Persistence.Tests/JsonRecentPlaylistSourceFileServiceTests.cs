using Iptv.Persistence.RecentPlaylists;

namespace Iptv.Persistence.Tests;

public sealed class JsonRecentPlaylistSourceFileServiceTests
{
    [Fact]
    public async Task ExportAndImportAsync_RoundTripsRecentPlaylistSources()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), $"iptv-recent-sources-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(tempDirectory);
            string path = Path.Combine(tempDirectory, "recent-playlists.json");
            var service = new JsonRecentPlaylistSourceFileService();
            var export = new RecentPlaylistSourcesExport
            {
                Sources =
                [
                    new RecentPlaylistSourcePreference
                    {
                        Kind = RecentPlaylistSourceKind.RemoteUrl,
                        DisplayName = " Xumo ",
                        Value = " https://example.test/xumo.m3u ",
                        IsPinned = true,
                        LastUsedAt = DateTimeOffset.Parse("2026-06-21T01:00:00Z")
                    },
                    new RecentPlaylistSourcePreference
                    {
                        Kind = RecentPlaylistSourceKind.RemoteUrl,
                        DisplayName = "Older Duplicate",
                        Value = "https://example.test/xumo.m3u",
                        LastUsedAt = DateTimeOffset.Parse("2026-06-20T01:00:00Z")
                    }
                ]
            };

            await service.ExportAsync(path, export, CancellationToken.None);
            RecentPlaylistSourcesExport imported = await service.ImportAsync(path, CancellationToken.None);

            RecentPlaylistSourcePreference source = Assert.Single(imported.Sources);
            Assert.Equal(RecentPlaylistSourceKind.RemoteUrl, source.Kind);
            Assert.Equal("Xumo", source.DisplayName);
            Assert.Equal("https://example.test/xumo.m3u", source.Value);
            Assert.True(source.IsPinned);
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ImportAsync_RejectsUnsupportedVersion()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), $"iptv-recent-sources-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(tempDirectory);
            string path = Path.Combine(tempDirectory, "recent-playlists.json");
            await File.WriteAllTextAsync(path, """{"version":99}""");
            var service = new JsonRecentPlaylistSourceFileService();

            await Assert.ThrowsAsync<InvalidDataException>(() => service.ImportAsync(path, CancellationToken.None));
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ImportAsync_RejectsOversizedFile()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), $"iptv-recent-sources-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(tempDirectory);
            string path = Path.Combine(tempDirectory, "recent-playlists.json");
            await File.WriteAllBytesAsync(path, new byte[(2 * 1024 * 1024) + 1]);
            var service = new JsonRecentPlaylistSourceFileService();

            await Assert.ThrowsAsync<InvalidDataException>(() => service.ImportAsync(path, CancellationToken.None));
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }
}
