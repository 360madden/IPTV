using System.Text.Json;

namespace Iptv.Persistence;

public sealed class JsonUiPreferencesStore : IUiPreferencesStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string filePath;

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
