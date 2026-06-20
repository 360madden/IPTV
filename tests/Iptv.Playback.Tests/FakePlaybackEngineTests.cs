using Iptv.Core;
using Iptv.Core.Channels;
using Iptv.Core.Playback;
using Iptv.Playback;

namespace Iptv.Playback.Tests;

public sealed class FakePlaybackEngineTests
{
    [Fact]
    public async Task PlayAsync_PublishesPlayingState()
    {
        var engine = new FakePlaybackEngine();
        Channel channel = CreateChannel();
        var states = new List<PlaybackStateSnapshot>();
        engine.StateChanged += (_, state) => states.Add(state);

        await engine.PlayAsync(channel, CancellationToken.None);

        Assert.Equal(PlaybackStatus.Playing, engine.CurrentState.Status);
        Assert.Contains(states, state => state.Status == PlaybackStatus.Loading);
        Assert.Contains(states, state => state.Status == PlaybackStatus.Playing);
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
