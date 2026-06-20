using Iptv.Core;

namespace Iptv.Core.Channels;

public sealed record Channel
{
    public required Guid Id { get; init; }

    public required Guid SourceId { get; init; }

    public required string RawName { get; init; }

    public required string DisplayName { get; init; }

    public required string NormalizedName { get; init; }

    public required SensitiveUri StreamUrl { get; init; }

    public string GroupTitle { get; init; } = "Ungrouped";

    public string? CustomGroup { get; init; }

    public string EffectiveGroupTitle => string.IsNullOrWhiteSpace(CustomGroup) ? GroupTitle : CustomGroup;

    public string Category { get; init; } = "Other";

    public string? TvgId { get; init; }

    public string? TvgName { get; init; }

    public string? TvgLogo { get; init; }

    public ContentKind ContentKind { get; init; } = ContentKind.LiveTv;

    public bool IsFavorite { get; init; }

    public bool IsHidden { get; init; }

    public DateTimeOffset ImportedAt { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? LastWatchedAt { get; init; }
}
