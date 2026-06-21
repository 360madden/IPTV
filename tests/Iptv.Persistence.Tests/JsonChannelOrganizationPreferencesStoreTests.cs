using Iptv.Core.Channels;
using Iptv.Core.Playback;

namespace Iptv.Persistence.Tests;

public sealed class JsonChannelOrganizationPreferencesStoreTests
{
    [Fact]
    public async Task SaveAndLoadAsync_RoundTripsSortMode()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), $"iptv-org-prefs-{Guid.NewGuid():N}");
        try
        {
            var store = new JsonChannelOrganizationPreferencesStore(tempDirectory);

            await store.SaveAsync(
                new ChannelOrganizationPreferences
                {
                    SortMode = ChannelSortMode.CustomOrder,
                    CustomGroups = ["Sports", "Sports", "  News  "],
                    LargeLibraryMode = true,
                    ChannelViewDensity = ChannelViewDensity.Dense,
                    SourceProfileNames = new Dictionary<string, string>
                    {
                        [" source-a "] = " Provider A "
                    },
                    SourcePlaybackProfiles = new Dictionary<string, ProviderPlaybackProfile>
                    {
                        [" source-a "] = new() { RetryCount = 9, BufferingPreset = BufferingPreset.PoorNetwork }
                    },
                    SourceAppearancePresets = new Dictionary<string, AppAppearancePreset>
                    {
                        [" source-a "] = AppAppearancePreset.LivingRoom,
                        [" source-b "] = AppAppearancePreset.Custom
                    },
                    SourceDefaultHiddenGroups = new Dictionary<string, string[]>
                    {
                        [" source-a "] = [" Kids ", "Kids", "Premium"]
                    },
                    RefreshScheduleEnabled = true,
                    RefreshIntervalMinutes = 3,
                    ParentalPinSalt = " salt ",
                    ParentalPinHash = " hash ",
                    LockedGroups = [" Kids ", "Kids", "Sports"],
                    XmltvGuideUrl = " https://example.com/guide.xml?token=secret ",
                    AutoLoadXmltvOnPlaylistImport = true
                },
                CancellationToken.None);

            ChannelOrganizationPreferences loaded = await store.LoadAsync(CancellationToken.None);

            Assert.Equal(ChannelSortMode.CustomOrder, loaded.SortMode);
            Assert.Equal(["News", "Sports"], loaded.CustomGroups);
            Assert.True(loaded.LargeLibraryMode);
            Assert.Equal(ChannelViewDensity.Dense, loaded.ChannelViewDensity);
            Assert.Equal("Provider A", loaded.SourceProfileNames["source-a"]);
            Assert.Equal(3, loaded.SourcePlaybackProfiles["source-a"].RetryCount);
            Assert.Equal(BufferingPreset.PoorNetwork, loaded.SourcePlaybackProfiles["source-a"].BufferingPreset);
            Assert.Equal(AppAppearancePreset.LivingRoom, loaded.SourceAppearancePresets["source-a"]);
            Assert.False(loaded.SourceAppearancePresets.ContainsKey("source-b"));
            Assert.Equal(["Kids", "Premium"], loaded.SourceDefaultHiddenGroups["source-a"]);
            Assert.True(loaded.RefreshScheduleEnabled);
            Assert.Equal(5, loaded.RefreshIntervalMinutes);
            Assert.Equal("salt", loaded.ParentalPinSalt);
            Assert.Equal("hash", loaded.ParentalPinHash);
            Assert.Equal(["Kids", "Sports"], loaded.LockedGroups);
            Assert.Equal("https://example.com/guide.xml?token=secret", loaded.XmltvGuideUrl);
            Assert.True(loaded.AutoLoadXmltvOnPlaylistImport);
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
    public async Task LoadAsync_ReturnsDefaultForMissingFile()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), $"iptv-org-prefs-{Guid.NewGuid():N}");
        try
        {
            var store = new JsonChannelOrganizationPreferencesStore(tempDirectory);

            ChannelOrganizationPreferences loaded = await store.LoadAsync(CancellationToken.None);

            Assert.Equal(ChannelSortMode.FavoritesFirst, loaded.SortMode);
            Assert.False(loaded.LargeLibraryMode);
            Assert.Equal(ChannelViewDensity.Comfortable, loaded.ChannelViewDensity);
            Assert.Empty(loaded.SourceAppearancePresets);
            Assert.Equal(60, loaded.RefreshIntervalMinutes);
            Assert.Null(loaded.XmltvGuideUrl);
            Assert.False(loaded.AutoLoadXmltvOnPlaylistImport);
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
