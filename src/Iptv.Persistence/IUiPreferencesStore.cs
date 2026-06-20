namespace Iptv.Persistence;

public interface IUiPreferencesStore
{
    Task<UiPreferences> LoadAsync(CancellationToken cancellationToken);

    Task SaveAsync(UiPreferences preferences, CancellationToken cancellationToken);
}
