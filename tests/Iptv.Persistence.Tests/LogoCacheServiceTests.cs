using System.Net;
using System.Net.Http.Headers;
using Iptv.Persistence.Logos;

namespace Iptv.Persistence.Tests;

public sealed class LogoCacheServiceTests
{
    [Fact]
    public async Task CacheLogoAsync_DownloadsSmallImageToHashedPath()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), $"iptv-logo-cache-{Guid.NewGuid():N}");
        try
        {
            var service = new LogoCacheService(tempDirectory);
            using var client = new HttpClient(new StubHttpMessageHandler(_ =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent([1, 2, 3, 4])
                };
                response.Content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
                return response;
            }));

            LogoCacheResult result = await service.CacheLogoAsync(
                "https://cdn.example.test/logo.png?token=secret",
                client,
                CancellationToken.None);

            Assert.True(result.Success, result.Message);
            Assert.NotNull(result.FilePath);
            Assert.True(File.Exists(result.FilePath));
            Assert.EndsWith(".png", result.FilePath, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("secret", Path.GetFileName(result.FilePath));
            Assert.Equal(result.FilePath, service.TryGetCachedLogoPath("https://cdn.example.test/logo.png?token=secret"));
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
    public async Task CacheLogoAsync_RejectsOversizedResponse()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), $"iptv-logo-cache-{Guid.NewGuid():N}");
        try
        {
            var service = new LogoCacheService(tempDirectory, maxLogoBytes: 2);
            using var client = new HttpClient(new StubHttpMessageHandler(_ =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent([1, 2, 3])
                };
                response.Content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
                return response;
            }));

            LogoCacheResult result = await service.CacheLogoAsync(
                "https://cdn.example.test/logo.png",
                client,
                CancellationToken.None);

            Assert.False(result.Success);
            Assert.Null(result.FilePath);
            Assert.False(Directory.Exists(tempDirectory) && Directory.EnumerateFiles(tempDirectory).Any());
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
    public void TryGetCachedLogoPath_ReturnsNullForUnsupportedScheme()
    {
        var service = new LogoCacheService(Path.Combine(Path.GetTempPath(), $"iptv-logo-cache-{Guid.NewGuid():N}"));

        Assert.Null(service.TryGetCachedLogoPath("file:///c:/private/logo.png"));
    }

    [Fact]
    public async Task TrimAsync_RemovesOldestFilesUntilUnderLimit()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), $"iptv-logo-cache-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(tempDirectory);
            string oldPath = Path.Combine(tempDirectory, "old.png");
            string newPath = Path.Combine(tempDirectory, "new.png");
            await File.WriteAllBytesAsync(oldPath, [1, 2, 3]);
            await File.WriteAllBytesAsync(newPath, [4, 5, 6]);
            File.SetLastAccessTimeUtc(oldPath, DateTime.UtcNow.AddDays(-2));
            File.SetLastAccessTimeUtc(newPath, DateTime.UtcNow);
            var service = new LogoCacheService(tempDirectory);

            int removed = await service.TrimAsync(3, CancellationToken.None);
            LogoCacheStatistics statistics = service.GetStatistics();

            Assert.Equal(1, removed);
            Assert.False(File.Exists(oldPath));
            Assert.True(File.Exists(newPath));
            Assert.Equal(1, statistics.FileCount);
            Assert.Equal(3, statistics.TotalBytes);
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
    public async Task ClearAsync_RemovesCacheFiles()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), $"iptv-logo-cache-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(tempDirectory);
            await File.WriteAllBytesAsync(Path.Combine(tempDirectory, "logo.png"), [1, 2, 3]);
            var service = new LogoCacheService(tempDirectory);

            int removed = await service.ClearAsync(CancellationToken.None);

            Assert.Equal(1, removed);
            Assert.Equal(0, service.GetStatistics().FileCount);
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
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
