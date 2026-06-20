using System.Text.Json;

namespace Iptv.Persistence;

public sealed class JsonChannelStateStore : IChannelStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string filePath;

    public JsonChannelStateStore(string? appDataDirectory = null)
    {
        string root = appDataDirectory ??
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "IptvViewer");
        Directory.CreateDirectory(root);
        filePath = Path.Combine(root, "channel-state.json");
    }

    public async Task<IReadOnlySet<Guid>> LoadFavoritesAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath))
        {
            return new HashSet<Guid>();
        }

        await using FileStream stream = File.OpenRead(filePath);
        var state = await JsonSerializer.DeserializeAsync<ChannelStateDocument>(stream, JsonOptions, cancellationToken)
            .ConfigureAwait(false);

        return state?.FavoriteChannelIds.ToHashSet() ?? new HashSet<Guid>();
    }

    public async Task SaveFavoritesAsync(IEnumerable<Guid> channelIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(channelIds);

        var state = new ChannelStateDocument(channelIds.Distinct().Order().ToArray());
        string directory = Path.GetDirectoryName(filePath) ?? ".";
        Directory.CreateDirectory(directory);

        string tempPath = $"{filePath}.tmp";
        await using (FileStream stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, state, JsonOptions, cancellationToken).ConfigureAwait(false);
        }

        File.Move(tempPath, filePath, overwrite: true);
    }

    private sealed record ChannelStateDocument(Guid[] FavoriteChannelIds);
}
