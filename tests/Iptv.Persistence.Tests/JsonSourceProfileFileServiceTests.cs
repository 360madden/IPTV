using Iptv.Core.Playback;
using Iptv.Persistence.SourceProfiles;

namespace Iptv.Persistence.Tests;

public sealed class JsonSourceProfileFileServiceTests
{
    [Fact]
    public async Task ExportAndImportAsync_RoundTripsProfiles()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), $"iptv-source-profiles-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(tempDirectory);
            string path = Path.Combine(tempDirectory, "profiles.json");
            var service = new JsonSourceProfileFileService();
            var export = new SourceProfileExport
            {
                SourceProfileNames = new Dictionary<string, string>
                {
                    ["source-a"] = "Provider A"
                },
                SourcePlaybackProfiles = new Dictionary<string, ProviderPlaybackProfile>
                {
                    ["source-a"] = new() { RetryCount = 2, BufferingPreset = BufferingPreset.LowLatency }
                }
            };

            await service.ExportAsync(path, export, CancellationToken.None);
            SourceProfileExport imported = await service.ImportAsync(path, CancellationToken.None);

            Assert.Equal("Provider A", imported.SourceProfileNames["source-a"]);
            Assert.Equal(2, imported.SourcePlaybackProfiles["source-a"].RetryCount);
            Assert.Equal(BufferingPreset.LowLatency, imported.SourcePlaybackProfiles["source-a"].BufferingPreset);
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
    public async Task ImportAsync_RejectsUnsupportedVersion()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), $"iptv-source-profiles-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(tempDirectory);
            string path = Path.Combine(tempDirectory, "profiles.json");
            await File.WriteAllTextAsync(path, """{"version":99}""");
            var service = new JsonSourceProfileFileService();

            await Assert.ThrowsAsync<InvalidDataException>(() => service.ImportAsync(path, CancellationToken.None));
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
    public async Task ImportAsync_RejectsOversizedFile()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), $"iptv-source-profiles-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(tempDirectory);
            string path = Path.Combine(tempDirectory, "profiles.json");
            await File.WriteAllBytesAsync(path, new byte[(2 * 1024 * 1024) + 1]);
            var service = new JsonSourceProfileFileService();

            await Assert.ThrowsAsync<InvalidDataException>(() => service.ImportAsync(path, CancellationToken.None));
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
