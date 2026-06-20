using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using Iptv.App.Mvvm;
using Iptv.App.Services;
using Iptv.Core.Channels;
using Iptv.Core.Diagnostics;
using Iptv.Core.Epg;
using Iptv.Core.Playback;
using Iptv.Core.PlaylistImport;
using Iptv.Epg;
using Iptv.Persistence;
using Iptv.Playback;
using Iptv.Playlists;
using Iptv.Search;

namespace Iptv.App.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private const string AllGroupsOption = "All Groups";
    private const string AllCategoriesOption = "All Categories";
    private const string SourceGroupAssignmentOption = "Source group";
    private const int MaxVisibleChannelResults = 50_000;

    private readonly IPlaylistImportService playlistImportService;
    private readonly IChannelSearchService channelSearchService;
    private readonly IPlaybackEngine playbackEngine;
    private readonly IChannelStateStore channelStateStore;
    private readonly IChannelOrganizationPreferencesStore organizationPreferencesStore;
    private readonly IChannelOrganizationBackupService organizationBackupService;
    private readonly IXmltvImportService xmltvImportService;
    private readonly IPlaylistDialogService dialogService;
    private readonly List<Channel> allChannels = [];
    private readonly CancellationTokenSource shutdownCts = new();
    private CancellationTokenSource? searchCts;
    private Func<CancellationToken, Task<PlaylistImportResult>>? lastPlaylistImport;
    private readonly Dictionary<Guid, ChannelUserState> channelStates = [];
    private readonly HashSet<string> knownCustomGroups = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<Guid> selectedChannelIds = [];

    private Channel? selectedChannel;
    private string searchText = string.Empty;
    private string selectedGroup = AllGroupsOption;
    private string selectedCategory = AllCategoriesOption;
    private string selectedCustomGroupAssignment = SourceGroupAssignmentOption;
    private string newCustomGroupName = string.Empty;
    private bool favoritesOnly;
    private HiddenChannelFilter selectedHiddenFilter = HiddenChannelFilter.VisibleOnly;
    private ChannelSortMode selectedSortMode = ChannelSortMode.FavoritesFirst;
    private string? selectedManagedCustomGroup;
    private string renameCustomGroupName = string.Empty;
    private string selectedBatchGroupAssignment = SourceGroupAssignmentOption;
    private int selectedChannelCount;
    private bool isUpdatingSelectedChannelOrganization;
    private bool isBusy;
    private string statusText = "Import a user-provided M3U/M3U8 playlist to begin.";
    private string playbackStatusText = "Playback idle.";
    private string importSummaryText = "No playlist imported yet.";
    private string refreshDiffText = "Refresh diff unavailable until a playlist is imported.";
    private string epgSummaryText = "No XMLTV guide imported.";
    private string selectedChannelDetails = "Select a channel to view safe details.";
    private BufferingPreset selectedBufferingPreset = BufferingPreset.Balanced;
    private Guid? nowPlayingChannelId;
    private int volume = 80;

    public MainViewModel(
        IPlaylistImportService playlistImportService,
        IChannelSearchService channelSearchService,
        IPlaybackEngine playbackEngine,
        IChannelStateStore channelStateStore,
        IChannelOrganizationPreferencesStore organizationPreferencesStore,
        IChannelOrganizationBackupService organizationBackupService,
        IUiPreferencesStore uiPreferencesStore,
        IXmltvImportService xmltvImportService,
        IPlaylistDialogService dialogService)
    {
        this.playlistImportService = playlistImportService;
        this.channelSearchService = channelSearchService;
        this.playbackEngine = playbackEngine;
        this.channelStateStore = channelStateStore;
        this.organizationPreferencesStore = organizationPreferencesStore;
        this.organizationBackupService = organizationBackupService;
        this.xmltvImportService = xmltvImportService;
        this.dialogService = dialogService;
        Clock = new ClockOverlayViewModel(uiPreferencesStore);

        ImportFileCommand = new AsyncRelayCommand(_ => ImportFileAsync(), _ => !IsBusy);
        ImportUrlCommand = new AsyncRelayCommand(_ => ImportUrlAsync(), _ => !IsBusy);
        LoadSampleCommand = new AsyncRelayCommand(_ => LoadSampleAsync(), _ => !IsBusy);
        RefreshPlaylistCommand = new AsyncRelayCommand(_ => RefreshPlaylistAsync(), _ => !IsBusy && lastPlaylistImport is not null);
        ImportEpgCommand = new AsyncRelayCommand(_ => ImportEpgAsync(), _ => !IsBusy);
        PlaySelectedCommand = new AsyncRelayCommand(_ => PlaySelectedAsync(), _ => SelectedChannel is not null);
        PauseCommand = new AsyncRelayCommand(_ => PauseAsync());
        StopCommand = new AsyncRelayCommand(_ => StopAsync());
        ToggleFavoriteCommand = new RelayCommand(_ => ToggleFavorite(), _ => SelectedChannel is not null);
        ToggleHiddenCommand = new RelayCommand(_ => ToggleHidden(), _ => SelectedChannel is not null);
        ClearCustomGroupCommand = new RelayCommand(_ => ClearCustomGroup(), _ => SelectedChannel is not null);
        AddCustomGroupCommand = new RelayCommand(_ => AddCustomGroup());
        RenameCustomGroupCommand = new RelayCommand(_ => RenameCustomGroup(), _ => SelectedManagedCustomGroup is not null);
        DeleteCustomGroupCommand = new RelayCommand(_ => DeleteCustomGroup(), _ => SelectedManagedCustomGroup is not null);
        MoveChannelUpCommand = new RelayCommand(_ => MoveSelectedChannel(-1), _ => SelectedChannel is not null);
        MoveChannelDownCommand = new RelayCommand(_ => MoveSelectedChannel(1), _ => SelectedChannel is not null);
        BatchFavoriteCommand = new RelayCommand(_ => ApplyBatchUpdate(channel => channel with { IsFavorite = true }, "favorited"), _ => HasBatchSelection);
        BatchHideCommand = new RelayCommand(_ => ApplyBatchUpdate(channel => channel with { IsHidden = true }, "hidden"), _ => HasBatchSelection);
        BatchUnhideCommand = new RelayCommand(_ => ApplyBatchUpdate(channel => channel with { IsHidden = false }, "unhidden"), _ => HasBatchSelection);
        BatchAssignGroupCommand = new RelayCommand(_ => AssignBatchGroup(), _ => HasBatchSelection);
        BatchClearGroupCommand = new RelayCommand(_ => ApplyBatchUpdate(channel => channel with { CustomGroup = null }, "removed from custom groups"), _ => HasBatchSelection);
        ImportOrganizationCommand = new AsyncRelayCommand(_ => ImportOrganizationAsync(), _ => !IsBusy);
        ExportOrganizationCommand = new AsyncRelayCommand(_ => ExportOrganizationAsync(), _ => !IsBusy);
        ClearFiltersCommand = new RelayCommand(_ => ClearFilters());

        playbackEngine.StateChanged += (_, state) => UiDispatcher.Run(() => ApplyPlaybackState(state));
    }

    public RangeObservableCollection<Channel> VisibleChannels { get; } = [];

    public ObservableCollection<string> Groups { get; } = [AllGroupsOption];

    public ObservableCollection<string> Categories { get; } = [AllCategoriesOption];

    public ObservableCollection<string> CustomGroups { get; } = [];

    public ObservableCollection<CustomGroupSummaryViewModel> CustomGroupSummaries { get; } = [];

    public ObservableCollection<string> CustomGroupAssignments { get; } = [SourceGroupAssignmentOption];

    public ObservableCollection<ImportIssueViewModel> RecentImportIssues { get; } = [];

    public ObservableCollection<string> Diagnostics { get; } = [];

    public ClockOverlayViewModel Clock { get; }

    public IReadOnlyList<BufferingPreset> BufferingPresets { get; } =
    [
        BufferingPreset.LowLatency,
        BufferingPreset.Balanced,
        BufferingPreset.PoorNetwork
    ];

    public IReadOnlyList<UiSelectionOption<HiddenChannelFilter>> VisibilityFilterOptions { get; } =
    [
        new(HiddenChannelFilter.VisibleOnly, "Visible channels"),
        new(HiddenChannelFilter.IncludeHidden, "All channels"),
        new(HiddenChannelFilter.HiddenOnly, "Hidden only")
    ];

    public IReadOnlyList<UiSelectionOption<ChannelSortMode>> SortModeOptions { get; } =
    [
        new(ChannelSortMode.FavoritesFirst, "Favorites first"),
        new(ChannelSortMode.PlaylistOrder, "Playlist order"),
        new(ChannelSortMode.NameAscending, "Name A-Z"),
        new(ChannelSortMode.NameDescending, "Name Z-A"),
        new(ChannelSortMode.GroupThenName, "Group then name"),
        new(ChannelSortMode.RecentlyWatched, "Recently watched"),
        new(ChannelSortMode.HiddenLast, "Hidden last"),
        new(ChannelSortMode.CustomOrder, "Custom order")
    ];

    public ICommand ImportFileCommand { get; }

    public ICommand ImportUrlCommand { get; }

    public ICommand LoadSampleCommand { get; }

    public ICommand RefreshPlaylistCommand { get; }

    public ICommand ImportEpgCommand { get; }

    public ICommand PlaySelectedCommand { get; }

    public ICommand PauseCommand { get; }

    public ICommand StopCommand { get; }

    public ICommand ToggleFavoriteCommand { get; }

    public ICommand ToggleHiddenCommand { get; }

    public ICommand ClearCustomGroupCommand { get; }

    public ICommand AddCustomGroupCommand { get; }

    public ICommand RenameCustomGroupCommand { get; }

    public ICommand DeleteCustomGroupCommand { get; }

    public ICommand MoveChannelUpCommand { get; }

    public ICommand MoveChannelDownCommand { get; }

    public ICommand BatchFavoriteCommand { get; }

    public ICommand BatchHideCommand { get; }

    public ICommand BatchUnhideCommand { get; }

    public ICommand BatchAssignGroupCommand { get; }

    public ICommand BatchClearGroupCommand { get; }

    public ICommand ImportOrganizationCommand { get; }

    public ICommand ExportOrganizationCommand { get; }

    public ICommand ClearFiltersCommand { get; }

    public Channel? SelectedChannel
    {
        get => selectedChannel;
        set
        {
            if (SetProperty(ref selectedChannel, value))
            {
                SelectedChannelDetails = value is null
                    ? "Select a channel to view safe details."
                    : FormatSelectedChannelDetails(value);
                isUpdatingSelectedChannelOrganization = true;
                try
                {
                    SelectedCustomGroupAssignment = value?.CustomGroup ?? SourceGroupAssignmentOption;
                }
                finally
                {
                    isUpdatingSelectedChannelOrganization = false;
                }

                OnPropertyChanged(nameof(HideSelectedChannelLabel));
                RaiseCommandStates();
            }
        }
    }

    public string SearchText
    {
        get => searchText;
        set
        {
            if (SetProperty(ref searchText, value))
            {
                ScheduleSearch();
            }
        }
    }

    public string SelectedGroup
    {
        get => selectedGroup;
        set
        {
            string nextValue = string.IsNullOrWhiteSpace(value) ? AllGroupsOption : value;
            if (SetProperty(ref selectedGroup, nextValue))
            {
                ScheduleSearch();
            }
        }
    }

    public string SelectedCategory
    {
        get => selectedCategory;
        set
        {
            string nextValue = string.IsNullOrWhiteSpace(value) ? AllCategoriesOption : value;
            if (SetProperty(ref selectedCategory, nextValue))
            {
                ScheduleSearch();
            }
        }
    }

    public bool FavoritesOnly
    {
        get => favoritesOnly;
        set
        {
            if (SetProperty(ref favoritesOnly, value))
            {
                ScheduleSearch();
            }
        }
    }

    public HiddenChannelFilter SelectedHiddenFilter
    {
        get => selectedHiddenFilter;
        set
        {
            if (SetProperty(ref selectedHiddenFilter, value))
            {
                ScheduleSearch();
            }
        }
    }

    public ChannelSortMode SelectedSortMode
    {
        get => selectedSortMode;
        set
        {
            if (!Enum.IsDefined(value))
            {
                value = ChannelSortMode.FavoritesFirst;
            }

            if (SetProperty(ref selectedSortMode, value))
            {
                ScheduleSearch();
                _ = SaveOrganizationPreferencesSafelyAsync();
            }
        }
    }

    public string SelectedCustomGroupAssignment
    {
        get => selectedCustomGroupAssignment;
        set
        {
            string nextValue = string.IsNullOrWhiteSpace(value) ? SourceGroupAssignmentOption : value;
            if (SetProperty(ref selectedCustomGroupAssignment, nextValue) && !isUpdatingSelectedChannelOrganization)
            {
                ApplyCustomGroupToSelected(nextValue == SourceGroupAssignmentOption ? null : nextValue);
            }
        }
    }

    public string NewCustomGroupName
    {
        get => newCustomGroupName;
        set => SetProperty(ref newCustomGroupName, value);
    }

    public string? SelectedManagedCustomGroup
    {
        get => selectedManagedCustomGroup;
        set
        {
            if (SetProperty(ref selectedManagedCustomGroup, value))
            {
                RenameCustomGroupName = value ?? string.Empty;
                RaiseCustomGroupCommandStates();
            }
        }
    }

    public string RenameCustomGroupName
    {
        get => renameCustomGroupName;
        set => SetProperty(ref renameCustomGroupName, value);
    }

    public string SelectedBatchGroupAssignment
    {
        get => selectedBatchGroupAssignment;
        set
        {
            string nextValue = string.IsNullOrWhiteSpace(value) ? SourceGroupAssignmentOption : value;
            SetProperty(ref selectedBatchGroupAssignment, nextValue);
        }
    }

    public int SelectedChannelCount
    {
        get => selectedChannelCount;
        private set
        {
            if (SetProperty(ref selectedChannelCount, value))
            {
                OnPropertyChanged(nameof(BatchSelectionText));
            }
        }
    }

    public string BatchSelectionText => SelectedChannelCount == 0
        ? "No channels selected for batch actions."
        : $"{SelectedChannelCount:N0} channels selected for batch actions.";

    public string HideSelectedChannelLabel => SelectedChannel?.IsHidden == true ? "Unhide" : "Hide";

    private bool HasBatchSelection => selectedChannelIds.Count > 0;

    public bool IsBusy
    {
        get => isBusy;
        private set
        {
            if (SetProperty(ref isBusy, value))
            {
                RaiseImportCommandStates();
            }
        }
    }

    public string StatusText
    {
        get => statusText;
        private set => SetProperty(ref statusText, value);
    }

    public string PlaybackStatusText
    {
        get => playbackStatusText;
        private set => SetProperty(ref playbackStatusText, value);
    }

    public string ImportSummaryText
    {
        get => importSummaryText;
        private set => SetProperty(ref importSummaryText, value);
    }

    public string RefreshDiffText
    {
        get => refreshDiffText;
        private set => SetProperty(ref refreshDiffText, value);
    }

    public string EpgSummaryText
    {
        get => epgSummaryText;
        private set => SetProperty(ref epgSummaryText, value);
    }

    public string SelectedChannelDetails
    {
        get => selectedChannelDetails;
        private set => SetProperty(ref selectedChannelDetails, value);
    }

    public BufferingPreset SelectedBufferingPreset
    {
        get => selectedBufferingPreset;
        set
        {
            if (SetProperty(ref selectedBufferingPreset, value))
            {
                _ = SetBufferingPresetSafelyAsync(value);
            }
        }
    }

    public Guid? NowPlayingChannelId
    {
        get => nowPlayingChannelId;
        private set => SetProperty(ref nowPlayingChannelId, value);
    }

    public int Volume
    {
        get => volume;
        set
        {
            int clamped = Math.Clamp(value, 0, 100);
            if (SetProperty(ref volume, clamped))
            {
                _ = SetVolumeSafelyAsync(clamped);
            }
        }
    }

    public async Task InitializeAsync()
    {
        await Clock.InitializeAsync(shutdownCts.Token).ConfigureAwait(true);
        ChannelOrganizationPreferences preferences = await organizationPreferencesStore
            .LoadAsync(shutdownCts.Token)
            .ConfigureAwait(true);
        selectedSortMode = Enum.IsDefined(preferences.SortMode)
            ? preferences.SortMode
            : ChannelSortMode.FavoritesFirst;
        OnPropertyChanged(nameof(SelectedSortMode));
        knownCustomGroups.Clear();
        foreach (string group in preferences.CustomGroups.Select(NormalizeCustomGroup).Where(group => group is not null).Select(group => group!))
        {
            knownCustomGroups.Add(group);
        }

        IReadOnlyDictionary<Guid, ChannelUserState> loadedStates = await channelStateStore
            .LoadChannelStatesAsync(shutdownCts.Token)
            .ConfigureAwait(true);
        channelStates.Clear();
        foreach ((Guid channelId, ChannelUserState state) in loadedStates)
        {
            channelStates[channelId] = state;
        }

        RefreshCustomGroupCollections();

        int favoriteCount = channelStates.Values.Count(state => state.IsFavorite);
        int hiddenCount = channelStates.Values.Count(state => state.IsHidden);
        if (channelStates.Count > 0)
        {
            StatusText = $"Loaded saved channel state: {favoriteCount:N0} favorites, {hiddenCount:N0} hidden. Import a playlist to match them.";
        }
    }

    public async Task ImportPlaylistUrlAsync(string playlistUrl)
    {
        if (string.IsNullOrWhiteSpace(playlistUrl))
        {
            return;
        }

        Func<CancellationToken, Task<PlaylistImportResult>> import =
            ct => playlistImportService.ImportUrlAsync(playlistUrl.Trim(), ct);
        await ImportAsync(import, rememberForRefresh: true).ConfigureAwait(true);
    }

    public void SetSelectedChannels(IEnumerable<Channel> channels)
    {
        ArgumentNullException.ThrowIfNull(channels);

        selectedChannelIds.Clear();
        foreach (Guid channelId in channels.Select(channel => channel.Id).Where(id => id != Guid.Empty))
        {
            selectedChannelIds.Add(channelId);
        }

        SelectedChannelCount = selectedChannelIds.Count;
        RaiseBatchCommandStates();
    }

    public async ValueTask DisposeAsync()
    {
        shutdownCts.Cancel();
        searchCts?.Cancel();
        searchCts?.Dispose();
        Clock.Dispose();
        shutdownCts.Dispose();
        await playbackEngine.DisposeAsync().ConfigureAwait(false);
    }

    private async Task ImportFileAsync()
    {
        try
        {
            if (IsBusy)
            {
                return;
            }

            string? path = dialogService.PickPlaylistFile();
            if (path is null)
            {
                return;
            }

            Func<CancellationToken, Task<PlaylistImportResult>> import = ct => playlistImportService.ImportFileAsync(path, ct);
            await ImportAsync(import, rememberForRefresh: true).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            ShowSafeError("Import failed", ex);
        }
    }

    private async Task ImportUrlAsync()
    {
        try
        {
            if (IsBusy)
            {
                return;
            }

            string? url = dialogService.PromptPlaylistUrl();
            if (string.IsNullOrWhiteSpace(url))
            {
                return;
            }

            await ImportPlaylistUrlAsync(url).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            ShowSafeError("Import failed", ex);
        }
    }

    private async Task LoadSampleAsync()
    {
        try
        {
            if (IsBusy)
            {
                return;
            }

            string samplePath = Path.Combine(AppContext.BaseDirectory, "Samples", "synthetic-news-sports.m3u");
            if (!File.Exists(samplePath))
            {
                dialogService.ShowError("Sample playlist missing", $"Could not find sample playlist at {samplePath}");
                return;
            }

            Func<CancellationToken, Task<PlaylistImportResult>> import = ct => playlistImportService.ImportFileAsync(samplePath, ct);
            await ImportAsync(import, rememberForRefresh: true).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            ShowSafeError("Sample import failed", ex);
        }
    }

    private async Task ImportAsync(Func<CancellationToken, Task<PlaylistImportResult>> import, bool rememberForRefresh)
    {
        try
        {
            IsBusy = true;
            StatusText = "Importing playlist...";
            AddDiagnostic("Playlist import started.");
            HashSet<Guid> previousIds = allChannels.Select(channel => channel.Id).ToHashSet();
            PlaylistImportResult result = await import(shutdownCts.Token).ConfigureAwait(true);

            if (result.Summary.ErrorCount > 0 && result.Channels.Count == 0)
            {
                string error = result.Issues.FirstOrDefault(issue => issue.Severity == ImportIssueSeverity.Error)?.Message
                    ?? "Playlist import failed.";
                dialogService.ShowError("Playlist import failed", error);
                StatusText = error;
                AddDiagnostic($"Playlist import failed: {error}");
                return;
            }

            allChannels.Clear();
            allChannels.AddRange(result.Channels.Select(ApplyUserState));
            SelectedChannel = null;
            RefreshGroupsAndCategories();
            PopulateImportIssues(result.Issues);
            await ApplySearchAsync(shutdownCts.Token).ConfigureAwait(true);

            PlaylistDiffSummary diff = CalculateDiff(previousIds, allChannels.Select(channel => channel.Id));
            RefreshDiffText = FormatDiff(diff);
            ImportSummaryText =
                $"Imported {result.Summary.ImportedCount:N0}; valid {result.Summary.ValidCount:N0}; " +
                $"warnings {result.Summary.WarningCount:N0}; errors {result.Summary.ErrorCount:N0}; " +
                $"duplicates {result.Summary.DuplicateCount:N0}.";
            StatusText =
                $"Imported {result.Summary.ImportedCount:N0} channels. " +
                $"{result.Summary.WarningCount:N0} warnings, {result.Summary.DuplicateCount:N0} duplicates.";
            AddDiagnostic($"{StatusText} {RefreshDiffText}");

            if (rememberForRefresh)
            {
                lastPlaylistImport = import;
            }
        }
        catch (OperationCanceledException)
        {
            StatusText = "Import cancelled.";
            AddDiagnostic("Playlist import cancelled.");
        }
        catch (Exception ex)
        {
            StatusText = "Import failed.";
            string message = SensitiveTextRedactor.RedactText(ex.Message);
            AddDiagnostic($"Playlist import exception: {message}");
            dialogService.ShowError("Import failed", message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RefreshPlaylistAsync()
    {
        if (lastPlaylistImport is null)
        {
            StatusText = "Import a playlist before refreshing.";
            return;
        }

        await ImportAsync(lastPlaylistImport, rememberForRefresh: false).ConfigureAwait(true);
    }

    private async Task ImportEpgAsync()
    {
        try
        {
            if (IsBusy)
            {
                return;
            }

            string? path = dialogService.PickXmltvFile();
            if (path is null)
            {
                return;
            }

            IsBusy = true;
            AddDiagnostic("XMLTV import started.");
            EpgImportResult result = await xmltvImportService.ImportFileAsync(path, shutdownCts.Token).ConfigureAwait(true);
            int matched = CountEpgMatches(result.Channels);
            EpgSummaryText = $"{result.SummaryText} Matched channels {matched:N0}.";
            StatusText = EpgSummaryText;
            AddDiagnostic(EpgSummaryText);
        }
        catch (OperationCanceledException)
        {
            StatusText = "XMLTV import cancelled.";
            AddDiagnostic(StatusText);
        }
        catch (Exception ex)
        {
            ShowSafeError("XMLTV import failed", ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ExportOrganizationAsync()
    {
        try
        {
            if (IsBusy)
            {
                return;
            }

            string? path = dialogService.PickOrganizationExportFile();
            if (path is null)
            {
                return;
            }

            await SaveChannelStatesSafelyAsync().ConfigureAwait(true);
            await SaveOrganizationPreferencesSafelyAsync().ConfigureAwait(true);
            ChannelOrganizationBackup backup = CreateOrganizationBackup();
            await organizationBackupService.ExportAsync(path, backup, shutdownCts.Token).ConfigureAwait(true);
            StatusText = $"Exported channel organization with {backup.ChannelStates.Length:N0} saved channel states.";
            AddDiagnostic(StatusText);
        }
        catch (OperationCanceledException)
        {
            StatusText = "Organization export cancelled.";
            AddDiagnostic(StatusText);
        }
        catch (Exception ex)
        {
            ShowSafeError("Organization export failed", ex);
        }
    }

    private async Task ImportOrganizationAsync()
    {
        try
        {
            if (IsBusy)
            {
                return;
            }

            string? path = dialogService.PickOrganizationImportFile();
            if (path is null)
            {
                return;
            }

            IsBusy = true;
            ChannelOrganizationBackup backup = await organizationBackupService.ImportAsync(path, shutdownCts.Token).ConfigureAwait(true);
            ApplyOrganizationBackup(backup);
            await channelStateStore.SaveChannelStatesAsync(channelStates.Values.ToArray(), shutdownCts.Token).ConfigureAwait(true);
            await organizationPreferencesStore.SaveAsync(CreateOrganizationPreferences(), shutdownCts.Token).ConfigureAwait(true);
            await ApplySearchAsync(shutdownCts.Token).ConfigureAwait(true);
            StatusText = $"Imported channel organization with {backup.ChannelStates.Length:N0} saved channel states.";
            AddDiagnostic(StatusText);
        }
        catch (OperationCanceledException)
        {
            StatusText = "Organization import cancelled.";
            AddDiagnostic(StatusText);
        }
        catch (Exception ex)
        {
            ShowSafeError("Organization import failed", ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task PlaySelectedAsync()
    {
        if (SelectedChannel is null)
        {
            return;
        }

        try
        {
            await playbackEngine.PlayAsync(SelectedChannel, shutdownCts.Token).ConfigureAwait(true);
            UpdateSelectedChannel(channel => channel with { LastWatchedAt = DateTimeOffset.UtcNow }, refreshGroups: false);
            AddDiagnostic($"Playback requested for '{SelectedChannel.DisplayName}' on host {SelectedChannel.StreamUrl.Host}.");
        }
        catch (OperationCanceledException)
        {
            PlaybackStatusText = "Playback cancelled.";
            AddDiagnostic(PlaybackStatusText);
        }
        catch (Exception ex)
        {
            PlaybackStatusText = $"Playback failed: {SensitiveTextRedactor.RedactText(ex.Message)}";
            AddDiagnostic(PlaybackStatusText);
        }
    }

    private async Task PauseAsync()
    {
        try
        {
            await playbackEngine.PauseAsync(shutdownCts.Token).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            PlaybackStatusText = $"Pause failed: {SensitiveTextRedactor.RedactText(ex.Message)}";
        }
    }

    private async Task StopAsync()
    {
        try
        {
            await playbackEngine.StopAsync(shutdownCts.Token).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            PlaybackStatusText = $"Stop failed: {SensitiveTextRedactor.RedactText(ex.Message)}";
        }
    }

    private async Task SetVolumeSafelyAsync(int value)
    {
        try
        {
            await playbackEngine.SetVolumeAsync(value, shutdownCts.Token).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            PlaybackStatusText = $"Volume update failed: {SensitiveTextRedactor.RedactText(ex.Message)}";
        }
    }

    private async Task SetBufferingPresetSafelyAsync(BufferingPreset preset)
    {
        try
        {
            await playbackEngine.SetBufferingPresetAsync(preset, shutdownCts.Token).ConfigureAwait(true);
            AddDiagnostic($"Buffering preset set to {preset}.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            PlaybackStatusText = $"Buffering preset update failed: {SensitiveTextRedactor.RedactText(ex.Message)}";
            AddDiagnostic(PlaybackStatusText);
        }
    }

    private void ToggleFavorite()
    {
        UpdateSelectedChannel(channel => channel with { IsFavorite = !channel.IsFavorite });
    }

    private void ToggleHidden()
    {
        UpdateSelectedChannel(channel => channel with { IsHidden = !channel.IsHidden });
    }

    private void ClearCustomGroup()
    {
        SelectedCustomGroupAssignment = SourceGroupAssignmentOption;
        ApplyCustomGroupToSelected(null);
    }

    private void AddCustomGroup()
    {
        string? normalized = NormalizeCustomGroup(NewCustomGroupName);
        if (normalized is null)
        {
            StatusText = "Enter a custom group name before adding.";
            return;
        }

        if (normalized.Equals(AllGroupsOption, StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals(AllCategoriesOption, StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals(SourceGroupAssignmentOption, StringComparison.OrdinalIgnoreCase))
        {
            StatusText = $"'{normalized}' is reserved. Choose a different custom group name.";
            return;
        }

        EnsureCustomGroupChoice(normalized);
        knownCustomGroups.Add(normalized);
        _ = SaveOrganizationPreferencesSafelyAsync();
        SelectedManagedCustomGroup = normalized;
        NewCustomGroupName = string.Empty;
        SelectedCustomGroupAssignment = normalized;
        StatusText = SelectedChannel is null
            ? $"Added custom group '{normalized}' for this session. Select a channel to assign it."
            : $"Assigned '{SelectedChannel.DisplayName}' to custom group '{normalized}'.";
    }

    private void RenameCustomGroup()
    {
        if (SelectedManagedCustomGroup is null)
        {
            StatusText = "Select a custom group before renaming.";
            return;
        }

        string? normalized = NormalizeCustomGroup(RenameCustomGroupName);
        if (normalized is null)
        {
            StatusText = "Enter a replacement group name.";
            return;
        }

        if (normalized.Equals(SelectedManagedCustomGroup, StringComparison.OrdinalIgnoreCase))
        {
            StatusText = "Custom group name is unchanged.";
            return;
        }

        if (normalized.Equals(AllGroupsOption, StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals(AllCategoriesOption, StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals(SourceGroupAssignmentOption, StringComparison.OrdinalIgnoreCase) ||
            CustomGroups.Contains(normalized, StringComparer.OrdinalIgnoreCase))
        {
            StatusText = $"'{normalized}' is reserved or already exists.";
            return;
        }

        knownCustomGroups.Remove(SelectedManagedCustomGroup);
        knownCustomGroups.Add(normalized);
        _ = SaveOrganizationPreferencesSafelyAsync();
        int changed = ReplaceCustomGroup(SelectedManagedCustomGroup, normalized);
        SelectedManagedCustomGroup = normalized;
        StatusText = $"Renamed custom group to '{normalized}' for {changed:N0} saved channels.";
    }

    private void DeleteCustomGroup()
    {
        if (SelectedManagedCustomGroup is null)
        {
            StatusText = "Select a custom group before deleting.";
            return;
        }

        string removedGroup = SelectedManagedCustomGroup;
        knownCustomGroups.Remove(removedGroup);
        _ = SaveOrganizationPreferencesSafelyAsync();
        int changed = ReplaceCustomGroup(removedGroup, null);
        SelectedManagedCustomGroup = null;
        StatusText = $"Removed custom group '{removedGroup}' from {changed:N0} saved channels.";
    }

    private void MoveSelectedChannel(int direction)
    {
        if (SelectedChannel is null)
        {
            return;
        }

        string group = SelectedChannel.EffectiveGroupTitle;
        List<Channel> orderedGroupChannels = allChannels
            .Where(channel => channel.EffectiveGroupTitle.Equals(group, StringComparison.OrdinalIgnoreCase))
            .OrderBy(GetCustomOrderIndex)
            .ThenBy(channel => channel.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
        int selectedIndex = orderedGroupChannels.FindIndex(channel => channel.Id == SelectedChannel.Id);
        int targetIndex = selectedIndex + direction;
        if (selectedIndex < 0 || targetIndex < 0 || targetIndex >= orderedGroupChannels.Count)
        {
            StatusText = "Selected channel cannot move farther in this group.";
            return;
        }

        Channel selected = orderedGroupChannels[selectedIndex];
        Channel target = orderedGroupChannels[targetIndex];
        int selectedSortIndex = GetCustomOrderIndex(selected);
        int targetSortIndex = GetCustomOrderIndex(target);
        UpdateChannelById(selected.Id, channel => channel with { CustomSortIndex = targetSortIndex });
        UpdateChannelById(target.Id, channel => channel with { CustomSortIndex = selectedSortIndex });
        SelectedSortMode = ChannelSortMode.CustomOrder;
        RefreshGroupsAndCategories();
        ScheduleSearch();
        _ = SaveChannelStatesSafelyAsync();
        StatusText = $"Moved '{selected.DisplayName}' {(direction < 0 ? "up" : "down")} in '{group}'.";
    }

    private void AssignBatchGroup()
    {
        string? normalized = SelectedBatchGroupAssignment == SourceGroupAssignmentOption
            ? null
            : NormalizeCustomGroup(SelectedBatchGroupAssignment);

        if (normalized is not null)
        {
            knownCustomGroups.Add(normalized);
            EnsureCustomGroupChoice(normalized);
            _ = SaveOrganizationPreferencesSafelyAsync();
        }

        ApplyBatchUpdate(
            channel => channel with { CustomGroup = normalized },
            normalized is null ? "removed from custom groups" : $"assigned to '{normalized}'");
    }

    private void ApplyBatchUpdate(Func<Channel, Channel> update, string actionDescription)
    {
        if (selectedChannelIds.Count == 0)
        {
            StatusText = "Select channels before using batch actions.";
            return;
        }

        HashSet<Guid> selectedIds = selectedChannelIds.ToHashSet();
        int changed = 0;
        for (int index = 0; index < allChannels.Count; index++)
        {
            Channel current = allChannels[index];
            if (!selectedIds.Contains(current.Id))
            {
                continue;
            }

            Channel updated = update(current);
            if (updated == current)
            {
                continue;
            }

            allChannels[index] = updated;
            UpdateChannelStateIndex(updated);
            changed++;
            if (SelectedChannel?.Id == updated.Id)
            {
                SelectedChannel = updated;
            }
        }

        if (changed == 0)
        {
            StatusText = "Batch action made no changes.";
            return;
        }

        RefreshGroupsAndCategories();
        ScheduleSearch();
        _ = SaveChannelStatesSafelyAsync();
        StatusText = $"{changed:N0} selected channels {actionDescription}.";
    }

    private void ClearFilters()
    {
        SearchText = string.Empty;
        SelectedGroup = AllGroupsOption;
        SelectedCategory = AllCategoriesOption;
        FavoritesOnly = false;
        SelectedHiddenFilter = HiddenChannelFilter.VisibleOnly;
        ScheduleSearch();
    }

    private void ScheduleSearch()
    {
        if (shutdownCts.IsCancellationRequested)
        {
            return;
        }

        searchCts?.Cancel();
        searchCts?.Dispose();
        searchCts = CancellationTokenSource.CreateLinkedTokenSource(shutdownCts.Token);
        CancellationToken token = searchCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(150), token).ConfigureAwait(false);
                await ApplySearchAsync(token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected during rapid typing/filter changes.
            }
        }, token);
    }

    private async Task ApplySearchAsync(CancellationToken cancellationToken)
    {
        var query = new ChannelSearchQuery
        {
            Text = SearchText,
            Group = SelectedGroup == AllGroupsOption ? null : SelectedGroup,
            Category = SelectedCategory == AllCategoriesOption ? null : SelectedCategory,
            FavoritesOnly = FavoritesOnly,
            HiddenFilter = SelectedHiddenFilter,
            SortMode = SelectedSortMode,
            Limit = MaxVisibleChannelResults
        };

        Channel[] snapshot = allChannels.ToArray();
        IReadOnlyList<Channel> results = await Task.Run(
            () => channelSearchService.Search(snapshot, query),
            cancellationToken).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();

        UiDispatcher.Run(() =>
        {
            VisibleChannels.ReplaceAll(results);

            if (allChannels.Count > 0)
            {
                int hiddenCount = allChannels.Count(channel => channel.IsHidden);
                string hiddenSummary = hiddenCount > 0 ? $" ({hiddenCount:N0} hidden)" : string.Empty;
                string capSummary = VisibleChannels.Count >= MaxVisibleChannelResults
                    ? $" Showing first {MaxVisibleChannelResults:N0} results."
                    : string.Empty;
                StatusText = SelectedHiddenFilter == HiddenChannelFilter.HiddenOnly
                    ? $"Showing {VisibleChannels.Count:N0} hidden channels of {allChannels.Count:N0} total."
                    : $"Showing {VisibleChannels.Count:N0} of {allChannels.Count:N0} channels{hiddenSummary}.{capSummary}";
            }
        });
    }

    private void RefreshGroupsAndCategories()
    {
        string previousGroup = SelectedGroup;
        string previousCategory = SelectedCategory;

        Groups.Clear();
        Groups.Add(AllGroupsOption);
        foreach (string group in allChannels
                     .Select(channel => channel.EffectiveGroupTitle)
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .Order(StringComparer.OrdinalIgnoreCase))
        {
            Groups.Add(group);
        }

        Categories.Clear();
        Categories.Add(AllCategoriesOption);
        foreach (string category in allChannels.Select(channel => channel.Category).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase))
        {
            Categories.Add(category);
        }

        RefreshCustomGroupCollections();

        SelectedGroup = Groups.Contains(previousGroup, StringComparer.OrdinalIgnoreCase)
            ? previousGroup
            : AllGroupsOption;
        SelectedCategory = Categories.Contains(previousCategory, StringComparer.OrdinalIgnoreCase)
            ? previousCategory
            : AllCategoriesOption;
    }

    private Channel ApplyUserState(Channel channel)
    {
        if (!channelStates.TryGetValue(channel.Id, out ChannelUserState? state))
        {
            return channel;
        }

        return channel with
        {
            IsFavorite = state.IsFavorite,
            IsHidden = state.IsHidden,
            CustomGroup = NormalizeCustomGroup(state.CustomGroup),
            CustomSortIndex = NormalizeCustomSortIndex(state.CustomSortIndex),
            LastWatchedAt = state.LastWatchedAt
        };
    }

    private void UpdateSelectedChannel(Func<Channel, Channel> update, bool refreshGroups = true)
    {
        if (SelectedChannel is null)
        {
            return;
        }

        int index = allChannels.FindIndex(channel => channel.Id == SelectedChannel.Id);
        if (index < 0)
        {
            return;
        }

        Channel updated = update(allChannels[index]);
        if (updated == allChannels[index])
        {
            return;
        }

        allChannels[index] = updated;
        UpdateChannelStateIndex(updated);
        SelectedChannel = updated;
        if (refreshGroups)
        {
            RefreshGroupsAndCategories();
        }

        ScheduleSearch();
        _ = SaveChannelStatesSafelyAsync();
    }

    private void ApplyCustomGroupToSelected(string? customGroup)
    {
        string? normalized = NormalizeCustomGroup(customGroup);
        UpdateSelectedChannel(channel => channel with { CustomGroup = normalized });
    }

    private void UpdateChannelStateIndex(Channel channel)
    {
        var state = new ChannelUserState
        {
            ChannelId = channel.Id,
            IsFavorite = channel.IsFavorite,
            IsHidden = channel.IsHidden,
            CustomGroup = NormalizeCustomGroup(channel.CustomGroup),
            CustomSortIndex = NormalizeCustomSortIndex(channel.CustomSortIndex),
            LastWatchedAt = channel.LastWatchedAt
        };

        if (HasUserState(state))
        {
            channelStates[channel.Id] = state;
        }
        else
        {
            channelStates.Remove(channel.Id);
        }
    }

    private async Task SaveChannelStatesSafelyAsync()
    {
        try
        {
            ChannelUserState[] stateSnapshot = channelStates.Values.ToArray();
            await channelStateStore.SaveChannelStatesAsync(stateSnapshot, shutdownCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown.
        }
        catch (Exception ex)
        {
            string message = SensitiveTextRedactor.RedactText(ex.Message);
            UiDispatcher.Run(() => AddDiagnostic($"Channel state save failed: {message}"));
        }
    }

    private ChannelOrganizationBackup CreateOrganizationBackup()
    {
        return new ChannelOrganizationBackup
        {
            Preferences = CreateOrganizationPreferences(),
            ChannelStates = channelStates.Values
                .Where(HasUserState)
                .OrderBy(state => state.ChannelId)
                .ToArray()
        };
    }

    private ChannelOrganizationPreferences CreateOrganizationPreferences()
    {
        string[] customGroups = knownCustomGroups
            .Select(NormalizeCustomGroup)
            .Where(group => group is not null)
            .Select(group => group!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new ChannelOrganizationPreferences
        {
            SortMode = SelectedSortMode,
            CustomGroups = customGroups
        };
    }

    private void ApplyOrganizationBackup(ChannelOrganizationBackup backup)
    {
        channelStates.Clear();
        foreach (ChannelUserState state in backup.ChannelStates.Where(HasUserState))
        {
            channelStates[state.ChannelId] = state;
        }

        knownCustomGroups.Clear();
        foreach (string group in backup.Preferences.CustomGroups
                     .Select(NormalizeCustomGroup)
                     .Where(group => group is not null)
                     .Select(group => group!))
        {
            knownCustomGroups.Add(group);
        }

        for (int index = 0; index < allChannels.Count; index++)
        {
            allChannels[index] = ApplyUserState(allChannels[index] with
            {
                IsFavorite = false,
                IsHidden = false,
                CustomGroup = null,
                CustomSortIndex = null,
                LastWatchedAt = null
            });
        }

        selectedSortMode = Enum.IsDefined(backup.Preferences.SortMode)
            ? backup.Preferences.SortMode
            : ChannelSortMode.FavoritesFirst;
        OnPropertyChanged(nameof(SelectedSortMode));
        RefreshGroupsAndCategories();
        if (SelectedChannel is not null)
        {
            SelectedChannel = allChannels.FirstOrDefault(channel => channel.Id == SelectedChannel.Id);
        }
    }

    private async Task SaveOrganizationPreferencesSafelyAsync()
    {
        try
        {
            await organizationPreferencesStore.SaveAsync(CreateOrganizationPreferences(), shutdownCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown.
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            string message = SensitiveTextRedactor.RedactText(ex.Message);
            UiDispatcher.Run(() => AddDiagnostic($"Organization preference save failed: {message}"));
        }
    }

    private int ReplaceCustomGroup(string sourceGroup, string? replacementGroup)
    {
        string? normalizedReplacement = NormalizeCustomGroup(replacementGroup);
        int changed = 0;
        HashSet<Guid> loadedChannelIds = allChannels.Select(channel => channel.Id).ToHashSet();
        for (int index = 0; index < allChannels.Count; index++)
        {
            Channel channel = allChannels[index];
            if (!string.Equals(channel.CustomGroup, sourceGroup, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            Channel updated = channel with { CustomGroup = normalizedReplacement };
            allChannels[index] = updated;
            UpdateChannelStateIndex(updated);
            changed++;
            if (SelectedChannel?.Id == updated.Id)
            {
                SelectedChannel = updated;
            }
        }

        foreach ((Guid channelId, ChannelUserState state) in channelStates.ToArray())
        {
            if (!string.Equals(state.CustomGroup, sourceGroup, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            ChannelUserState updated = state with { CustomGroup = normalizedReplacement };
            if (HasUserState(updated))
            {
                channelStates[channelId] = updated;
            }
            else
            {
                channelStates.Remove(channelId);
            }

            if (!loadedChannelIds.Contains(channelId))
            {
                changed++;
            }
        }

        RefreshGroupsAndCategories();
        ScheduleSearch();
        _ = SaveChannelStatesSafelyAsync();
        return changed;
    }

    private void UpdateChannelById(Guid channelId, Func<Channel, Channel> update)
    {
        int index = allChannels.FindIndex(channel => channel.Id == channelId);
        if (index < 0)
        {
            return;
        }

        Channel updated = update(allChannels[index]);
        allChannels[index] = updated;
        UpdateChannelStateIndex(updated);
        if (SelectedChannel?.Id == updated.Id)
        {
            SelectedChannel = updated;
        }
    }

    private void RefreshCustomGroupCollections()
    {
        string desiredAssignment = SelectedChannel?.CustomGroup ?? selectedCustomGroupAssignment;
        Dictionary<string, int> customGroupCounts = CountCustomGroupChannels();
        string[] customGroups = allChannels
            .Select(channel => channel.CustomGroup)
            .Concat(channelStates.Values.Select(state => state.CustomGroup))
            .Concat(knownCustomGroups)
            .Select(NormalizeCustomGroup)
            .Where(group => group is not null)
            .Select(group => group!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        isUpdatingSelectedChannelOrganization = true;
        try
        {
            CustomGroups.Clear();
            CustomGroupSummaries.Clear();
            CustomGroupAssignments.Clear();
            CustomGroupAssignments.Add(SourceGroupAssignmentOption);
            foreach (string group in customGroups)
            {
                CustomGroups.Add(group);
                CustomGroupSummaries.Add(new CustomGroupSummaryViewModel(
                    group,
                    customGroupCounts.GetValueOrDefault(group)));
                CustomGroupAssignments.Add(group);
            }

            SelectedCustomGroupAssignment = CustomGroupAssignments.Contains(desiredAssignment, StringComparer.OrdinalIgnoreCase)
                ? desiredAssignment
                : SourceGroupAssignmentOption;
            SelectedBatchGroupAssignment = CustomGroupAssignments.Contains(SelectedBatchGroupAssignment, StringComparer.OrdinalIgnoreCase)
                ? SelectedBatchGroupAssignment
                : SourceGroupAssignmentOption;
            if (SelectedManagedCustomGroup is not null &&
                !CustomGroups.Contains(SelectedManagedCustomGroup, StringComparer.OrdinalIgnoreCase))
            {
                SelectedManagedCustomGroup = null;
            }
        }
        finally
        {
            isUpdatingSelectedChannelOrganization = false;
        }
    }

    private Dictionary<string, int> CountCustomGroupChannels()
    {
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        HashSet<Guid> loadedChannelIds = allChannels.Select(channel => channel.Id).ToHashSet();

        foreach (Channel channel in allChannels)
        {
            string? group = NormalizeCustomGroup(channel.CustomGroup);
            if (group is null)
            {
                continue;
            }

            counts[group] = counts.GetValueOrDefault(group) + 1;
        }

        foreach ((Guid channelId, ChannelUserState state) in channelStates)
        {
            if (loadedChannelIds.Contains(channelId))
            {
                continue;
            }

            string? group = NormalizeCustomGroup(state.CustomGroup);
            if (group is null)
            {
                continue;
            }

            counts[group] = counts.GetValueOrDefault(group) + 1;
        }

        foreach (string group in knownCustomGroups)
        {
            counts.TryAdd(group, 0);
        }

        return counts;
    }

    private void EnsureCustomGroupChoice(string group)
    {
        if (!CustomGroupAssignments.Contains(group, StringComparer.OrdinalIgnoreCase))
        {
            CustomGroupAssignments.Add(group);
        }

        if (!CustomGroups.Contains(group, StringComparer.OrdinalIgnoreCase))
        {
            CustomGroups.Add(group);
        }

        if (!CustomGroupSummaries.Any(summary => summary.Name.Equals(group, StringComparison.OrdinalIgnoreCase)))
        {
            CustomGroupSummaries.Add(new CustomGroupSummaryViewModel(group, 0));
        }
    }

    private static bool HasUserState(ChannelUserState state)
    {
        return state.ChannelId != Guid.Empty &&
            (state.IsFavorite ||
                state.IsHidden ||
                !string.IsNullOrWhiteSpace(state.CustomGroup) ||
                state.CustomSortIndex.HasValue ||
                state.LastWatchedAt.HasValue);
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

    private static int GetCustomOrderIndex(Channel channel)
    {
        return channel.CustomSortIndex ?? channel.ImportIndex;
    }

    private static string FormatSelectedChannelDetails(Channel channel)
    {
        string groupText = channel.CustomGroup is null
            ? channel.GroupTitle
            : $"{channel.EffectiveGroupTitle} (source: {channel.GroupTitle})";
        string hiddenText = channel.IsHidden ? " | Hidden" : string.Empty;
        return $"Group: {groupText} | Category: {channel.Category} | Host: {channel.StreamUrl.Host}{hiddenText}";
    }

    private void ApplyPlaybackState(PlaybackStateSnapshot state)
    {
        PlaybackStatusText = state.Channel is null
            ? state.Message
            : $"{state.Status}: {state.Channel.DisplayName} — {state.Message}";
        UpdateNowPlayingMarker(state);
        AddDiagnostic(PlaybackStatusText);
    }

    private void UpdateNowPlayingMarker(PlaybackStateSnapshot state)
    {
        if (state.Channel is null)
        {
            return;
        }

        switch (state.Status)
        {
            case PlaybackStatus.Loading:
            case PlaybackStatus.Buffering:
            case PlaybackStatus.Playing:
            case PlaybackStatus.Paused:
                NowPlayingChannelId = state.Channel.Id;
                break;
            case PlaybackStatus.Stopped:
            case PlaybackStatus.Failed:
            case PlaybackStatus.Unsupported:
            case PlaybackStatus.TimedOut:
                if (NowPlayingChannelId == state.Channel.Id)
                {
                    NowPlayingChannelId = null;
                }

                break;
        }
    }

    private void PopulateImportIssues(IReadOnlyList<PlaylistImportIssue> issues)
    {
        RecentImportIssues.Clear();
        foreach (PlaylistImportIssue issue in issues
                     .OrderByDescending(issue => issue.Severity)
                     .ThenBy(issue => issue.LineNumber ?? int.MaxValue)
                     .Take(25))
        {
            RecentImportIssues.Add(ImportIssueViewModel.FromIssue(issue));
        }
    }

    private void RaiseCommandStates()
    {
        if (PlaySelectedCommand is AsyncRelayCommand play)
        {
            play.RaiseCanExecuteChanged();
        }

        if (ToggleFavoriteCommand is RelayCommand favorite)
        {
            favorite.RaiseCanExecuteChanged();
        }

        if (ToggleHiddenCommand is RelayCommand hidden)
        {
            hidden.RaiseCanExecuteChanged();
        }

        if (ClearCustomGroupCommand is RelayCommand clearGroup)
        {
            clearGroup.RaiseCanExecuteChanged();
        }

        if (MoveChannelUpCommand is RelayCommand moveUp)
        {
            moveUp.RaiseCanExecuteChanged();
        }

        if (MoveChannelDownCommand is RelayCommand moveDown)
        {
            moveDown.RaiseCanExecuteChanged();
        }
    }

    private void RaiseCustomGroupCommandStates()
    {
        if (RenameCustomGroupCommand is RelayCommand rename)
        {
            rename.RaiseCanExecuteChanged();
        }

        if (DeleteCustomGroupCommand is RelayCommand delete)
        {
            delete.RaiseCanExecuteChanged();
        }
    }

    private void RaiseBatchCommandStates()
    {
        if (BatchFavoriteCommand is RelayCommand favorite)
        {
            favorite.RaiseCanExecuteChanged();
        }

        if (BatchHideCommand is RelayCommand hide)
        {
            hide.RaiseCanExecuteChanged();
        }

        if (BatchUnhideCommand is RelayCommand unhide)
        {
            unhide.RaiseCanExecuteChanged();
        }

        if (BatchAssignGroupCommand is RelayCommand assignGroup)
        {
            assignGroup.RaiseCanExecuteChanged();
        }

        if (BatchClearGroupCommand is RelayCommand clearGroup)
        {
            clearGroup.RaiseCanExecuteChanged();
        }
    }

    private void RaiseImportCommandStates()
    {
        if (ImportFileCommand is AsyncRelayCommand file)
        {
            file.RaiseCanExecuteChanged();
        }

        if (ImportUrlCommand is AsyncRelayCommand url)
        {
            url.RaiseCanExecuteChanged();
        }

        if (LoadSampleCommand is AsyncRelayCommand sample)
        {
            sample.RaiseCanExecuteChanged();
        }

        if (RefreshPlaylistCommand is AsyncRelayCommand refresh)
        {
            refresh.RaiseCanExecuteChanged();
        }

        if (ImportEpgCommand is AsyncRelayCommand epg)
        {
            epg.RaiseCanExecuteChanged();
        }

        if (ImportOrganizationCommand is AsyncRelayCommand importOrganization)
        {
            importOrganization.RaiseCanExecuteChanged();
        }

        if (ExportOrganizationCommand is AsyncRelayCommand exportOrganization)
        {
            exportOrganization.RaiseCanExecuteChanged();
        }
    }

    private void ShowSafeError(string title, Exception ex)
    {
        string message = SensitiveTextRedactor.RedactText(ex.Message);
        StatusText = message;
        AddDiagnostic($"{title}: {message}");
        dialogService.ShowError(title, message);
    }

    private void AddDiagnostic(string message)
    {
        string entry = $"{DateTimeOffset.Now:HH:mm:ss} {SensitiveTextRedactor.RedactText(message)}";
        Diagnostics.Insert(0, entry);
        while (Diagnostics.Count > 100)
        {
            Diagnostics.RemoveAt(Diagnostics.Count - 1);
        }
    }

    private static PlaylistDiffSummary CalculateDiff(HashSet<Guid> previousIds, IEnumerable<Guid> currentIds)
    {
        HashSet<Guid> current = currentIds.ToHashSet();
        int unchanged = current.Count(previousIds.Contains);
        int added = current.Count - unchanged;
        int removed = previousIds.Count(id => !current.Contains(id));
        return new PlaylistDiffSummary(previousIds.Count, current.Count, added, removed, unchanged);
    }

    private static string FormatDiff(PlaylistDiffSummary diff)
    {
        return $"Refresh diff: previous {diff.PreviousCount:N0}; current {diff.CurrentCount:N0}; added {diff.AddedCount:N0}; removed {diff.RemovedCount:N0}; unchanged {diff.UnchangedCount:N0}.";
    }

    private int CountEpgMatches(IEnumerable<EpgChannel> epgChannels)
    {
        HashSet<string> epgIds = epgChannels.Select(channel => ChannelNormalizer.NormalizeForSearch(channel.Id)).ToHashSet();
        HashSet<string> epgNames = epgChannels.Select(channel => ChannelNormalizer.NormalizeForSearch(channel.DisplayName)).ToHashSet();

        return allChannels.Count(channel =>
            (!string.IsNullOrWhiteSpace(channel.TvgId) && epgIds.Contains(ChannelNormalizer.NormalizeForSearch(channel.TvgId))) ||
            epgNames.Contains(channel.NormalizedName));
    }
}
