namespace Iptv.App.ViewModels;

public sealed record VodLibraryItemViewModel(
    Guid ChannelId,
    string Title,
    string GroupName,
    string YearText,
    string ResumeText,
    string PosterStatusText,
    string? PosterPath)
{
    public string DisplayText => $"{Title} · {GroupName} · {YearText} · {ResumeText}";
}
