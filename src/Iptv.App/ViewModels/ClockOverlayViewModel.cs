using System.Globalization;
using System.IO;
using System.Windows.Input;
using System.Windows.Threading;
using Iptv.App.Mvvm;
using Iptv.Persistence;

namespace Iptv.App.ViewModels;

public sealed class ClockOverlayViewModel : ObservableObject, IDisposable
{
    private readonly IUiPreferencesStore preferencesStore;
    private readonly DispatcherTimer timer;
    private readonly SemaphoreSlim saveGate = new(1, 1);
    private bool isVisible;
    private bool use24HourClock;
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

    public string CurrentTime
    {
        get => currentTime;
        private set => SetProperty(ref currentTime, value);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        UiPreferences preferences = await preferencesStore.LoadAsync(cancellationToken).ConfigureAwait(true);
        isVisible = preferences.ShowClockOverlay;
        use24HourClock = preferences.Use24HourClock;
        OnPropertyChanged(nameof(IsVisible));
        OnPropertyChanged(nameof(Use24HourClock));
        UpdateTime();
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
                    Use24HourClock = Use24HourClock
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
        string format = Use24HourClock ? "HH:mm" : "h:mm tt";
        CurrentTime = DateTime.Now.ToString(format, CultureInfo.CurrentCulture);
    }

    private void ScheduleNextTick()
    {
        DateTime now = DateTime.Now;
        DateTime nextMinute = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0).AddMinutes(1);
        TimeSpan interval = nextMinute.AddMilliseconds(250) - now;
        timer.Interval = interval < TimeSpan.FromSeconds(1)
            ? TimeSpan.FromSeconds(1)
            : interval;
    }
}
