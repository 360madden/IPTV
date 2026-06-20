using Iptv.Core;
using Iptv.Core.Channels;
using Iptv.Persistence.CustomGroups;

namespace Iptv.Persistence.Tests;

public sealed class CustomGroupCsvServiceTests
{
    [Fact]
    public async Task ExportAndImportAsync_RoundTripsCustomGroupsWithEscaping()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), $"iptv-custom-groups-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(tempDirectory);
            string path = Path.Combine(tempDirectory, "groups.csv");
            var service = new CustomGroupCsvService();
            Channel first = CreateChannel("Movie, One", "Movies", "Custom, Movies", importIndex: 1);
            Channel second = CreateChannel("News \"Two\"", "News", null, importIndex: 0);

            await service.ExportAsync(path, [first, second], CancellationToken.None);

            IReadOnlyList<CustomGroupCsvRow> rows = await service.ImportAsync(path, CancellationToken.None);

            Assert.Equal(2, rows.Count);
            Dictionary<Guid, string?> groups = rows.ToDictionary(row => row.ChannelId, row => row.CustomGroup);
            Assert.Equal("Custom, Movies", groups[first.Id]);
            Assert.Null(groups[second.Id]);
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
    public async Task ImportAsync_UsesLastDuplicateChannelRow()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), $"iptv-custom-groups-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(tempDirectory);
            string path = Path.Combine(tempDirectory, "groups.csv");
            Guid channelId = Guid.CreateVersion7();
            await File.WriteAllTextAsync(
                path,
                $"channelId,displayName,customGroup{Environment.NewLine}{channelId},One,Old{Environment.NewLine}{channelId},One,New",
                CancellationToken.None);

            var service = new CustomGroupCsvService();

            CustomGroupCsvRow row = Assert.Single(await service.ImportAsync(path, CancellationToken.None));
            Assert.Equal(channelId, row.ChannelId);
            Assert.Equal("New", row.CustomGroup);
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }

    private static Channel CreateChannel(string name, string group, string? customGroup, int importIndex)
    {
        Assert.True(SensitiveUri.TryCreate($"https://stream.example/{Uri.EscapeDataString(name)}.m3u8", out SensitiveUri? uri, out string? error), error);
        return new Channel
        {
            Id = Guid.CreateVersion7(),
            SourceId = Guid.CreateVersion7(),
            RawName = name,
            DisplayName = name,
            NormalizedName = ChannelNormalizer.NormalizeForSearch(name),
            StreamUrl = uri!,
            ImportIndex = importIndex,
            GroupTitle = group,
            CustomGroup = customGroup,
            Category = ChannelNormalizer.InferCategory(group, name),
            ContentKind = ContentKind.LiveTv
        };
    }
}
