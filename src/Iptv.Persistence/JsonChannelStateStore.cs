using System.Text.Json;

namespace Iptv.Persistence;

public sealed class JsonChannelStateStore : IChannelStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string filePath;
    private readonly SemaphoreSlim ioGate = new(1, 1);

    public JsonChannelStateStore(string? appDataDirectory = null)
    {
        string root = appDataDirectory ??
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "IptvViewer");
        Directory.CreateDirectory(root);
        filePath = Path.Combine(root, "channel-state.json");
    }

    public async Task<IReadOnlySet<Guid>> LoadFavoritesAsync(CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<Guid, ChannelUserState> states = await LoadChannelStatesAsync(cancellationToken).ConfigureAwait(false);
        return states.Values.Where(state => state.IsFavorite).Select(state => state.ChannelId).ToHashSet();
    }

    public async Task SaveFavoritesAsync(IEnumerable<Guid> channelIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(channelIds);

        IReadOnlyDictionary<Guid, ChannelUserState> existingStates = await LoadChannelStatesAsync(cancellationToken).ConfigureAwait(false);
        HashSet<Guid> favoriteIds = channelIds.Where(id => id != Guid.Empty).ToHashSet();
        HashSet<Guid> stateIds = existingStates.Keys.Concat(favoriteIds).ToHashSet();

        ChannelUserState[] mergedStates = stateIds
            .Select(id =>
            {
                existingStates.TryGetValue(id, out ChannelUserState? existing);
                return new ChannelUserState
                {
                    ChannelId = id,
                    IsFavorite = favoriteIds.Contains(id),
                    IsHidden = existing?.IsHidden ?? false,
                    CustomGroup = NormalizeCustomGroup(existing?.CustomGroup),
                    CustomSortIndex = NormalizeCustomSortIndex(existing?.CustomSortIndex),
                    LastWatchedAt = existing?.LastWatchedAt,
                    ResumeProgressPercent = NormalizeResumeProgress(existing?.ResumeProgressPercent)
                };
            })
            .Where(HasUserState)
            .ToArray();

        await SaveChannelStatesAsync(mergedStates, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyDictionary<Guid, ChannelUserState>> LoadChannelStatesAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath))
        {
            return new Dictionary<Guid, ChannelUserState>();
        }

        await ioGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using FileStream stream = File.OpenRead(filePath);
            var document = await JsonSerializer.DeserializeAsync<ChannelStateDocument>(stream, JsonOptions, cancellationToken)
                .ConfigureAwait(false);

            var states = new Dictionary<Guid, ChannelUserState>();
            foreach (ChannelUserState state in document?.ChannelStates ?? [])
            {
                ChannelUserState normalized = NormalizeState(state);
                if (HasUserState(normalized))
                {
                    states[normalized.ChannelId] = normalized;
                }
            }

            foreach (Guid favoriteId in document?.FavoriteChannelIds ?? [])
            {
                if (favoriteId == Guid.Empty)
                {
                    continue;
                }

                states[favoriteId] = states.TryGetValue(favoriteId, out ChannelUserState? existing)
                    ? existing with { IsFavorite = true }
                    : new ChannelUserState { ChannelId = favoriteId, IsFavorite = true };
            }

            return states;
        }
        finally
        {
            ioGate.Release();
        }
    }

    public async Task SaveChannelStatesAsync(IEnumerable<ChannelUserState> states, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(states);

        ChannelUserState[] normalizedStates = states
            .Select(NormalizeState)
            .Where(HasUserState)
            .GroupBy(state => state.ChannelId)
            .Select(group => MergeStates(group))
            .OrderBy(state => state.ChannelId)
            .ToArray();

        var document = new ChannelStateDocument
        {
            FavoriteChannelIds = normalizedStates
                .Where(state => state.IsFavorite)
                .Select(state => state.ChannelId)
                .ToArray(),
            ChannelStates = normalizedStates
        };

        string directory = Path.GetDirectoryName(filePath) ?? ".";
        Directory.CreateDirectory(directory);

        string tempPath = $"{filePath}.{Guid.NewGuid():N}.tmp";
        await ioGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using (FileStream stream = File.Create(tempPath))
            {
                await JsonSerializer.SerializeAsync(stream, document, JsonOptions, cancellationToken).ConfigureAwait(false);
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

    private static ChannelUserState MergeStates(IEnumerable<ChannelUserState> states)
    {
        ChannelUserState[] snapshot = states.ToArray();
        ChannelUserState first = snapshot[0];
        string? customGroup = snapshot.LastOrDefault(state => !string.IsNullOrWhiteSpace(state.CustomGroup))?.CustomGroup;

        return first with
        {
            IsFavorite = snapshot.Any(state => state.IsFavorite),
            IsHidden = snapshot.Any(state => state.IsHidden),
            CustomGroup = NormalizeCustomGroup(customGroup),
            CustomSortIndex = snapshot.LastOrDefault(state => state.CustomSortIndex.HasValue)?.CustomSortIndex,
            LastWatchedAt = snapshot
                .Where(state => state.LastWatchedAt.HasValue)
                .Select(state => state.LastWatchedAt)
                .DefaultIfEmpty()
                .Max(),
            ResumeProgressPercent = snapshot.LastOrDefault(state => state.ResumeProgressPercent.HasValue)?.ResumeProgressPercent
        };
    }

    private static ChannelUserState NormalizeState(ChannelUserState state)
    {
        return state with
        {
            CustomGroup = NormalizeCustomGroup(state.CustomGroup),
            CustomSortIndex = NormalizeCustomSortIndex(state.CustomSortIndex),
            ResumeProgressPercent = NormalizeResumeProgress(state.ResumeProgressPercent)
        };
    }

    private static bool HasUserState(ChannelUserState state)
    {
        return state.ChannelId != Guid.Empty &&
            (state.IsFavorite ||
                state.IsHidden ||
                !string.IsNullOrWhiteSpace(state.CustomGroup) ||
                state.CustomSortIndex.HasValue ||
                state.LastWatchedAt.HasValue ||
                state.ResumeProgressPercent.HasValue);
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

    private static int? NormalizeCustomSortIndex(int? value)
    {
        return value < 0 ? null : value;
    }

    private static int? NormalizeResumeProgress(int? value)
    {
        return value is null ? null : Math.Clamp(value.Value, 0, 100);
    }

    private sealed class ChannelStateDocument
    {
        public Guid[] FavoriteChannelIds { get; init; } = [];

        public ChannelUserState[] ChannelStates { get; init; } = [];
    }
}
