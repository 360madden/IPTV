namespace Iptv.Core.Playback;

public enum PlaybackStatus
{
    Idle = 0,
    Loading = 1,
    Buffering = 2,
    Playing = 3,
    Paused = 4,
    Retrying = 5,
    Failed = 6,
    Unsupported = 7,
    TimedOut = 8,
    Stopped = 9
}
