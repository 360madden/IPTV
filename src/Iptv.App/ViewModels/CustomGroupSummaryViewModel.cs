namespace Iptv.App.ViewModels;

public sealed record CustomGroupSummaryViewModel(string Name, int ChannelCount)
{
    public string DisplayText => $"{Name} ({ChannelCount:N0})";
}
