namespace Iptv.App.ViewModels;

public sealed record RefreshApprovalChangeViewModel(
    string ChangeKind,
    Guid ChannelId,
    string DisplayName,
    string Detail)
{
    public string DisplayText => $"{ChangeKind}: {DisplayName} — {Detail}";
}
