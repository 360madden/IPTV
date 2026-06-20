using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Iptv.App.Mvvm;
using Iptv.Persistence;

namespace Iptv.App.ViewModels;

public sealed class ClockOverlayViewModel : ObservableObject, IDisposable
{
    private static readonly Brush DarkOverlayBrush = CreateFrozenBrush(Color.FromArgb(0xB0, 0x00, 0x00, 0x00));
    private static readonly Brush BlueOverlayBrush = CreateFrozenBrush(Color.FromArgb(0xD4, 0x08, 0x1F, 0x34));
    private static readonly Brush MinimalOverlayBrush = CreateFrozenBrush(Color.FromArgb(0x86, 0x00, 0x00, 0x00));
    private static readonly Brush DefaultBorderBrush = CreateFrozenBrush(Color.FromArgb(0x55, 0xFF, 0xFF, 0xFF));
    private static readonly Brush AccentBorderBrush = CreateFrozenBrush(Color.FromArgb(0x8A, 0x4E, 0xA7, 0xFF));
    private static readonly Brush MinimalBorderBrush = CreateFrozenBrush(Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF));

    private readonly IUiPreferencesStore preferencesStore;
    private readonly DispatcherTimer timer;
    private readonly SemaphoreSlim saveGate = new(1, 1);
    private bool isVisible;
    private bool use24HourClock;
    private bool showSeconds;
    private ClockOverlayPosition position = ClockOverlayPosition.TopRight;
    private ClockOverlaySize size = ClockOverlaySize.Normal;
    private ClockOverlayBackground background = ClockOverlayBackground.Dark;
    private double overlayOpacity = UiPreferences.DefaultClockOverlayOpacity;
    private bool autoHideFullscreenControls = true;
    private int fullscreenMonitorIndex = -1;
    private string currentTime = string.Empty;
    private bool disposed;

    public ClockOverlayViewModel(IUiPreferencesStore preferencesStore)
    {
        this.preferencesStore = preferencesStore;
        timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        timer.Tick += (_, _) =>
        {
            UpdateTime();
            ScheduleNextTick();
        };

        ToggleClockCommand = new RelayCommand(_ => IsVisible = !IsVisible);
        UpdateTime();
        ScheduleNextTick();
        timer.Start();
    }

    public ICommand ToggleClockCommand { get; }

    public IReadOnlyList<UiSelectionOption<ClockOverlayPosition>> PositionOptions { get; } =
    [
        new(ClockOverlayPosition.TopRight, "Top right"),
        new(ClockOverlayPosition.TopLeft, "Top left"),
        new(ClockOverlayPosition.BottomRight, "Bottom right"),
        new(ClockOverlayPosition.BottomLeft, "Bottom left")
    ];

    public IReadOnlyList<UiSelectionOption<ClockOverlaySize>> SizeOptions { get; } =
    [
        new(ClockOverlaySize.Normal, "Normal"),
        new(ClockOverlaySize.Compact, "Compact"),
        new(ClockOverlaySize.Large, "Large")
    ];

    public IReadOnlyList<UiSelectionOption<ClockOverlayBackground>> BackgroundOptions { get; } =
    [
        new(ClockOverlayBackground.Dark, "Dark"),
        new(ClockOverlayBackground.Blue, "Blue"),
        new(ClockOverlayBackground.Minimal, "Minimal")
    ];

    public ObservableCollection<FullscreenMonitorOption> FullscreenMonitorOptions { get; } =
    [
        new(-1, "Current window monitor")
    ];

    public bool IsVisible
    {
        get => isVisible;
        set
        {
            if (SetProperty(ref isVisible, value))
            {
                _ = SaveAsync();
            }
        }
    }

    public bool Use24HourClock
    {
        get => use24HourClock;
        set
        {
            if (SetProperty(ref use24HourClock, value))
            {
                UpdateTime();
                _ = SaveAsync();
            }
        }
    }

    public bool ShowSeconds
    {
        get => showSeconds;
        set
        {
            if (SetProperty(ref showSeconds, value))
            {
                UpdateTime();
                ScheduleNextTick();
                _ = SaveAsync();
            }
        }
    }

    public ClockOverlayPosition Position
    {
        get => position;
        set
        {
            if (!Enum.IsDefined(value))
            {
                value = ClockOverlayPosition.TopRight;
            }

            if (SetProperty(ref position, value))
            {
                _ = SaveAsync();
            }
        }
    }

    public ClockOverlaySize Size
    {
        get => size;
        set
        {
            if (!Enum.IsDefined(value))
            {
                value = ClockOverlaySize.Normal;
            }

            if (SetProperty(ref size, value))
            {
                OnPropertyChanged(nameof(FontSize));
                OnPropertyChanged(nameof(OverlayPadding));
                OnPropertyChanged(nameof(OverlayCornerRadius));
                _ = SaveAsync();
            }
        }
    }

    public ClockOverlayBackground Background
    {
        get => background;
        set
        {
            if (!Enum.IsDefined(value))
            {
                value = ClockOverlayBackground.Dark;
            }

            if (SetProperty(ref background, value))
            {
                OnPropertyChanged(nameof(OverlayBackgroundBrush));
                OnPropertyChanged(nameof(OverlayBorderBrush));
                _ = SaveAsync();
            }
        }
    }

    public double OverlayOpacity
    {
        get => overlayOpacity;
        set
        {
            double normalized = NormalizeOpacity(value);
            if (SetProperty(ref overlayOpacity, normalized))
            {
                OnPropertyChanged(nameof(OverlayOpacityPercent));
                _ = SaveAsync();
            }
        }
    }

    public bool AutoHideFullscreenControls
    {
        get => autoHideFullscreenControls;
        set
        {
            if (SetProperty(ref autoHideFullscreenControls, value))
            {
                _ = SaveAsync();
            }
        }
    }

    public int FullscreenMonitorIndex
    {
        get => fullscreenMonitorIndex;
        set
        {
            int normalized = Math.Max(-1, value);
            if (SetProperty(ref fullscreenMonitorIndex, normalized))
            {
                _ = SaveAsync();
            }
        }
    }

    public double FontSize => Size switch
    {
        ClockOverlaySize.Compact => 20,
        ClockOverlaySize.Large => 34,
        _ => 26
    };

    public Thickness OverlayPadding => Size switch
    {
        ClockOverlaySize.Compact => new Thickness(12, 6, 12, 6),
        ClockOverlaySize.Large => new Thickness(20, 10, 20, 10),
        _ => new Thickness(16, 8, 16, 8)
    };

    public CornerRadius OverlayCornerRadius => Size switch
    {
        ClockOverlaySize.Compact => new CornerRadius(13),
        ClockOverlaySize.Large => new CornerRadius(20),
        _ => new CornerRadius(16)
    };

    public Brush OverlayBackgroundBrush => Background switch
    {
        ClockOverlayBackground.Blue => BlueOverlayBrush,
        ClockOverlayBackground.Minimal => MinimalOverlayBrush,
        _ => DarkOverlayBrush
    };

    public Brush OverlayBorderBrush => Background switch
    {
        ClockOverlayBackground.Blue => AccentBorderBrush,
        ClockOverlayBackground.Minimal => MinimalBorderBrush,
        _ => DefaultBorderBrush
    };

    public string OverlayOpacityPercent => string.Create(
        CultureInfo.CurrentCulture,
        $"{OverlayOpacity:P0}");

    public string CurrentTime
    {
        get => currentTime;
        private set => SetProperty(ref currentTime, value);
    }

    public void SetMonitorOptions(IEnumerable<FullscreenMonitorOption> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        List<FullscreenMonitorOption> normalizedOptions = options
            .Where(option => option.Index >= -1 && !string.IsNullOrWhiteSpace(option.DisplayName))
            .DistinctBy(option => option.Index)
            .OrderBy(option => option.Index < 0 ? int.MinValue : option.Index)
            .ToList();

        if (!normalizedOptions.Any(option => option.Index == -1))
        {
            normalizedOptions.Insert(0, new FullscreenMonitorOption(-1, "Current window monitor"));
        }

        FullscreenMonitorOptions.Clear();
        foreach (FullscreenMonitorOption option in normalizedOptions)
        {
            FullscreenMonitorOptions.Add(option);
        }

        if (!FullscreenMonitorOptions.Any(option => option.Index == FullscreenMonitorIndex))
        {
            FullscreenMonitorIndex = -1;
        }
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        UiPreferences preferences = await preferencesStore.LoadAsync(cancellationToken).ConfigureAwait(true);
        isVisible = preferences.ShowClockOverlay;
        use24HourClock = preferences.Use24HourClock;
        showSeconds = preferences.ShowClockSeconds;
        position = Enum.IsDefined(preferences.ClockOverlayPosition)
            ? preferences.ClockOverlayPosition
            : ClockOverlayPosition.TopRight;
        size = Enum.IsDefined(preferences.ClockOverlaySize)
            ? preferences.ClockOverlaySize
            : ClockOverlaySize.Normal;
        background = Enum.IsDefined(preferences.ClockOverlayBackground)
            ? preferences.ClockOverlayBackground
            : ClockOverlayBackground.Dark;
        overlayOpacity = NormalizeOpacity(preferences.ClockOverlayOpacity);
        autoHideFullscreenControls = preferences.AutoHideFullscreenControls;
        fullscreenMonitorIndex = Math.Max(-1, preferences.FullscreenMonitorIndex);
        NotifyPreferencesChanged();
        UpdateTime();
        ScheduleNextTick();
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        timer.Stop();
    }

    private async Task SaveAsync()
    {
        if (disposed)
        {
            return;
        }

        bool entered = false;
        try
        {
            await saveGate.WaitAsync().ConfigureAwait(false);
            entered = true;
            await preferencesStore.SaveAsync(
                new UiPreferences
                {
                    ShowClockOverlay = IsVisible,
                    Use24HourClock = Use24HourClock,
                    ShowClockSeconds = ShowSeconds,
                    ClockOverlayPosition = Position,
                    ClockOverlaySize = Size,
                    ClockOverlayBackground = Background,
                    ClockOverlayOpacity = OverlayOpacity,
                    AutoHideFullscreenControls = AutoHideFullscreenControls,
                    FullscreenMonitorIndex = FullscreenMonitorIndex
                },
                CancellationToken.None).ConfigureAwait(false);
        }
        catch (IOException)
        {
            // Preferences are non-critical; keep the UI responsive if storage is temporarily unavailable.
        }
        catch (UnauthorizedAccessException)
        {
            // Preferences are non-critical; keep the UI responsive if storage is not writable.
        }
        finally
        {
            if (entered)
            {
                saveGate.Release();
            }
        }
    }

    private void UpdateTime()
    {
        string format = (Use24HourClock, ShowSeconds) switch
        {
            (true, true) => "HH:mm:ss",
            (true, false) => "HH:mm",
            (false, true) => "h:mm:ss tt",
            _ => "h:mm tt"
        };
        CurrentTime = DateTime.Now.ToString(format, CultureInfo.CurrentCulture);
    }

    private void ScheduleNextTick()
    {
        DateTime now = DateTime.Now;
        DateTime nextBoundary = ShowSeconds
            ? new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second).AddSeconds(1)
            : new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0).AddMinutes(1);
        TimeSpan interval = nextBoundary.AddMilliseconds(ShowSeconds ? 100 : 250) - now;
        timer.Interval = interval < TimeSpan.FromSeconds(1)
            ? TimeSpan.FromSeconds(1)
            : interval;
    }

    private void NotifyPreferencesChanged()
    {
        OnPropertyChanged(nameof(IsVisible));
        OnPropertyChanged(nameof(Use24HourClock));
        OnPropertyChanged(nameof(ShowSeconds));
        OnPropertyChanged(nameof(Position));
        OnPropertyChanged(nameof(Size));
        OnPropertyChanged(nameof(Background));
        OnPropertyChanged(nameof(OverlayOpacity));
        OnPropertyChanged(nameof(OverlayOpacityPercent));
        OnPropertyChanged(nameof(AutoHideFullscreenControls));
        OnPropertyChanged(nameof(FullscreenMonitorIndex));
        OnPropertyChanged(nameof(FontSize));
        OnPropertyChanged(nameof(OverlayPadding));
        OnPropertyChanged(nameof(OverlayCornerRadius));
        OnPropertyChanged(nameof(OverlayBackgroundBrush));
        OnPropertyChanged(nameof(OverlayBorderBrush));
    }

    private static double NormalizeOpacity(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0)
        {
            return UiPreferences.DefaultClockOverlayOpacity;
        }

        return Math.Clamp(Math.Round(value, 2), 0.35, 1.0);
    }

    private static SolidColorBrush CreateFrozenBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
