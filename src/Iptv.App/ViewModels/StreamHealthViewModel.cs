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
    public string RecommendationText
    {
        get
        {
            if (LastStatus == PlaybackStatus.TimedOut || FailureCount >= 2)
            {
                return "Recommendation: use PoorNetwork buffer, then Retry; if audio plays with black video, disable hardware decoding.";
            }

            if (SlowEventCount >= 2)
            {
                return "Recommendation: use PoorNetwork buffer for this source.";
            }

            return string.Empty;
        }
    }

    public string DisplayText =>
        string.IsNullOrWhiteSpace(RecommendationText)
            ? $"{ChannelName} [{Host}] — {LastStatus}; ok {SuccessCount:N0}, fail {FailureCount:N0}, slow {SlowEventCount:N0}; {LastUpdatedAt.ToLocalTime():g}"
            : $"{ChannelName} [{Host}] — {LastStatus}; ok {SuccessCount:N0}, fail {FailureCount:N0}, slow {SlowEventCount:N0}; {LastUpdatedAt.ToLocalTime():g}; {RecommendationText}";
}
