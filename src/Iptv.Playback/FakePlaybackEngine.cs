using Iptv.Core.Channels;
using Iptv.Core.Playback;

namespace Iptv.Playback;

public sealed class FakePlaybackEngine : PlaybackEngineBase
{
    public override async Task PlayAsync(Channel channel, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(channel);
        Publish(PlaybackStatus.Loading, channel, "Opening stream...");
        await Task.Delay(25, cancellationToken).ConfigureAwait(false);
        Publish(PlaybackStatus.Playing, channel, "Playing via fake playback engine.");
        PublishProgress(channel, 25_000, 100_000, 0.25f);
    }

    public override Task PauseAsync(CancellationToken cancellationToken)
    {
        Publish(PlaybackStatus.Paused, CurrentState.Channel, "Paused.");
        return Task.CompletedTask;
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        Publish(PlaybackStatus.Stopped, CurrentState.Channel, "Stopped.");
        return Task.CompletedTask;
    }
}
