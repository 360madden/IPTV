using Iptv.Core.Channels;

namespace Iptv.Core.Playback;

public sealed record PlaybackStateSnapshot(
    PlaybackStatus Status,
    Channel? Channel,
    string Message,
    DateTimeOffset UpdatedAt)
{
    public static PlaybackStateSnapshot Idle { get; } = new(
        PlaybackStatus.Idle,
        null,
        "Idle",
        DateTimeOffset.UtcNow);
}
