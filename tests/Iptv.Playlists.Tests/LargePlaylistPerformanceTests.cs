using System.Diagnostics;
using System.Text;
using Iptv.Core;
using Iptv.Playlists;

namespace Iptv.Playlists.Tests;

public sealed class LargePlaylistPerformanceTests
{
    [Fact]
    public async Task ParseAsync_HandlesLargePlaylistWithinReasonableTime()
    {
        const int channelCount = 10_000;
        var builder = new StringBuilder("#EXTM3U\r\n", channelCount * 140);
        for (int i = 0; i < channelCount; i++)
        {
            builder.Append("#EXTINF:-1 group-title=\"News\",Channel ");
            builder.Append(i);
            builder.Append("\r\nhttps://stream.example/live/");
            builder.Append(i);
            builder.Append(".m3u8\r\n");
        }

        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(builder.ToString()));
        var parser = new M3uPlaylistParser();
        var source = new PlaylistSource
        {
            Id = Guid.CreateVersion7(),
            DisplayName = "large",
            Kind = PlaylistSourceKind.InMemory
        };

        var stopwatch = Stopwatch.StartNew();
        var result = await parser.ParseAsync(stream, source, CancellationToken.None);
        stopwatch.Stop();

        Assert.Equal(channelCount, result.Channels.Count);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(10), $"Large parse took {stopwatch.Elapsed}.");
    }
}
