using Iptv.App.ViewModels;
using Iptv.Core;
using Iptv.Core.Channels;

namespace Iptv.App.Tests;

public sealed class SourceDefaultVisibilityManagerTests
{
    [Fact]
    public void SetRule_NormalizesDeduplicatesAndRemovesEmptyRules()
    {
        var manager = new SourceDefaultVisibilityManager();
        var rules = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["source-a"] = ["Kids"]
        };

        SourceDefaultVisibilityChange hideChange = manager.SetRule(rules, "source-a", "  Premium   Sports ", hidden: true);
        SourceDefaultVisibilityChange duplicateChange = manager.SetRule(rules, "source-a", "premium sports", hidden: true);
        SourceDefaultVisibilityChange showKidsChange = manager.SetRule(rules, "source-a", "kids", hidden: false);
        SourceDefaultVisibilityChange showPremiumChange = manager.SetRule(rules, "source-a", "premium sports", hidden: false);

        Assert.True(hideChange.Changed);
        Assert.False(duplicateChange.Changed);
        Assert.True(showKidsChange.Changed);
        Assert.True(showPremiumChange.Changed);
        Assert.False(rules.ContainsKey("source-a"));
    }

    [Fact]
    public void IsHiddenByDefault_MatchesSourceAndOriginalGroupOnly()
    {
        var manager = new SourceDefaultVisibilityManager();
        Guid sourceId = Guid.CreateVersion7();
        var rules = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            [sourceId.ToString()] = ["Kids"]
        };

        Channel hidden = CreateChannel(sourceId, "Kids");
        Channel visibleDifferentGroup = CreateChannel(sourceId, "News");
        Channel visibleDifferentSource = CreateChannel(Guid.CreateVersion7(), "Kids");

        Assert.True(manager.IsHiddenByDefault(hidden, rules));
        Assert.False(manager.IsHiddenByDefault(visibleDifferentGroup, rules));
        Assert.False(manager.IsHiddenByDefault(visibleDifferentSource, rules));
    }

    [Fact]
    public void GetGroupOptions_ReturnsAllOptionAndSortedDistinctSourceGroups()
    {
        var manager = new SourceDefaultVisibilityManager();
        Guid sourceId = Guid.CreateVersion7();
        Channel[] channels =
        [
            CreateChannel(sourceId, "Sports"),
            CreateChannel(sourceId, "news"),
            CreateChannel(sourceId, "News"),
            CreateChannel(Guid.CreateVersion7(), "Kids")
        ];

        IReadOnlyList<string> groups = manager.GetGroupOptions(channels, sourceId.ToString(), "All groups");

        Assert.Equal(["All groups", "news", "Sports"], groups);
    }

    private static Channel CreateChannel(Guid sourceId, string group)
    {
        SensitiveUri.TryCreate("https://example.test/stream.m3u8", out SensitiveUri? streamUrl, out string? error);
        Assert.Null(error);
        return new Channel
        {
            Id = Guid.CreateVersion7(),
            SourceId = sourceId,
            RawName = $"{group} Channel",
            DisplayName = $"{group} Channel",
            NormalizedName = group.ToUpperInvariant(),
            StreamUrl = streamUrl!,
            GroupTitle = group
        };
    }
}
