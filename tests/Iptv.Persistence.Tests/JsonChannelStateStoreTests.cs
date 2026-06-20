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
}
