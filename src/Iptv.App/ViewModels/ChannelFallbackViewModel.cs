namespace Iptv.App.ViewModels;

public sealed record ChannelFallbackViewModel(
    Guid ChannelId,
    string DisplayName,
    string GroupName,
    string Host,
    bool IsHidden)
{
    public string DisplayText => $"{DisplayName} · {GroupName} · {Host}{(IsHidden ? " · hidden" : string.Empty)}";
}
