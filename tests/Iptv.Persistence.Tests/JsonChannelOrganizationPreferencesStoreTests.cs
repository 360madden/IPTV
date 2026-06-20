using Iptv.Core.Channels;

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
                    }
                },
                CancellationToken.None);

            ChannelOrganizationPreferences loaded = await store.LoadAsync(CancellationToken.None);

            Assert.Equal(ChannelSortMode.CustomOrder, loaded.SortMode);
            Assert.Equal(["News", "Sports"], loaded.CustomGroups);
            Assert.True(loaded.LargeLibraryMode);
            Assert.Equal(ChannelViewDensity.Dense, loaded.ChannelViewDensity);
            Assert.Equal("Provider A", loaded.SourceProfileNames["source-a"]);
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
