using Iptv.Core.Channels;

namespace Iptv.Core.PlaylistImport;

public sealed record PlaylistImportResult(
    IReadOnlyList<Channel> Channels,
    IReadOnlyList<PlaylistImportIssue> Issues)
{
    public PlaylistImportSummary Summary { get; } = new(
        Channels.Count,
        Channels.Count,
        Issues.Count(issue => issue.Severity == ImportIssueSeverity.Warning),
        Issues.Count(issue => issue.Severity == ImportIssueSeverity.Error),
        Issues.Count(issue => issue.Code.Equals("duplicate-channel", StringComparison.OrdinalIgnoreCase)));
}
