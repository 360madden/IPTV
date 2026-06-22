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

public enum ClockOverlayBackground
{
    Dark,
    Blue,
    Minimal
}

public enum RecentPlaylistSourceKind
{
    LocalFile,
    RemoteUrl
}

public enum AppTheme
{
    Dark,
    Light,
    HighContrast
}

public enum AppUiScale
{
    Normal,
    Large,
    Tv
}

public enum AppAppearancePreset
{
    Custom,
    Desktop,
    LivingRoom,
    HighContrast
}

public sealed record RecentPlaylistSourcePreference
{
    public RecentPlaylistSourceKind Kind { get; init; }

    public string DisplayName { get; init; } = string.Empty;

    public string Value { get; init; } = string.Empty;

    public bool IsPinned { get; init; }

    public DateTimeOffset LastUsedAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record UiPreferences
{
    public const double DefaultClockOverlayOpacity = 0.86;

    public bool ShowClockOverlay { get; init; }

    public bool Use24HourClock { get; init; }

    public bool ShowClockSeconds { get; init; }

    public ClockOverlayPosition ClockOverlayPosition { get; init; } = ClockOverlayPosition.TopRight;

    public ClockOverlaySize ClockOverlaySize { get; init; } = ClockOverlaySize.Normal;

    public ClockOverlayBackground ClockOverlayBackground { get; init; } = ClockOverlayBackground.Dark;

    public double ClockOverlayOpacity { get; init; } = DefaultClockOverlayOpacity;

    public bool AutoHideFullscreenControls { get; init; } = true;

    public int FullscreenMonitorIndex { get; init; } = -1;

    public bool IsBasicMode { get; init; } = true;

    public bool FirstRunSetupCompleted { get; init; }

    public int LogoCacheLimitMegabytes { get; init; } = 100;

    public AppTheme AppTheme { get; init; } = AppTheme.Dark;

    public AppUiScale AppUiScale { get; init; } = AppUiScale.Normal;

    public AppAppearancePreset AppearancePreset { get; init; } = AppAppearancePreset.Desktop;

    public RecentPlaylistSourcePreference[] RecentPlaylistSources { get; init; } = [];
}
