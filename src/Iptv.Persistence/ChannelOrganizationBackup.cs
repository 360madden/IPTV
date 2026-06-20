namespace Iptv.Persistence;

public sealed record ChannelOrganizationBackup
{
    public int Version { get; init; } = 1;

    public DateTimeOffset ExportedAt { get; init; } = DateTimeOffset.UtcNow;

    public ChannelOrganizationPreferences Preferences { get; init; } = new();

    public ChannelUserState[] ChannelStates { get; init; } = [];
}
