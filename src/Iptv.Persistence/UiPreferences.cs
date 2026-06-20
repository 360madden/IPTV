namespace Iptv.Persistence;

public enum ClockOverlayPosition
{
    TopRight,
    TopLeft,
    BottomRight,
    BottomLeft
}

public enum ClockOverlaySize
{
    Normal,
    Compact,
    Large
}

public sealed record UiPreferences
{
    public const double DefaultClockOverlayOpacity = 0.86;

    public bool ShowClockOverlay { get; init; }

    public bool Use24HourClock { get; init; }

    public bool ShowClockSeconds { get; init; }

    public ClockOverlayPosition ClockOverlayPosition { get; init; } = ClockOverlayPosition.TopRight;

    public ClockOverlaySize ClockOverlaySize { get; init; } = ClockOverlaySize.Normal;

    public double ClockOverlayOpacity { get; init; } = DefaultClockOverlayOpacity;

    public bool AutoHideFullscreenControls { get; init; } = true;

    public int FullscreenMonitorIndex { get; init; } = -1;
}
