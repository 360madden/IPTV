using Iptv.Core.Playback;

namespace Iptv.Playback;

public static class PlaybackStartupWatchdogPolicy
{
    public static bool ShouldPromoteAlivePlayback(PlaybackStatus status, bool isPlaybackAlive)
    {
        return isPlaybackAlive && status is PlaybackStatus.Loading or PlaybackStatus.Buffering or PlaybackStatus.TimedOut;
    }

    public static bool ShouldTimeout(PlaybackStatus status, bool isPlaybackAlive)
    {
        return !isPlaybackAlive && status is PlaybackStatus.Loading or PlaybackStatus.Buffering;
    }

    public static bool ShouldClearActivePlayback(PlaybackStatus status)
    {
        return status is PlaybackStatus.Stopped or PlaybackStatus.Failed or PlaybackStatus.Unsupported;
    }
}
