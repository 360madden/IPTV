namespace Iptv.Persistence;

public sealed record UiPreferences
{
    public bool ShowClockOverlay { get; init; }

    public bool Use24HourClock { get; init; }
}
