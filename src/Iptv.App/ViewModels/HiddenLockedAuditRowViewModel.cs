namespace Iptv.App.ViewModels;

public sealed record HiddenLockedAuditRowViewModel(
    string GroupName,
    int TotalCount,
    int HiddenCount,
    bool IsLocked)
{
    public string DisplayText => $"{GroupName}: {TotalCount:N0} total · {HiddenCount:N0} hidden · {(IsLocked ? "locked" : "unlocked")}";
}
