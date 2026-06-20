namespace Iptv.App.ViewModels;

public sealed record PlaylistRefreshConflictViewModel(
    string Kind,
    Guid ChannelId,
    string ChannelName,
    string Detail)
{
    public string DisplayText => $"{Kind}: {ChannelName} — {Detail}";
}
