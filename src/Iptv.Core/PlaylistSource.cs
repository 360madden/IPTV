namespace Iptv.Core;

public sealed record PlaylistSource
{
    public required Guid Id { get; init; }

    public required string DisplayName { get; init; }

    public PlaylistSourceKind Kind { get; init; }

    public SensitiveUri? RemoteUrl { get; init; }

    public string? LocalPath { get; init; }
}
