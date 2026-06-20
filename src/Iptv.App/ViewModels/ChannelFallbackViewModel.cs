namespace Iptv.App.ViewModels;

public sealed record ChannelFallbackViewModel(
    Guid ChannelId,
    string DisplayName,
    string GroupName,
    string Host,
    bool IsHidden,
    int Score,
    string ScoreReason)
{
    public string DisplayText => $"{DisplayName} · {GroupName} · {Host} · score {Score:N0} · {ScoreReason}{(IsHidden ? " · hidden" : string.Empty)}";
}
