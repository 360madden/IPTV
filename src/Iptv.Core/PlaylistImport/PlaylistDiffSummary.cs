namespace Iptv.Core.PlaylistImport;

public sealed record PlaylistDiffSummary(
    int PreviousCount,
    int CurrentCount,
    int AddedCount,
    int RemovedCount,
    int UnchangedCount)
{
    public static PlaylistDiffSummary Empty { get; } = new(0, 0, 0, 0, 0);
}
