using Iptv.Persistence.SmartGroups;

namespace Iptv.Persistence.Tests;

public sealed class JsonSmartGroupPresetFileServiceTests
{
    [Fact]
    public async Task ExportAndImportAsync_RoundTripsNormalizedPresets()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), $"iptv-smart-groups-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(tempDirectory);
            string path = Path.Combine(tempDirectory, "smart-groups.json");
            var service = new JsonSmartGroupPresetFileService();

            await service.ExportAsync(
                path,
                [
                    new SmartGroupRulePreset
                    {
                        Name = " News ",
                        MatchText = " local   news ",
                        DestinationGroup = "My News",
                        MatchMode = SmartRuleMatchMode.NameStartsWith
                    },
                    new SmartGroupRulePreset
                    {
                        Name = "News",
                        MatchText = "world news",
                        DestinationGroup = "World",
                        MatchMode = SmartRuleMatchMode.Regex
                    }
                ],
                CancellationToken.None);

            IReadOnlyList<SmartGroupRulePreset> imported = await service.ImportAsync(path, CancellationToken.None);

            SmartGroupRulePreset preset = Assert.Single(imported);
            Assert.Equal("News", preset.Name);
            Assert.Equal("world news", preset.MatchText);
            Assert.Equal("World", preset.DestinationGroup);
            Assert.Equal(SmartRuleMatchMode.Regex, preset.MatchMode);
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
        string tempDirectory = Path.Combine(Path.GetTempPath(), $"iptv-smart-groups-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(tempDirectory);
            string path = Path.Combine(tempDirectory, "smart-groups.json");
            await File.WriteAllTextAsync(path, """{"version":99}""", CancellationToken.None);
            var service = new JsonSmartGroupPresetFileService();

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
