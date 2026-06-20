using Iptv.Core;
using Iptv.Core.Channels;
using Iptv.Core.Playback;

namespace Iptv.Playback.Tests;

public sealed class DisabledPlaybackEngineTests
{
    [Fact]
    public async Task PlayAsync_PublishesUnsupportedInsteadOfFakePlaying()
    {
        var engine = new DisabledPlaybackEngine("disabled for test");
        Channel channel = CreateChannel();

        await engine.PlayAsync(channel, CancellationToken.None);

        Assert.Equal(PlaybackStatus.Unsupported, engine.CurrentState.Status);
        Assert.Equal(channel, engine.CurrentState.Channel);
    }

    private static Channel CreateChannel()
    {
        Assert.True(SensitiveUri.TryCreate("https://stream.example/live.m3u8", out SensitiveUri? uri, out string? error), error);

        return new Channel
        {
            Id = Guid.CreateVersion7(),
            SourceId = Guid.CreateVersion7(),
            RawName = "Example",
            DisplayName = "Example",
            NormalizedName = "example",
            StreamUrl = uri!,
            GroupTitle = "News",
            Category = "News"
        };
    }
}
