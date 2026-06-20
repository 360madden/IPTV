namespace Iptv.Search;

public sealed record ChannelSearchQuery
{
    public string? Text { get; init; }

    public string? Group { get; init; }

    public string? Category { get; init; }

    public bool FavoritesOnly { get; init; }

    public HiddenChannelFilter HiddenFilter { get; init; } = HiddenChannelFilter.VisibleOnly;

    public int Limit { get; init; } = 5000;
}
