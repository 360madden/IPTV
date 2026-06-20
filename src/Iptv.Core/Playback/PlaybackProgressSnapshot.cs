using Iptv.Core.Channels;

namespace Iptv.Core.Playback;

public sealed record PlaybackProgressSnapshot(
    Channel? Channel,
    long TimeMilliseconds,
    long LengthMilliseconds,
    float Position,
    DateTimeOffset UpdatedAt)
{
    public int? ProgressPercent
    {
        get
        {
            if (LengthMilliseconds > 0 && TimeMilliseconds >= 0)
            {
                return Math.Clamp((int)Math.Round(TimeMilliseconds * 100d / LengthMilliseconds), 0, 100);
            }

            if (Position is >= 0 and <= 1)
            {
                return Math.Clamp((int)Math.Round(Position * 100d), 0, 100);
            }

            return null;
        }
    }

    public string DisplayText => ProgressPercent is int progress && LengthMilliseconds > 0
        ? $"Playback position: {progress}% · {FormatDuration(TimeMilliseconds)} / {FormatDuration(LengthMilliseconds)}"
        : "Playback position: unavailable for this stream.";

    private static string FormatDuration(long milliseconds)
    {
        if (milliseconds < 0)
        {
            return "--:--";
        }

        TimeSpan value = TimeSpan.FromMilliseconds(milliseconds);
        return value.TotalHours >= 1
            ? value.ToString(@"h\:mm\:ss")
            : value.ToString(@"m\:ss");
    }
}
