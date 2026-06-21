using System.Text.Json;

namespace Iptv.Persistence;

public sealed class JsonUiPreferencesStore : IUiPreferencesStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string filePath;
    private readonly SemaphoreSlim saveGate = new(1, 1);

    public JsonUiPreferencesStore(string? appDataDirectory = null)
    {
        string root = appDataDirectory ??
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "IptvViewer");
        Directory.CreateDirectory(root);
        filePath = Path.Combine(root, "ui-preferences.json");
    }

    public async Task<UiPreferences> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath))
        {
            return new UiPreferences();
        }

        try
        {
            await using FileStream stream = File.OpenRead(filePath);
            UiPreferences? preferences = await JsonSerializer
                .DeserializeAsync<UiPreferences>(stream, JsonOptions, cancellationToken)
                .ConfigureAwait(false);
            return preferences ?? new UiPreferences();
        }
        catch (JsonException)
        {
            return new UiPreferences();
        }
        catch (IOException)
        {
            return new UiPreferences();
        }
    }

    public async Task SaveAsync(UiPreferences preferences, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(preferences);

        await saveGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await SaveUnsafeAsync(preferences, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            saveGate.Release();
        }
    }

    public async Task UpdateAsync(Func<UiPreferences, UiPreferences> update, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(update);

        await saveGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            UiPreferences current = await LoadUnsafeAsync(cancellationToken).ConfigureAwait(false);
            UiPreferences next = update(current) ?? throw new InvalidOperationException("UI preference update returned null.");
            await SaveUnsafeAsync(next, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            saveGate.Release();
        }
    }

    private async Task<UiPreferences> LoadUnsafeAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath))
        {
            return new UiPreferences();
        }

        try
        {
            await using FileStream stream = File.OpenRead(filePath);
            UiPreferences? preferences = await JsonSerializer
                .DeserializeAsync<UiPreferences>(stream, JsonOptions, cancellationToken)
                .ConfigureAwait(false);
            return preferences ?? new UiPreferences();
        }
        catch (JsonException)
        {
            return new UiPreferences();
        }
        catch (IOException)
        {
            return new UiPreferences();
        }
    }

    private async Task SaveUnsafeAsync(UiPreferences preferences, CancellationToken cancellationToken)
    {
        string directory = Path.GetDirectoryName(filePath) ?? ".";
        Directory.CreateDirectory(directory);

        string tempPath = $"{filePath}.{Guid.NewGuid():N}.tmp";
        await using (FileStream stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, preferences, JsonOptions, cancellationToken).ConfigureAwait(false);
        }

        File.Move(tempPath, filePath, overwrite: true);
    }
}
