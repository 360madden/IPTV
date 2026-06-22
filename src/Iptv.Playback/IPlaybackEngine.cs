using Iptv.Core.Channels;
using Iptv.Core.Playback;

namespace Iptv.Playback;

public interface IPlaybackEngine : IAsyncDisposable
{
    event EventHandler<PlaybackStateSnapshot>? StateChanged;

    event EventHandler<PlaybackProgressSnapshot>? ProgressChanged;

    PlaybackStateSnapshot CurrentState { get; }

    Task PlayAsync(Channel channel, CancellationToken cancellationToken);

    Task PauseAsync(CancellationToken cancellationToken);

    Task StopAsync(CancellationToken cancellationToken);

    Task SetVolumeAsync(int volume, CancellationToken cancellationToken);

    Task SetBufferingPresetAsync(BufferingPreset preset, CancellationToken cancellationToken);

    Task SetHardwareDecodingAsync(bool enabled, CancellationToken cancellationToken);

    Task SeekToProgressAsync(int progressPercent, CancellationToken cancellationToken);
}
