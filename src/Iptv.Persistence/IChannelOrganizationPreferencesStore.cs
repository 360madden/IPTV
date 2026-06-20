namespace Iptv.Persistence;

public interface IChannelOrganizationPreferencesStore
{
    Task<ChannelOrganizationPreferences> LoadAsync(CancellationToken cancellationToken);

    Task SaveAsync(ChannelOrganizationPreferences preferences, CancellationToken cancellationToken);
}
