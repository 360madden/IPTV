using Iptv.Core.Diagnostics;
using Iptv.Core.Playback;

namespace Iptv.App.ViewModels;

public sealed class StreamHealthTracker
{
    private const int DefaultMaximumRows = 100;

    private readonly Dictionary<Guid, StreamHealthSnapshot> snapshots = [];

    public int Count => snapshots.Count;

    public string SummaryText
    {
        get
        {
            if (snapshots.Count == 0)
            {
                return "Stream health appears after playback attempts.";
            }

            int failures = snapshots.Values.Sum(snapshot => snapshot.FailureCount);
            int slow = snapshots.Values.Sum(snapshot => snapshot.SlowEventCount);
            return $"Stream health: {snapshots.Count:N0} checked; {failures:N0} failures; {slow:N0} buffering/retry events.";
        }
    }

    public bool Record(PlaybackStateSnapshot state)
    {
        if (state.Channel is null)
        {
            return false;
        }

        snapshots.TryGetValue(state.Channel.Id, out StreamHealthSnapshot? current);
        current ??= new StreamHealthSnapshot(
            state.Channel.Id,
            state.Channel.DisplayName,
            state.Channel.StreamUrl.Host,
            state.Status,
            0,
            0,
            0,
            state.UpdatedAt,
            state.Message);

        int success = current.SuccessCount + (state.Status == PlaybackStatus.Playing ? 1 : 0);
        int failure = current.FailureCount + (state.Status is PlaybackStatus.Failed or PlaybackStatus.Unsupported or PlaybackStatus.TimedOut ? 1 : 0);
        int slow = current.SlowEventCount + (state.Status is PlaybackStatus.Buffering or PlaybackStatus.Retrying ? 1 : 0);
        snapshots[state.Channel.Id] = current with
        {
            LastStatus = state.Status,
            SuccessCount = success,
            FailureCount = failure,
            SlowEventCount = slow,
            LastUpdatedAt = state.UpdatedAt,
            LastMessage = SensitiveTextRedactor.RedactText(state.Message)
        };

        return true;
    }

    public IReadOnlyList<StreamHealthViewModel> CreateRows(int maximumRows = DefaultMaximumRows)
    {
        int rowCount = Math.Max(1, maximumRows);
        return snapshots.Values
            .OrderByDescending(snapshot => snapshot.FailureCount)
            .ThenByDescending(snapshot => snapshot.SlowEventCount)
            .ThenByDescending(snapshot => snapshot.LastUpdatedAt)
            .Take(rowCount)
            .Select(snapshot => snapshot.ToViewModel())
            .ToArray();
    }

    public bool TryGetFallbackScoreImpact(Guid channelId, out int scoreAdjustment, out string statusReason)
    {
        scoreAdjustment = 0;
        statusReason = string.Empty;

        if (!snapshots.TryGetValue(channelId, out StreamHealthSnapshot? snapshot))
        {
            return false;
        }

        scoreAdjustment += Math.Min(30, snapshot.SuccessCount * 10);
        scoreAdjustment -= Math.Min(35, snapshot.FailureCount * 12);
        scoreAdjustment -= Math.Min(15, snapshot.SlowEventCount * 5);
        statusReason = snapshot.LastStatus.ToString();
        return true;
    }

    public void Clear()
    {
        snapshots.Clear();
    }

    private sealed record StreamHealthSnapshot(
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
        public StreamHealthViewModel ToViewModel()
        {
            return new StreamHealthViewModel(
                ChannelId,
                ChannelName,
                Host,
                LastStatus,
                SuccessCount,
                FailureCount,
                SlowEventCount,
                LastUpdatedAt,
                LastMessage);
        }
    }
}
