using Iptv.App.ViewModels;
using Iptv.Core;
using Iptv.Core.Channels;

namespace Iptv.App.Tests;

public sealed class LibraryHealthAnalyzerTests
{
    [Fact]
    public void BuildMetrics_IncludesImportResourceMetrics()
    {
        Guid sourceId = Guid.CreateVersion7();
        Channel[] channels = [CreateChannel(sourceId)];
        var resourceMetrics = new LibraryHealthResourceMetrics(
            ManagedMemoryBeforeBytes: 1024,
            ManagedMemoryAfterBytes: 4096,
            Gen0Collections: 2,
            Gen1Collections: 1,
            Gen2Collections: 0);

        IReadOnlyList<LibraryHealthMetricViewModel> metrics = LibraryHealthAnalyzer.BuildMetrics(
            channels,
            savedStateIds: [],
            sourceDefaultHiddenGroups: new Dictionary<string, string[]>(),
            epgProgramCount: 0,
            lastImportDuration: TimeSpan.FromMilliseconds(250),
            importSummary: null,
            resourceMetrics: resourceMetrics);

        LibraryHealthMetricViewModel importMemory = Assert.Single(metrics, metric => metric.Name == "Import memory");
        Assert.Equal("4.0 KB", importMemory.Value);
        Assert.Contains("+3.0 KB", importMemory.Detail, StringComparison.Ordinal);
        Assert.Contains("GC 2/1/0", importMemory.Detail, StringComparison.Ordinal);
    }

    private static Channel CreateChannel(Guid sourceId)
    {
        SensitiveUri.TryCreate("https://example.test/stream.m3u8", out SensitiveUri? streamUrl, out string? error);
        Assert.Null(error);
        return new Channel
        {
            Id = Guid.CreateVersion7(),
            SourceId = sourceId,
            RawName = "News",
            DisplayName = "News",
            NormalizedName = "NEWS",
            StreamUrl = streamUrl!,
            GroupTitle = "News"
        };
    }
}
