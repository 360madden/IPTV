namespace Iptv.Persistence.Tests;

public sealed class JsonUiPreferencesStoreTests
{
    [Fact]
    public async Task SaveAndLoadAsync_RoundTripsClockPreferences()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), $"iptv-ui-prefs-{Guid.NewGuid():N}");
        try
        {
            var store = new JsonUiPreferencesStore(tempDirectory);
            var preferences = new UiPreferences
            {
                ShowClockOverlay = true,
                Use24HourClock = true
            };

            await store.SaveAsync(preferences, CancellationToken.None);
            UiPreferences loaded = await store.LoadAsync(CancellationToken.None);

            Assert.True(loaded.ShowClockOverlay);
            Assert.True(loaded.Use24HourClock);
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
