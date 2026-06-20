namespace Iptv.Core.PlaylistImport;

public sealed record PlaylistImportIssue(
    ImportIssueSeverity Severity,
    string Code,
    string Message,
    int? LineNumber = null);
