namespace Iptv.Core.PlaylistImport;

public sealed record PlaylistImportSummary(
    int ImportedCount,
    int ValidCount,
    int WarningCount,
    int ErrorCount,
    int DuplicateCount);
