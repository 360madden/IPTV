namespace Iptv.Persistence;

public sealed record ChannelUserState
{
    public required Guid ChannelId { get; init; }

    public bool IsFavorite { get; init; }

    public bool IsHidden { get; init; }

    public string? CustomGroup { get; init; }

    public int? CustomSortIndex { get; init; }

    public DateTimeOffset? LastWatchedAt { get; init; }
}
