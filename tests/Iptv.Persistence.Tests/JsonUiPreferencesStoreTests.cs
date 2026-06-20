namespace Iptv.Persistence.Tests;

public sealed class JsonUiPreferencesStoreTests
{
    [Fact]
    public async Task SaveAndLoadAsync_RoundTripsUiPreferences()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), $"iptv-ui-prefs-{Guid.NewGuid():N}");
        try
        {
            var store = new JsonUiPreferencesStore(tempDirectory);
            var preferences = new UiPreferences
            {
                ShowClockOverlay = true,
                Use24HourClock = true,
                ShowClockSeconds = true,
                ClockOverlayPosition = ClockOverlayPosition.BottomLeft,
                ClockOverlaySize = ClockOverlaySize.Large,
                ClockOverlayOpacity = 0.7,
                AutoHideFullscreenControls = false,
                FullscreenMonitorIndex = 2
            };

            await store.SaveAsync(preferences, CancellationToken.None);
            UiPreferences loaded = await store.LoadAsync(CancellationToken.None);

            Assert.True(loaded.ShowClockOverlay);
            Assert.True(loaded.Use24HourClock);
            Assert.True(loaded.ShowClockSeconds);
            Assert.Equal(ClockOverlayPosition.BottomLeft, loaded.ClockOverlayPosition);
            Assert.Equal(ClockOverlaySize.Large, loaded.ClockOverlaySize);
            Assert.Equal(0.7, loaded.ClockOverlayOpacity);
            Assert.False(loaded.AutoHideFullscreenControls);
            Assert.Equal(2, loaded.FullscreenMonitorIndex);
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task LoadAsync_ReturnsDefaultsForMissingFile()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), $"iptv-ui-prefs-{Guid.NewGuid():N}");
        try
        {
            var store = new JsonUiPreferencesStore(tempDirectory);

            UiPreferences loaded = await store.LoadAsync(CancellationToken.None);

            Assert.False(loaded.ShowClockOverlay);
            Assert.False(loaded.Use24HourClock);
            Assert.False(loaded.ShowClockSeconds);
            Assert.Equal(ClockOverlayPosition.TopRight, loaded.ClockOverlayPosition);
            Assert.Equal(ClockOverlaySize.Normal, loaded.ClockOverlaySize);
            Assert.Equal(UiPreferences.DefaultClockOverlayOpacity, loaded.ClockOverlayOpacity);
            Assert.True(loaded.AutoHideFullscreenControls);
            Assert.Equal(-1, loaded.FullscreenMonitorIndex);
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task LoadAsync_AppliesDefaultsForOlderPreferenceFiles()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), $"iptv-ui-prefs-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(tempDirectory);
            string filePath = Path.Combine(tempDirectory, "ui-preferences.json");
            await File.WriteAllTextAsync(
                filePath,
                """
                {
                  "showClockOverlay": true,
                  "use24HourClock": true
                }
                """);
            var store = new JsonUiPreferencesStore(tempDirectory);

            UiPreferences loaded = await store.LoadAsync(CancellationToken.None);

            Assert.True(loaded.ShowClockOverlay);
            Assert.True(loaded.Use24HourClock);
            Assert.False(loaded.ShowClockSeconds);
            Assert.Equal(ClockOverlayPosition.TopRight, loaded.ClockOverlayPosition);
            Assert.Equal(ClockOverlaySize.Normal, loaded.ClockOverlaySize);
            Assert.Equal(UiPreferences.DefaultClockOverlayOpacity, loaded.ClockOverlayOpacity);
            Assert.True(loaded.AutoHideFullscreenControls);
            Assert.Equal(-1, loaded.FullscreenMonitorIndex);
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }
}
