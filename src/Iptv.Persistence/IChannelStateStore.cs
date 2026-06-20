namespace Iptv.Persistence;

public interface IChannelStateStore
{
    Task<IReadOnlySet<Guid>> LoadFavoritesAsync(CancellationToken cancellationToken);

    Task SaveFavoritesAsync(IEnumerable<Guid> channelIds, CancellationToken cancellationToken);
}
