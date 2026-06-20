using Iptv.Core.Playback;

namespace Iptv.App.ViewModels;

public sealed record StreamHealthViewModel(
    Guid ChannelId,
    string ChannelName,
    string Host,
    PlaybackStatus LastStatus,
    int SuccessCount,
    int FailureCount,
    int SlowEventCount,
    DateTimeOffset LastUpdatedAt,
    string LastMessage)
{
    public string DisplayText =>
        $"{ChannelName} [{Host}] — {LastStatus}; ok {SuccessCount:N0}, fail {FailureCount:N0}, slow {SlowEventCount:N0}; {LastUpdatedAt.ToLocalTime():g}";
}
