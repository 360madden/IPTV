namespace Iptv.Persistence;

public interface IChannelStateStore
{
    Task<IReadOnlyDictionary<Guid, ChannelUserState>> LoadChannelStatesAsync(CancellationToken cancellationToken);

    Task SaveChannelStatesAsync(IEnumerable<ChannelUserState> states, CancellationToken cancellationToken);

    Task<IReadOnlySet<Guid>> LoadFavoritesAsync(CancellationToken cancellationToken);

    Task SaveFavoritesAsync(IEnumerable<Guid> channelIds, CancellationToken cancellationToken);
}
