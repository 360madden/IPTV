using Iptv.Core.Channels;
using Iptv.Core.Playback;

namespace Iptv.Playback;

public abstract class PlaybackEngineBase : IPlaybackEngine
{
    public event EventHandler<PlaybackStateSnapshot>? StateChanged;

    public event EventHandler<PlaybackProgressSnapshot>? ProgressChanged;

    public PlaybackStateSnapshot CurrentState { get; private set; } = PlaybackStateSnapshot.Idle;

    public abstract Task PlayAsync(Channel channel, CancellationToken cancellationToken);

    public abstract Task PauseAsync(CancellationToken cancellationToken);

    public abstract Task StopAsync(CancellationToken cancellationToken);

    public virtual Task SetVolumeAsync(int volume, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public virtual Task SetBufferingPresetAsync(BufferingPreset preset, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    public virtual Task SeekToProgressAsync(int progressPercent, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    public virtual ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    protected void Publish(PlaybackStatus status, Channel? channel, string message)
    {
        CurrentState = new PlaybackStateSnapshot(status, channel, message, DateTimeOffset.UtcNow);
        StateChanged?.Invoke(this, CurrentState);
    }

    protected void PublishProgress(Channel? channel, long timeMilliseconds, long lengthMilliseconds, float position)
    {
        ProgressChanged?.Invoke(
            this,
            new PlaybackProgressSnapshot(channel, timeMilliseconds, lengthMilliseconds, position, DateTimeOffset.UtcNow));
    }
}
