namespace Iptv.App.ViewModels;

public sealed record SourceProfileViewModel(string SourceId, string DisplayName, int ChannelCount)
{
    public string DisplayText => $"{DisplayName} ({ChannelCount:N0})";
}
