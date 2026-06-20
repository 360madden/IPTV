namespace Iptv.App.ViewModels;

public sealed record EpgTimelineRowViewModel(
    string ChannelName,
    string NowTitle,
    string NextTitle,
    string LaterTitle,
    string TimeWindowText);
