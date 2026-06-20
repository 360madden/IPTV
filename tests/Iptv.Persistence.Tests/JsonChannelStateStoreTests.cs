namespace Iptv.Persistence.Tests;

public sealed class JsonChannelStateStoreTests
{
    [Fact]
    public async Task SaveAndLoadFavoritesAsync_RoundTripsDistinctIds()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), $"iptv-state-{Guid.NewGuid():N}");
        try
        {
            var store = new JsonChannelStateStore(tempDirectory);
            Guid first = Guid.CreateVersion7();
            Guid second = Guid.CreateVersion7();

            await store.SaveFavoritesAsync([first, second, first], CancellationToken.None);
            IReadOnlySet<Guid> loaded = await store.LoadFavoritesAsync(CancellationToken.None);

            Assert.Equal(2, loaded.Count);
            Assert.Contains(first, loaded);
            Assert.Contains(second, loaded);
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
    public async Task SaveAndLoadChannelStatesAsync_RoundTripsOrganizationState()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), $"iptv-state-{Guid.NewGuid():N}");
        try
        {
            var store = new JsonChannelStateStore(tempDirectory);
            Guid favorite = Guid.CreateVersion7();
            Guid hidden = Guid.CreateVersion7();

            await store.SaveChannelStatesAsync(
                [
                    new ChannelUserState { ChannelId = favorite, IsFavorite = true, CustomGroup = "My News" },
                    new ChannelUserState { ChannelId = hidden, IsHidden = true }
                ],
                CancellationToken.None);

            IReadOnlyDictionary<Guid, ChannelUserState> loaded = await store.LoadChannelStatesAsync(CancellationToken.None);

            Assert.Equal(2, loaded.Count);
            Assert.True(loaded[favorite].IsFavorite);
            Assert.Equal("My News", loaded[favorite].CustomGroup);
            Assert.True(loaded[hidden].IsHidden);
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
    public async Task LoadChannelStatesAsync_ReadsLegacyFavoriteDocument()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), $"iptv-state-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(tempDirectory);
            Guid favorite = Guid.CreateVersion7();
            await File.WriteAllTextAsync(
                Path.Combine(tempDirectory, "channel-state.json"),
                $$"""{"favoriteChannelIds":["{{favorite}}"]}""",
                CancellationToken.None);

            var store = new JsonChannelStateStore(tempDirectory);
            IReadOnlyDictionary<Guid, ChannelUserState> loaded = await store.LoadChannelStatesAsync(CancellationToken.None);

            ChannelUserState state = Assert.Single(loaded.Values);
            Assert.Equal(favorite, state.ChannelId);
            Assert.True(state.IsFavorite);
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
