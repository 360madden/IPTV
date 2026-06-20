using Iptv.Core.PlaylistImport;

namespace Iptv.Core.Epg;

public sealed record EpgImportResult(
    IReadOnlyList<EpgChannel> Channels,
    IReadOnlyList<EpgProgram> Programs,
    IReadOnlyList<PlaylistImportIssue> Issues)
{
    public string SummaryText =>
        $"EPG channels {Channels.Count:N0}; programs {Programs.Count:N0}; warnings {Issues.Count(issue => issue.Severity == ImportIssueSeverity.Warning):N0}; errors {Issues.Count(issue => issue.Severity == ImportIssueSeverity.Error):N0}.";
}
