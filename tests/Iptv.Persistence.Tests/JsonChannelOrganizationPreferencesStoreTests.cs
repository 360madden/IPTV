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
                    CustomGroups = ["Sports", "Sports", "  News  "]
                },
                CancellationToken.None);

            ChannelOrganizationPreferences loaded = await store.LoadAsync(CancellationToken.None);

            Assert.Equal(ChannelSortMode.CustomOrder, loaded.SortMode);
            Assert.Equal(["News", "Sports"], loaded.CustomGroups);
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
