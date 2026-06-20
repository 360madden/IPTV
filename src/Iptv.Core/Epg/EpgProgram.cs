namespace Iptv.Core.Epg;

public sealed record EpgProgram(
    string ChannelId,
    string Title,
    DateTimeOffset? Start,
    DateTimeOffset? Stop,
    string? Description);
