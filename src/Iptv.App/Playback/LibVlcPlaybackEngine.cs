using System.Windows.Threading;
using Iptv.Core.Channels;
using Iptv.Core.Playback;
using Iptv.Playback;
using LibVLCSharp.Shared;
using LibVLCSharp.WPF;

namespace Iptv.App.Playback;

public sealed class LibVlcPlaybackEngine : PlaybackEngineBase
{
    private readonly LibVLC libVlc;
    private readonly MediaPlayer mediaPlayer;
    private readonly Dispatcher dispatcher;
    private Media? currentMedia;
    private Channel? currentChannel;
    private BufferingPreset bufferingPreset = BufferingPreset.Balanced;
    private int playAttemptVersion;
    private bool disposed;

    public LibVlcPlaybackEngine(VideoView videoView)
    {
        ArgumentNullException.ThrowIfNull(videoView);

        dispatcher = videoView.Dispatcher;
        LibVLCSharp.Shared.Core.Initialize();
        libVlc = new LibVLC(
            "--no-video-title-show",
            "--network-caching=1200",
            "--file-caching=800",
            "--live-caching=1200",
            "--avcodec-hw=any");
        mediaPlayer = new MediaPlayer(libVlc)
        {
            EnableHardwareDecoding = true,
            Volume = 80
        };

        mediaPlayer.Playing += (_, _) => PublishOnUi(PlaybackStatus.Playing, currentChannel, "Playing.");
        mediaPlayer.Paused += (_, _) => PublishOnUi(PlaybackStatus.Paused, currentChannel, "Paused.");
        mediaPlayer.Stopped += (_, _) => PublishOnUi(PlaybackStatus.Stopped, currentChannel, "Stopped.");
        mediaPlayer.Buffering += (_, _) => PublishOnUi(PlaybackStatus.Buffering, currentChannel, "Buffering...");
        mediaPlayer.EndReached += (_, _) => PublishOnUi(PlaybackStatus.Stopped, currentChannel, "Stream ended.");
        mediaPlayer.EncounteredError += (_, _) => PublishOnUi(PlaybackStatus.Failed, currentChannel, "Playback failed. Try another stream or retry later.");

        videoView.MediaPlayer = mediaPlayer;
    }

    public override Task PlayAsync(Channel channel, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(channel);
        cancellationToken.ThrowIfCancellationRequested();

        currentChannel = channel;
        int attempt = Interlocked.Increment(ref playAttemptVersion);
        Publish(PlaybackStatus.Loading, channel, "Opening stream...");

        currentMedia?.Dispose();
        currentMedia = new Media(libVlc, channel.StreamUrl.Uri);
        (int networkCaching, int liveCaching) = GetCaching(bufferingPreset);
        currentMedia.AddOption($":network-caching={networkCaching}");
        currentMedia.AddOption($":live-caching={liveCaching}");
        currentMedia.AddOption(":clock-jitter=0");
        currentMedia.AddOption(":drop-late-frames");
        currentMedia.AddOption(":skip-frames");

        bool started = mediaPlayer.Play(currentMedia);
        if (!started)
        {
            Publish(PlaybackStatus.Failed, channel, "Playback engine rejected the stream.");
        }
        else
        {
            _ = PublishStartupWatchdogAsync(channel, attempt);
        }

        return Task.CompletedTask;
    }

    public override Task PauseAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        if (mediaPlayer.IsPlaying)
        {
            mediaPlayer.Pause();
        }

        return Task.CompletedTask;
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        mediaPlayer.Stop();
        Interlocked.Increment(ref playAttemptVersion);
        Publish(PlaybackStatus.Stopped, currentChannel, "Stopped.");
        return Task.CompletedTask;
    }

    public override Task SetVolumeAsync(int volume, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        mediaPlayer.Volume = Math.Clamp(volume, 0, 100);
        return Task.CompletedTask;
    }

    public override Task SetBufferingPresetAsync(BufferingPreset preset, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        bufferingPreset = preset;
        Publish(PlaybackStatus.Idle, currentChannel, $"Buffering preset set to {preset}.");
        return Task.CompletedTask;
    }

    public override ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return ValueTask.CompletedTask;
        }

        disposed = true;
        mediaPlayer.Stop();
        currentMedia?.Dispose();
        mediaPlayer.Dispose();
        libVlc.Dispose();
        return ValueTask.CompletedTask;
    }

    private async Task PublishStartupWatchdogAsync(Channel channel, int attempt)
    {
        await Task.Delay(TimeSpan.FromSeconds(20)).ConfigureAwait(false);

        if (disposed || attempt != Volatile.Read(ref playAttemptVersion))
        {
            return;
        }

        PlaybackStatus status = CurrentState.Status;
        if (status is PlaybackStatus.Loading or PlaybackStatus.Buffering)
        {
            PublishOnUi(
                PlaybackStatus.TimedOut,
                channel,
                "Still waiting for the stream. The provider may be slow, offline, geo-blocked, or unsupported. You can retry or choose another channel.");
        }
    }

    private static (int NetworkCaching, int LiveCaching) GetCaching(BufferingPreset preset)
    {
        return preset switch
        {
            BufferingPreset.LowLatency => (500, 500),
            BufferingPreset.PoorNetwork => (3000, 3000),
            _ => (1200, 1200)
        };
    }

    private void PublishOnUi(PlaybackStatus status, Channel? channel, string message)
    {
        if (dispatcher.CheckAccess())
        {
            Publish(status, channel, message);
            return;
        }

        dispatcher.BeginInvoke(() => Publish(status, channel, message));
    }
}
