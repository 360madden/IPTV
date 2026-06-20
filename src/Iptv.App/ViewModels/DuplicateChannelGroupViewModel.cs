namespace Iptv.App.ViewModels;

public sealed record DuplicateChannelGroupViewModel(
    string Key,
    int Count,
    string DisplayName,
    string GroupText)
{
    public string DisplayText => $"{DisplayName} — {Count:N0} duplicates · {GroupText}";
}
