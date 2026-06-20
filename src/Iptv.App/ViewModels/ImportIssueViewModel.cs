using Iptv.Core.PlaylistImport;

namespace Iptv.App.ViewModels;

public sealed record ImportIssueViewModel(
    string Severity,
    string Code,
    string Message,
    int? LineNumber)
{
    public static ImportIssueViewModel FromIssue(PlaylistImportIssue issue)
    {
        return new ImportIssueViewModel(
            issue.Severity.ToString(),
            issue.Code,
            issue.Message,
            issue.LineNumber);
    }

    public string DisplayText => LineNumber is null
        ? $"{Severity}: {Message}"
        : $"{Severity} line {LineNumber}: {Message}";
}
