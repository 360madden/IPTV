using System.Text.Json;
using Iptv.Core.Channels;

namespace Iptv.Persistence;

public sealed class JsonChannelOrganizationPreferencesStore : IChannelOrganizationPreferencesStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string filePath;
    private readonly SemaphoreSlim ioGate = new(1, 1);

    public JsonChannelOrganizationPreferencesStore(string? appDataDirectory = null)
    {
        string root = appDataDirectory ??
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "IptvViewer");
        Directory.CreateDirectory(root);
        filePath = Path.Combine(root, "channel-organization-preferences.json");
    }

    public async Task<ChannelOrganizationPreferences> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath))
        {
            return new ChannelOrganizationPreferences();
        }

        await ioGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using FileStream stream = File.OpenRead(filePath);
            ChannelOrganizationPreferences? preferences = await JsonSerializer
                .DeserializeAsync<ChannelOrganizationPreferences>(stream, JsonOptions, cancellationToken)
                .ConfigureAwait(false);
            return Normalize(preferences);
        }
        catch (JsonException)
        {
            return new ChannelOrganizationPreferences();
        }
        catch (IOException)
        {
            return new ChannelOrganizationPreferences();
        }
        finally
        {
            ioGate.Release();
        }
    }

    public async Task SaveAsync(ChannelOrganizationPreferences preferences, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(preferences);

        ChannelOrganizationPreferences normalized = Normalize(preferences);
        string directory = Path.GetDirectoryName(filePath) ?? ".";
        Directory.CreateDirectory(directory);

        string tempPath = $"{filePath}.{Guid.NewGuid():N}.tmp";
        await ioGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using (FileStream stream = File.Create(tempPath))
            {
                await JsonSerializer.SerializeAsync(stream, normalized, JsonOptions, cancellationToken).ConfigureAwait(false);
            }

            File.Move(tempPath, filePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }

            ioGate.Release();
        }
    }

    private static ChannelOrganizationPreferences Normalize(ChannelOrganizationPreferences? preferences)
    {
        if (preferences is null)
        {
            return new ChannelOrganizationPreferences();
        }

        string[] customGroups = preferences.CustomGroups
            .Select(NormalizeCustomGroup)
            .Where(group => group is not null)
            .Select(group => group!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return preferences with
        {
            SortMode = Enum.IsDefined(preferences.SortMode)
                ? preferences.SortMode
                : ChannelSortMode.FavoritesFirst,
            CustomGroups = customGroups
        };
    }

    private static string? NormalizeCustomGroup(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string normalized = string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return normalized.Length == 0 ? null : normalized;
    }
}
