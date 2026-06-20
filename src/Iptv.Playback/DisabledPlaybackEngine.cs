using Iptv.Core.Channels;
using Iptv.Core.Playback;

namespace Iptv.Playback;

public sealed class DisabledPlaybackEngine : PlaybackEngineBase
{
    private readonly string reason;

    public DisabledPlaybackEngine(string reason)
    {
        this.reason = string.IsNullOrWhiteSpace(reason)
            ? "Playback is unavailable."
            : reason;
        Publish(PlaybackStatus.Unsupported, null, this.reason);
    }

    public override Task PlayAsync(Channel channel, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(channel);
        cancellationToken.ThrowIfCancellationRequested();
        Publish(PlaybackStatus.Unsupported, channel, reason);
        return Task.CompletedTask;
    }

    public override Task PauseAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Publish(PlaybackStatus.Unsupported, CurrentState.Channel, reason);
        return Task.CompletedTask;
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Publish(PlaybackStatus.Stopped, CurrentState.Channel, "Playback is disabled; nothing is playing.");
        return Task.CompletedTask;
    }
}
