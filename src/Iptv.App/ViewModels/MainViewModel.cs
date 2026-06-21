using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows.Input;
using Iptv.App.Mvvm;
using Iptv.App.Services;
using Iptv.Core;
using Iptv.Core.Channels;
using Iptv.Core.Diagnostics;
using Iptv.Core.Epg;
using Iptv.Core.Playback;
using Iptv.Core.PlaylistImport;
using Iptv.Epg;
using Iptv.Persistence;
using Iptv.Persistence.CustomGroups;
using Iptv.Persistence.Logos;
using Iptv.Persistence.RecentPlaylists;
using Iptv.Persistence.SmartGroups;
using Iptv.Persistence.SourceProfiles;
using Iptv.Playback;
using Iptv.Playlists;
using Iptv.Search;

namespace Iptv.App.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private const string AllGroupsOption = "All Groups";
    private const string AllCategoriesOption = "All Categories";
    private const string AllYearsOption = "All Years";
    private const string SourceGroupAssignmentOption = "Source group";
    private const int StandardVisibleChannelResults = 50_000;
    private const int LargeLibraryVisibleChannelResults = 10_000;
    private const int MaximumLogoPrefetchCount = 250;
    private const int MaximumEpgTimelineRows = 250;
    private const int MaximumDuplicateGroups = 100;
    private const int MaximumVodLibraryItems = 1_000;
    private const int VodLibraryPageSize = 60;
    private const int SearchBenchmarkChannelCount = 50_000;
    private const long MaximumRemoteXmltvBytes = 50L * 1024 * 1024;
    private const int MaximumRecentPlaylistSources = 10;

    private readonly IPlaylistImportService playlistImportService;
    private readonly IChannelSearchService channelSearchService;
    private readonly IPlaybackEngine playbackEngine;
    private readonly IChannelStateStore channelStateStore;
    private readonly IChannelOrganizationPreferencesStore organizationPreferencesStore;
    private readonly IChannelOrganizationBackupService organizationBackupService;
    private readonly ILogoCacheService logoCacheService;
    private readonly IRecentPlaylistSourceFileService recentPlaylistSourceFileService;
    private readonly ISourceProfileFileService sourceProfileFileService;
    private readonly ISmartGroupPresetFileService smartGroupPresetFileService;
    private readonly IUiPreferencesStore uiPreferencesStore;
    private readonly IThemeService themeService;
    private readonly CustomGroupCsvService customGroupCsvService = new();
    private readonly HttpClient logoHttpClient = new() { Timeout = TimeSpan.FromSeconds(6) };
    private readonly HttpClient xmltvHttpClient = new() { Timeout = TimeSpan.FromSeconds(20) };
    private readonly IXmltvImportService xmltvImportService;
    private readonly IPlaylistDialogService dialogService;
    private readonly List<Channel> allChannels = [];
    private readonly CancellationTokenSource shutdownCts = new();
    private CancellationTokenSource? searchCts;
    private PlaylistImportOperation? lastPlaylistImport;
    private readonly Dictionary<Guid, ChannelUserState> channelStates = [];
    private readonly HashSet<string> knownCustomGroups = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<Guid> selectedChannelIds = [];
    private readonly Dictionary<string, string> sourceProfileNames = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ProviderPlaybackProfile> sourcePlaybackProfiles = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string[]> sourceDefaultHiddenGroups = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> lockedGroups = new(StringComparer.OrdinalIgnoreCase);
    private readonly StreamHealthTracker streamHealthTracker = new();
    private readonly PlaylistImportCoordinator playlistImportCoordinator = new();
    private readonly RecentPlaylistSourceManager recentPlaylistSourceManager = new(MaximumRecentPlaylistSources);
    private readonly SourceDefaultVisibilityManager sourceDefaultVisibilityManager = new();
    private readonly Stack<ChannelUndoAction> organizationUndoStack = new();
    private readonly List<EpgProgram> epgPrograms = [];
    private readonly Dictionary<string, List<EpgProgram>> epgProgramsByChannelKey = new(StringComparer.Ordinal);
    private HashSet<Guid> lastRemovedChannelIds = [];
    private Channel[]? pendingRefreshChannels;
    private Channel[] pendingRefreshPreviousChannels = [];
    private PlaylistImportResult? pendingRefreshResult;
    private CancellationTokenSource? refreshScheduleCts;
    private DateTimeOffset? lastPlaylistImportedAt;
    private TimeSpan? lastPlaylistImportDuration;
    private PlaylistImportSummary? lastPlaylistImportSummary;
    private LibraryHealthResourceMetrics? lastLibraryHealthResourceMetrics;

    private Channel? selectedChannel;
    private string searchText = string.Empty;
    private string selectedGroup = AllGroupsOption;
    private string selectedCategory = AllCategoriesOption;
    private string selectedVodYear = AllYearsOption;
    private string selectedCustomGroupAssignment = SourceGroupAssignmentOption;
    private string newCustomGroupName = string.Empty;
    private ContentKind? selectedContentKind;
    private bool favoritesOnly;
    private bool largeLibraryMode;
    private ChannelViewDensity selectedChannelViewDensity = ChannelViewDensity.Comfortable;
    private HiddenChannelFilter selectedHiddenFilter = HiddenChannelFilter.VisibleOnly;
    private ChannelSortMode selectedSortMode = ChannelSortMode.FavoritesFirst;
    private string? selectedManagedCustomGroup;
    private SourceProfileViewModel? selectedSourceProfile;
    private RecentPlaylistSourceViewModel? selectedRecentPlaylistSource;
    private string recentPlaylistSourceName = string.Empty;
    private string renameSourceProfileName = string.Empty;
    private string selectedSourceDefaultVisibilityGroup = AllGroupsOption;
    private string sourceDefaultVisibilitySummaryText = "Default source visibility rules appear after importing a playlist.";
    private string renameCustomGroupName = string.Empty;
    private string selectedBatchGroupAssignment = SourceGroupAssignmentOption;
    private string smartGroupMatchText = string.Empty;
    private string smartGroupName = string.Empty;
    private string smartGroupPresetName = string.Empty;
    private SmartRuleMatchMode selectedSmartRuleMatchMode = SmartRuleMatchMode.ContainsAny;
    private SmartGroupRulePresetViewModel? selectedSmartGroupPreset;
    private string smartGroupPreviewText = "Enter a match term and group name, then preview before applying.";
    private DuplicateChannelGroupViewModel? selectedDuplicateGroup;
    private VodLibraryItemViewModel? selectedVodLibraryItem;
    private HiddenLockedAuditRowViewModel? selectedAuditRow;
    private ChannelFallbackViewModel? selectedChannelFallback;
    private SmartViewFilter selectedSmartView;
    private string epgSearchText = string.Empty;
    private string duplicateAssistantSummaryText = "Duplicate assistant: import a playlist, then refresh duplicate groups.";
    private string vodLibrarySummaryText = "VOD library appears after importing playlists with VOD or series entries.";
    private string auditSummaryText = "Hidden/locked audit appears after importing a playlist.";
    private string fallbackSummaryText = "Fallback streams appear when playlist alternates share the selected channel name.";
    private string pendingRefreshSummaryText = "Refresh approval: no pending refresh.";
    private string searchBenchmarkSummaryText = "Search benchmark has not run.";
    private string libraryHealthSummaryText = "Library health appears after importing a playlist.";
    private string playbackProgressText = "Playback position unavailable.";
    private string conflictReviewText = "Refresh conflicts unavailable until a playlist is refreshed.";
    private string logoPrefetchStatusText = "Logo prefetch idle.";
    private string logoCacheStatusText = "Logo cache: not checked yet.";
    private string streamHealthSummaryText = "Stream health appears after playback attempts.";
    private string epgTimelineSummaryText = "EPG timeline appears after importing XMLTV guide data.";
    private string selectedVodDetailText = "Select VOD or series content to view detail and resume controls.";
    private EpgTimelineWindow selectedEpgTimelineWindow;
    private int vodLibraryPageIndex;
    private string refreshScheduleStatusText = "Provider refresh schedule is off.";
    private bool refreshScheduleEnabled;
    private int selectedRefreshIntervalMinutes = 60;
    private int selectedSourceRetryCount;
    private BufferingPreset selectedSourceBufferingPreset = BufferingPreset.Balanced;
    private string? parentalPinSalt;
    private string? parentalPinHash;
    private bool isParentalUnlocked;
    private string parentalLockStatusText = "Parental lock not configured.";
    private string xmltvGuideUrl = string.Empty;
    private bool autoLoadXmltvOnPlaylistImport;
    private int selectedChannelCount;
    private bool isUpdatingSelectedChannelOrganization;
    private bool isBusy;
    private bool isImporting;
    private string importProgressText = "Import idle.";
    private CancellationTokenSource? importCts;
    private bool isBasicMode;
    private bool firstRunSetupCompleted;
    private int selectedLogoCacheLimitMegabytes = 100;
    private AppTheme selectedAppTheme = AppTheme.Dark;
    private AppUiScale selectedAppUiScale = AppUiScale.Normal;
    private string statusText = "Import a user-provided M3U/M3U8 playlist to begin.";
    private string playbackStatusText = "Playback idle.";
    private string importSummaryText = "No playlist imported yet.";
    private string refreshDiffText = "Refresh diff unavailable until a playlist is imported.";
    private string profileSummaryText = "Profile: automatic per-playlist/source organization will activate after import.";
    private string reconciliationText = "Organization reconciliation unavailable until a playlist is imported.";
    private string epgSummaryText = "No XMLTV guide imported.";
    private string selectedChannelDetails = "Select a channel to view safe details.";
    private string selectedChannelMetadataText = "No channel selected.";
    private string selectedChannelLogoStatusText = "Logo: no channel selected.";
    private string? selectedChannelLogoPath;
    private BufferingPreset selectedBufferingPreset = BufferingPreset.Balanced;
    private Guid? nowPlayingChannelId;
    private int volume = 80;
    private CancellationTokenSource? logoCts;
    private CancellationTokenSource? logoPrefetchCts;
    private DateTimeOffset lastResumeStateSaveAt = DateTimeOffset.MinValue;

    public MainViewModel(
        IPlaylistImportService playlistImportService,
        IChannelSearchService channelSearchService,
        IPlaybackEngine playbackEngine,
        IChannelStateStore channelStateStore,
        IChannelOrganizationPreferencesStore organizationPreferencesStore,
        IChannelOrganizationBackupService organizationBackupService,
        ILogoCacheService logoCacheService,
        IRecentPlaylistSourceFileService recentPlaylistSourceFileService,
        ISourceProfileFileService sourceProfileFileService,
        ISmartGroupPresetFileService smartGroupPresetFileService,
        IUiPreferencesStore uiPreferencesStore,
        IThemeService themeService,
        IXmltvImportService xmltvImportService,
        IPlaylistDialogService dialogService)
    {
        this.playlistImportService = playlistImportService;
        this.channelSearchService = channelSearchService;
        this.playbackEngine = playbackEngine;
        this.channelStateStore = channelStateStore;
        this.organizationPreferencesStore = organizationPreferencesStore;
        this.organizationBackupService = organizationBackupService;
        this.logoCacheService = logoCacheService;
        this.recentPlaylistSourceFileService = recentPlaylistSourceFileService;
        this.sourceProfileFileService = sourceProfileFileService;
        this.smartGroupPresetFileService = smartGroupPresetFileService;
        this.uiPreferencesStore = uiPreferencesStore;
        this.themeService = themeService;
        this.xmltvImportService = xmltvImportService;
        this.dialogService = dialogService;
        Clock = new ClockOverlayViewModel(uiPreferencesStore);

        ImportFileCommand = new AsyncRelayCommand(_ => ImportFileAsync(), _ => !IsBusy);
        ImportUrlCommand = new AsyncRelayCommand(_ => ImportUrlAsync(), _ => !IsBusy);
        LoadSampleCommand = new AsyncRelayCommand(_ => LoadSampleAsync(), _ => !IsBusy);
        OpenRecentPlaylistSourceCommand = new AsyncRelayCommand(_ => OpenRecentPlaylistSourceAsync(), _ => !IsBusy && SelectedRecentPlaylistSource is not null);
        RenameRecentPlaylistSourceCommand = new RelayCommand(_ => RenameRecentPlaylistSource(), _ => SelectedRecentPlaylistSource is not null);
        TogglePinRecentPlaylistSourceCommand = new RelayCommand(_ => TogglePinRecentPlaylistSource(), _ => SelectedRecentPlaylistSource is not null);
        RemoveRecentPlaylistSourceCommand = new RelayCommand(_ => RemoveRecentPlaylistSource(), _ => SelectedRecentPlaylistSource is not null);
        ImportRecentPlaylistSourcesCommand = new AsyncRelayCommand(_ => ImportRecentPlaylistSourcesAsync(), _ => !IsBusy);
        ExportRecentPlaylistSourcesCommand = new AsyncRelayCommand(_ => ExportRecentPlaylistSourcesAsync(), _ => !IsBusy && RecentPlaylistSources.Count > 0);
        ClearRecentPlaylistSourcesCommand = new RelayCommand(_ => ClearRecentPlaylistSources(), _ => RecentPlaylistSources.Count > 0);
        CancelImportCommand = new RelayCommand(_ => CancelImport(), _ => IsImporting);
        RefreshPlaylistCommand = new AsyncRelayCommand(_ => RefreshPlaylistAsync(), _ => !IsBusy && lastPlaylistImport is not null);
        ImportEpgCommand = new AsyncRelayCommand(_ => ImportEpgAsync(), _ => !IsBusy);
        ImportEpgUrlCommand = new AsyncRelayCommand(_ => ImportEpgUrlAsync(), _ => !IsBusy);
        PlaySelectedCommand = new AsyncRelayCommand(_ => PlaySelectedAsync(), _ => SelectedChannel is not null);
        PlaySelectedFallbackCommand = new AsyncRelayCommand(_ => PlaySelectedFallbackAsync(), _ => SelectedChannelFallback is not null);
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
        BatchHideCommand = new RelayCommand(_ => ApplyBatchUpdate(channel => channel with { IsHidden = true }, "hidden", visibilityOverride: true), _ => HasBatchSelection);
        BatchUnhideCommand = new RelayCommand(_ => ApplyBatchUpdate(channel => channel with { IsHidden = false }, "unhidden", visibilityOverride: true), _ => HasBatchSelection);
        BatchAssignGroupCommand = new RelayCommand(_ => AssignBatchGroup(), _ => HasBatchSelection);
        BatchClearGroupCommand = new RelayCommand(_ => ApplyBatchUpdate(channel => channel with { CustomGroup = null }, "removed from custom groups"), _ => HasBatchSelection);
        ImportCustomGroupCsvCommand = new AsyncRelayCommand(_ => ImportCustomGroupCsvAsync(), _ => !IsBusy);
        ExportCustomGroupCsvCommand = new AsyncRelayCommand(_ => ExportCustomGroupCsvAsync(), _ => !IsBusy && allChannels.Count > 0);
        PreviousVodPageCommand = new RelayCommand(_ => MoveVodPage(-1), _ => VodLibraryPageIndex > 0);
        NextVodPageCommand = new RelayCommand(_ => MoveVodPage(1), _ => HasNextVodPage());
        PreviewSmartGroupCommand = new RelayCommand(_ => PreviewSmartGroup());
        ApplySmartGroupCommand = new RelayCommand(_ => ApplySmartGroup());
        SaveSmartGroupPresetCommand = new RelayCommand(_ => SaveSmartGroupPreset());
        UseSmartGroupPresetCommand = new RelayCommand(_ => UseSmartGroupPreset(), _ => SelectedSmartGroupPreset is not null);
        ImportSmartGroupPresetsCommand = new AsyncRelayCommand(_ => ImportSmartGroupPresetsAsync(), _ => !IsBusy);
        ExportSmartGroupPresetsCommand = new AsyncRelayCommand(_ => ExportSmartGroupPresetsAsync(), _ => !IsBusy && SmartGroupPresets.Count > 0);
        RenameSourceProfileCommand = new RelayCommand(_ => RenameSelectedSourceProfile(), _ => SelectedSourceProfile is not null);
        SaveSourcePlaybackProfileCommand = new RelayCommand(_ => SaveSelectedSourcePlaybackProfile(), _ => SelectedSourceProfile is not null);
        ImportSourceProfilesCommand = new AsyncRelayCommand(_ => ImportSourceProfilesAsync(), _ => !IsBusy);
        ExportSourceProfilesCommand = new AsyncRelayCommand(_ => ExportSourceProfilesAsync(), _ => !IsBusy && (sourceProfileNames.Count > 0 || sourcePlaybackProfiles.Count > 0 || sourceDefaultHiddenGroups.Count > 0));
        HideSourceDefaultGroupCommand = new RelayCommand(_ => HideSelectedSourceDefaultGroup(), _ => CanChangeSelectedSourceDefaultVisibilityGroup);
        ShowSourceDefaultGroupCommand = new RelayCommand(_ => ShowSelectedSourceDefaultGroup(), _ => CanChangeSelectedSourceDefaultVisibilityGroup);
        RefreshDuplicateGroupsCommand = new RelayCommand(_ => RefreshDuplicateGroups());
        HideSelectedDuplicateGroupCommand = new RelayCommand(_ => HideSelectedDuplicateGroup(), _ => SelectedDuplicateGroup is not null);
        RefreshAuditCommand = new RelayCommand(_ => RefreshHiddenLockedAudit());
        UnhideAuditGroupCommand = new RelayCommand(_ => UnhideSelectedAuditGroup(), _ => (SelectedAuditRow?.HiddenCount ?? 0) > 0);
        UnlockAuditGroupCommand = new RelayCommand(_ => UnlockSelectedAuditGroup(), _ => SelectedAuditRow?.IsLocked == true && IsParentalUnlocked);
        ApplyPendingRefreshCommand = new AsyncRelayCommand(_ => ApplyPendingRefreshAsync(), _ => !IsBusy && pendingRefreshChannels is not null);
        DiscardPendingRefreshCommand = new RelayCommand(_ => DiscardPendingRefresh(), _ => !IsBusy && pendingRefreshChannels is not null);
        RunSearchBenchmarkCommand = new AsyncRelayCommand(_ => RunSearchBenchmarkAsync(), _ => !IsBusy);
        LockParentalControlsCommand = new RelayCommand(_ => LockParentalControls(), _ => IsParentalLockConfigured);
        LockSelectedGroupCommand = new RelayCommand(_ => LockSelectedGroup(), _ => IsParentalLockConfigured);
        UnlockSelectedGroupCommand = new RelayCommand(_ => UnlockSelectedGroup(), _ => IsParentalUnlocked);
        ClearParentalPinCommand = new RelayCommand(_ => ClearParentalPin(), _ => IsParentalLockConfigured);
        SetResume25Command = new RelayCommand(_ => SetSelectedResumeProgress(25), _ => SelectedChannel is not null);
        SetResume50Command = new RelayCommand(_ => SetSelectedResumeProgress(50), _ => SelectedChannel is not null);
        SetResume75Command = new RelayCommand(_ => SetSelectedResumeProgress(75), _ => SelectedChannel is not null);
        ClearResumeCommand = new RelayCommand(_ => SetSelectedResumeProgress(null), _ => SelectedChannel is not null);
        ClearRemovedConflictStatesCommand = new RelayCommand(_ => ClearRemovedConflictStates(), _ => lastRemovedChannelIds.Count > 0);
        PrefetchVisibleLogosCommand = new AsyncRelayCommand(_ => PrefetchVisibleLogosAsync(), _ => VisibleChannels.Count > 0);
        TrimLogoCacheCommand = new AsyncRelayCommand(_ => TrimLogoCacheAsync(), _ => !IsBusy);
        ClearLogoCacheCommand = new AsyncRelayCommand(_ => ClearLogoCacheAsync(), _ => !IsBusy);
        UndoOrganizationActionCommand = new RelayCommand(_ => UndoLastOrganizationAction(), _ => organizationUndoStack.Count > 0);
        ClearStreamHealthCommand = new RelayCommand(_ => ClearStreamHealth(), _ => StreamHealthRows.Count > 0);
        ExportDiagnosticsCommand = new AsyncRelayCommand(_ => ExportDiagnosticsAsync(), _ => Diagnostics.Count > 0);
        ImportOrganizationCommand = new AsyncRelayCommand(_ => ImportOrganizationAsync(), _ => !IsBusy);
        ExportOrganizationCommand = new AsyncRelayCommand(_ => ExportOrganizationAsync(), _ => !IsBusy);
        ClearFiltersCommand = new RelayCommand(_ => ClearFilters());

        playbackEngine.StateChanged += (_, state) => UiDispatcher.Run(() => ApplyPlaybackState(state));
        playbackEngine.ProgressChanged += (_, progress) => UiDispatcher.Run(() => ApplyPlaybackProgress(progress));
    }

    public RangeObservableCollection<Channel> VisibleChannels { get; } = [];

    public ObservableCollection<string> Groups { get; } = [AllGroupsOption];

    public ObservableCollection<string> Categories { get; } = [AllCategoriesOption];

    public ObservableCollection<string> VodYears { get; } = [AllYearsOption];

    public ObservableCollection<string> CustomGroups { get; } = [];

    public ObservableCollection<CustomGroupSummaryViewModel> CustomGroupSummaries { get; } = [];

    public ObservableCollection<string> CustomGroupAssignments { get; } = [SourceGroupAssignmentOption];

    public ObservableCollection<SourceProfileViewModel> SourceProfiles { get; } = [];

    public ObservableCollection<RecentPlaylistSourceViewModel> RecentPlaylistSources { get; } = [];

    public ObservableCollection<string> SourceDefaultVisibilityGroups { get; } = [AllGroupsOption];

    public ObservableCollection<LibraryHealthMetricViewModel> LibraryHealthMetrics { get; } = [];

    public ObservableCollection<SmartGroupRulePresetViewModel> SmartGroupPresets { get; } = [];

    public ObservableCollection<VodLibraryItemViewModel> VodLibraryItems { get; } = [];

    public ObservableCollection<DuplicateChannelGroupViewModel> DuplicateChannelGroups { get; } = [];

    public ObservableCollection<HiddenLockedAuditRowViewModel> HiddenLockedAuditRows { get; } = [];

    public ObservableCollection<ChannelFallbackViewModel> ChannelFallbacks { get; } = [];

    public ObservableCollection<RefreshApprovalChangeViewModel> PendingRefreshChanges { get; } = [];

    public ObservableCollection<SearchBenchmarkResultViewModel> SearchBenchmarkResults { get; } = [];

    public ObservableCollection<PlaylistRefreshConflictViewModel> RefreshConflicts { get; } = [];

    public ObservableCollection<EpgProgramViewModel> SelectedChannelEpgPrograms { get; } = [];

    public ObservableCollection<EpgTimelineRowViewModel> EpgTimelineRows { get; } = [];

    public ObservableCollection<StreamHealthViewModel> StreamHealthRows { get; } = [];

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

    public IReadOnlyList<UiSelectionOption<ContentKind?>> ContentKindFilterOptions { get; } =
    [
        new(null, "All content"),
        new(ContentKind.LiveTv, "Live TV"),
        new(ContentKind.Vod, "VOD"),
        new(ContentKind.Series, "Series"),
        new(ContentKind.Radio, "Radio"),
        new(ContentKind.Unknown, "Unknown")
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

    public IReadOnlyList<UiSelectionOption<ChannelViewDensity>> ChannelViewDensityOptions { get; } =
    [
        new(ChannelViewDensity.Comfortable, "Comfortable"),
        new(ChannelViewDensity.Compact, "Compact"),
        new(ChannelViewDensity.Dense, "Dense")
    ];

    public IReadOnlyList<UiSelectionOption<SmartRuleMatchMode>> SmartRuleMatchModeOptions { get; } =
    [
        new(SmartRuleMatchMode.ContainsAny, "Contains any field"),
        new(SmartRuleMatchMode.NameStartsWith, "Name starts with"),
        new(SmartRuleMatchMode.Regex, "Regex"),
        new(SmartRuleMatchMode.GroupEquals, "Group equals"),
        new(SmartRuleMatchMode.CategoryEquals, "Category equals")
    ];

    public IReadOnlyList<int> RefreshIntervalMinuteOptions { get; } = [5, 15, 30, 60, 180, 360, 720, 1440];

    public IReadOnlyList<int> RetryCountOptions { get; } = [0, 1, 2, 3];

    public IReadOnlyList<int> LogoCacheLimitMegabyteOptions { get; } = [25, 50, 100, 250, 500];

    public IReadOnlyList<UiSelectionOption<AppTheme>> AppThemeOptions { get; } =
    [
        new(AppTheme.Dark, "Dark"),
        new(AppTheme.Light, "Light"),
        new(AppTheme.HighContrast, "High contrast")
    ];

    public IReadOnlyList<UiSelectionOption<AppUiScale>> AppUiScaleOptions { get; } =
    [
        new(AppUiScale.Normal, "Normal"),
        new(AppUiScale.Large, "Large"),
        new(AppUiScale.Tv, "TV distance")
    ];

    public IReadOnlyList<UiSelectionOption<SmartViewFilter>> SmartViewOptions { get; } =
    [
        new(SmartViewFilter.All, "All channels"),
        new(SmartViewFilter.UnwatchedMovies, "Unwatched movies"),
        new(SmartViewFilter.RecentlyAdded, "Recently added"),
        new(SmartViewFilter.FavoritesByGroup, "Favorites by group")
    ];

    public IReadOnlyList<UiSelectionOption<EpgTimelineWindow>> EpgTimelineWindowOptions { get; } =
    [
        new(EpgTimelineWindow.Now, "Now"),
        new(EpgTimelineWindow.PlusTwoHours, "+2 hours"),
        new(EpgTimelineWindow.Tonight, "Tonight"),
        new(EpgTimelineWindow.Tomorrow, "Tomorrow")
    ];

    public ICommand ImportFileCommand { get; }

    public ICommand ImportUrlCommand { get; }

    public ICommand LoadSampleCommand { get; }

    public ICommand OpenRecentPlaylistSourceCommand { get; }

    public ICommand RenameRecentPlaylistSourceCommand { get; }

    public ICommand TogglePinRecentPlaylistSourceCommand { get; }

    public ICommand RemoveRecentPlaylistSourceCommand { get; }

    public ICommand ImportRecentPlaylistSourcesCommand { get; }

    public ICommand ExportRecentPlaylistSourcesCommand { get; }

    public ICommand ClearRecentPlaylistSourcesCommand { get; }

    public ICommand CancelImportCommand { get; }

    public ICommand RefreshPlaylistCommand { get; }

    public ICommand ImportEpgCommand { get; }

    public ICommand ImportEpgUrlCommand { get; }

    public ICommand PlaySelectedCommand { get; }

    public ICommand PlaySelectedFallbackCommand { get; }

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

    public ICommand ImportCustomGroupCsvCommand { get; }

    public ICommand ExportCustomGroupCsvCommand { get; }

    public ICommand PreviousVodPageCommand { get; }

    public ICommand NextVodPageCommand { get; }

    public ICommand PreviewSmartGroupCommand { get; }

    public ICommand ApplySmartGroupCommand { get; }

    public ICommand SaveSmartGroupPresetCommand { get; }

    public ICommand UseSmartGroupPresetCommand { get; }

    public ICommand ImportSmartGroupPresetsCommand { get; }

    public ICommand ExportSmartGroupPresetsCommand { get; }

    public ICommand RenameSourceProfileCommand { get; }

    public ICommand SaveSourcePlaybackProfileCommand { get; }

    public ICommand ImportSourceProfilesCommand { get; }

    public ICommand ExportSourceProfilesCommand { get; }

    public ICommand HideSourceDefaultGroupCommand { get; }

    public ICommand ShowSourceDefaultGroupCommand { get; }

    public ICommand RefreshDuplicateGroupsCommand { get; }

    public ICommand HideSelectedDuplicateGroupCommand { get; }

    public ICommand RefreshAuditCommand { get; }

    public ICommand UnhideAuditGroupCommand { get; }

    public ICommand UnlockAuditGroupCommand { get; }

    public ICommand ApplyPendingRefreshCommand { get; }

    public ICommand DiscardPendingRefreshCommand { get; }

    public ICommand RunSearchBenchmarkCommand { get; }

    public ICommand LockParentalControlsCommand { get; }

    public ICommand LockSelectedGroupCommand { get; }

    public ICommand UnlockSelectedGroupCommand { get; }

    public ICommand ClearParentalPinCommand { get; }

    public ICommand SetResume25Command { get; }

    public ICommand SetResume50Command { get; }

    public ICommand SetResume75Command { get; }

    public ICommand ClearResumeCommand { get; }

    public ICommand ClearRemovedConflictStatesCommand { get; }

    public ICommand PrefetchVisibleLogosCommand { get; }

    public ICommand TrimLogoCacheCommand { get; }

    public ICommand ClearLogoCacheCommand { get; }

    public ICommand UndoOrganizationActionCommand { get; }

    public ICommand ClearStreamHealthCommand { get; }

    public ICommand ExportDiagnosticsCommand { get; }

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
                SelectedChannelMetadataText = value is null
                    ? "No channel selected."
                    : FormatSelectedChannelMetadata(value);
                SelectedVodDetailText = value is null
                    ? "Select VOD or series content to view detail and resume controls."
                    : FormatVodDetail(value);
                QueueLogoLoad(value);
                RefreshSelectedChannelEpgGuide();
                RefreshSelectedChannelFallbacks();
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
                OnPropertyChanged(nameof(ResumeProgressText));
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

    public string SelectedVodYear
    {
        get => selectedVodYear;
        set
        {
            string nextValue = string.IsNullOrWhiteSpace(value) ? AllYearsOption : value;
            if (SetProperty(ref selectedVodYear, nextValue))
            {
                ScheduleSearch();
            }
        }
    }

    public ContentKind? SelectedContentKind
    {
        get => selectedContentKind;
        set
        {
            if (SetProperty(ref selectedContentKind, value))
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

    public bool LargeLibraryMode
    {
        get => largeLibraryMode;
        set
        {
            if (SetProperty(ref largeLibraryMode, value))
            {
                OnPropertyChanged(nameof(VisibleResultLimitText));
                ScheduleSearch();
                _ = SaveOrganizationPreferencesSafelyAsync();
            }
        }
    }

    public string VisibleResultLimitText => LargeLibraryMode
        ? $"Large library mode: compact rows, first {LargeLibraryVisibleChannelResults:N0} visible results."
        : $"Standard mode: first {StandardVisibleChannelResults:N0} visible results.";

    public ChannelViewDensity SelectedChannelViewDensity
    {
        get => selectedChannelViewDensity;
        set
        {
            if (!Enum.IsDefined(value))
            {
                value = ChannelViewDensity.Comfortable;
            }

            if (SetProperty(ref selectedChannelViewDensity, value))
            {
                _ = SaveOrganizationPreferencesSafelyAsync();
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

    public SourceProfileViewModel? SelectedSourceProfile
    {
        get => selectedSourceProfile;
        set
        {
            if (SetProperty(ref selectedSourceProfile, value))
            {
                RenameSourceProfileName = value?.DisplayName ?? string.Empty;
                LoadSelectedSourcePlaybackProfile(value?.SourceId);
                RefreshSourceDefaultVisibilityOptions();
                RaiseProfileCommandStates();
            }
        }
    }

    public RecentPlaylistSourceViewModel? SelectedRecentPlaylistSource
    {
        get => selectedRecentPlaylistSource;
        set
        {
            if (SetProperty(ref selectedRecentPlaylistSource, value))
            {
                RecentPlaylistSourceName = value?.DisplayName ?? string.Empty;
                OnPropertyChanged(nameof(PinRecentPlaylistSourceLabel));
                RaiseRecentPlaylistCommandStates();
            }
        }
    }

    public string RecentPlaylistSourceName
    {
        get => recentPlaylistSourceName;
        set => SetProperty(ref recentPlaylistSourceName, value);
    }

    public string PinRecentPlaylistSourceLabel => SelectedRecentPlaylistSource?.IsPinned == true ? "Unpin" : "Pin";

    public string RenameSourceProfileName
    {
        get => renameSourceProfileName;
        set => SetProperty(ref renameSourceProfileName, value);
    }

    public string SelectedSourceDefaultVisibilityGroup
    {
        get => selectedSourceDefaultVisibilityGroup;
        set
        {
            string nextValue = string.IsNullOrWhiteSpace(value) ? AllGroupsOption : value;
            if (SetProperty(ref selectedSourceDefaultVisibilityGroup, nextValue))
            {
                RefreshSourceDefaultVisibilitySummary();
                RaiseSourceDefaultVisibilityCommandStates();
            }
        }
    }

    public string SourceDefaultVisibilitySummaryText
    {
        get => sourceDefaultVisibilitySummaryText;
        private set => SetProperty(ref sourceDefaultVisibilitySummaryText, value);
    }

    private bool CanChangeSelectedSourceDefaultVisibilityGroup =>
        SelectedSourceProfile is not null &&
        !string.Equals(SelectedSourceDefaultVisibilityGroup, AllGroupsOption, StringComparison.OrdinalIgnoreCase);

    public int SelectedSourceRetryCount
    {
        get => selectedSourceRetryCount;
        set => SetProperty(ref selectedSourceRetryCount, Math.Clamp(value, 0, 3));
    }

    public BufferingPreset SelectedSourceBufferingPreset
    {
        get => selectedSourceBufferingPreset;
        set
        {
            if (!Enum.IsDefined(value))
            {
                value = BufferingPreset.Balanced;
            }

            SetProperty(ref selectedSourceBufferingPreset, value);
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

    public string SmartGroupMatchText
    {
        get => smartGroupMatchText;
        set => SetProperty(ref smartGroupMatchText, value);
    }

    public string SmartGroupName
    {
        get => smartGroupName;
        set => SetProperty(ref smartGroupName, value);
    }

    public string SmartGroupPresetName
    {
        get => smartGroupPresetName;
        set => SetProperty(ref smartGroupPresetName, value);
    }

    public SmartRuleMatchMode SelectedSmartRuleMatchMode
    {
        get => selectedSmartRuleMatchMode;
        set
        {
            if (!Enum.IsDefined(value))
            {
                value = SmartRuleMatchMode.ContainsAny;
            }

            SetProperty(ref selectedSmartRuleMatchMode, value);
        }
    }

    public SmartGroupRulePresetViewModel? SelectedSmartGroupPreset
    {
        get => selectedSmartGroupPreset;
        set
        {
            if (SetProperty(ref selectedSmartGroupPreset, value) &&
                UseSmartGroupPresetCommand is RelayCommand usePreset)
            {
                usePreset.RaiseCanExecuteChanged();
            }
        }
    }

    public string SmartGroupPreviewText
    {
        get => smartGroupPreviewText;
        private set => SetProperty(ref smartGroupPreviewText, value);
    }

    public DuplicateChannelGroupViewModel? SelectedDuplicateGroup
    {
        get => selectedDuplicateGroup;
        set
        {
            if (SetProperty(ref selectedDuplicateGroup, value) &&
                HideSelectedDuplicateGroupCommand is RelayCommand hide)
            {
                hide.RaiseCanExecuteChanged();
            }
        }
    }

    public VodLibraryItemViewModel? SelectedVodLibraryItem
    {
        get => selectedVodLibraryItem;
        set
        {
            if (SetProperty(ref selectedVodLibraryItem, value) && value is not null)
            {
                SelectedChannel = allChannels.FirstOrDefault(channel => channel.Id == value.ChannelId);
            }
        }
    }

    public HiddenLockedAuditRowViewModel? SelectedAuditRow
    {
        get => selectedAuditRow;
        set
        {
            if (SetProperty(ref selectedAuditRow, value))
            {
                RaiseAuditCommandStates();
            }
        }
    }

    public ChannelFallbackViewModel? SelectedChannelFallback
    {
        get => selectedChannelFallback;
        set
        {
            if (SetProperty(ref selectedChannelFallback, value) &&
                PlaySelectedFallbackCommand is AsyncRelayCommand playFallback)
            {
                playFallback.RaiseCanExecuteChanged();
            }
        }
    }

    public SmartViewFilter SelectedSmartView
    {
        get => selectedSmartView;
        set
        {
            if (!Enum.IsDefined(value))
            {
                value = SmartViewFilter.All;
            }

            if (SetProperty(ref selectedSmartView, value))
            {
                ScheduleSearch();
            }
        }
    }

    public string EpgSearchText
    {
        get => epgSearchText;
        set
        {
            if (SetProperty(ref epgSearchText, value ?? string.Empty))
            {
                RefreshSelectedChannelEpgGuide();
                RefreshEpgTimeline();
            }
        }
    }

    public string DuplicateAssistantSummaryText
    {
        get => duplicateAssistantSummaryText;
        private set => SetProperty(ref duplicateAssistantSummaryText, value);
    }

    public string VodLibrarySummaryText
    {
        get => vodLibrarySummaryText;
        private set => SetProperty(ref vodLibrarySummaryText, value);
    }

    public string AuditSummaryText
    {
        get => auditSummaryText;
        private set => SetProperty(ref auditSummaryText, value);
    }

    public string FallbackSummaryText
    {
        get => fallbackSummaryText;
        private set => SetProperty(ref fallbackSummaryText, value);
    }

    public string PendingRefreshSummaryText
    {
        get => pendingRefreshSummaryText;
        private set => SetProperty(ref pendingRefreshSummaryText, value);
    }

    public string SearchBenchmarkSummaryText
    {
        get => searchBenchmarkSummaryText;
        private set => SetProperty(ref searchBenchmarkSummaryText, value);
    }

    public string PlaybackProgressText
    {
        get => playbackProgressText;
        private set => SetProperty(ref playbackProgressText, value);
    }

    public int VodLibraryPageIndex
    {
        get => vodLibraryPageIndex;
        private set
        {
            if (SetProperty(ref vodLibraryPageIndex, Math.Max(0, value)))
            {
                RaiseVodPageCommandStates();
            }
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
                RaiseProfileCommandStates();
                RaiseRecentPlaylistCommandStates();
                RaisePendingRefreshCommandStates();
            }
        }
    }

    public bool IsImporting
    {
        get => isImporting;
        private set
        {
            if (SetProperty(ref isImporting, value))
            {
                if (CancelImportCommand is RelayCommand cancel)
                {
                    cancel.RaiseCanExecuteChanged();
                }
            }
        }
    }

    public string ImportProgressText
    {
        get => importProgressText;
        private set => SetProperty(ref importProgressText, value);
    }

    public bool IsBasicMode
    {
        get => isBasicMode;
        set
        {
            if (SetProperty(ref isBasicMode, value))
            {
                OnPropertyChanged(nameof(IsAdvancedModeVisible));
                StatusText = value
                    ? "Basic mode enabled: advanced organization, EPG, VOD, diagnostics, and release panels are hidden."
                    : "Advanced mode enabled: full IPTV organization and diagnostics panels are visible.";
                _ = SaveUiPreferencesSafelyAsync();
            }
        }
    }

    public bool IsAdvancedModeVisible => !IsBasicMode;

    public bool FirstRunSetupCompleted
    {
        get => firstRunSetupCompleted;
        private set
        {
            if (SetProperty(ref firstRunSetupCompleted, value))
            {
                OnPropertyChanged(nameof(ShouldShowFirstRunSetup));
            }
        }
    }

    public bool ShouldShowFirstRunSetup => !FirstRunSetupCompleted && !HasChannels;

    public bool HasChannels => allChannels.Count > 0;

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

    public string ProfileSummaryText
    {
        get => profileSummaryText;
        private set => SetProperty(ref profileSummaryText, value);
    }

    public string ReconciliationText
    {
        get => reconciliationText;
        private set => SetProperty(ref reconciliationText, value);
    }

    public string ConflictReviewText
    {
        get => conflictReviewText;
        private set => SetProperty(ref conflictReviewText, value);
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

    public string SelectedChannelMetadataText
    {
        get => selectedChannelMetadataText;
        private set => SetProperty(ref selectedChannelMetadataText, value);
    }

    public string SelectedChannelLogoStatusText
    {
        get => selectedChannelLogoStatusText;
        private set => SetProperty(ref selectedChannelLogoStatusText, value);
    }

    public string? SelectedChannelLogoPath
    {
        get => selectedChannelLogoPath;
        private set => SetProperty(ref selectedChannelLogoPath, value);
    }

    public string LogoPrefetchStatusText
    {
        get => logoPrefetchStatusText;
        private set => SetProperty(ref logoPrefetchStatusText, value);
    }

    public string LogoCacheStatusText
    {
        get => logoCacheStatusText;
        private set => SetProperty(ref logoCacheStatusText, value);
    }

    public string LibraryHealthSummaryText
    {
        get => libraryHealthSummaryText;
        private set => SetProperty(ref libraryHealthSummaryText, value);
    }

    public int SelectedLogoCacheLimitMegabytes
    {
        get => selectedLogoCacheLimitMegabytes;
        set
        {
            int normalized = NormalizeLogoCacheLimit(value);
            if (SetProperty(ref selectedLogoCacheLimitMegabytes, normalized))
            {
                _ = SaveUiPreferencesSafelyAsync();
                RefreshLogoCacheStatus();
            }
        }
    }

    public AppTheme SelectedAppTheme
    {
        get => selectedAppTheme;
        set
        {
            AppTheme normalized = ThemeService.NormalizeTheme(value);
            if (SetProperty(ref selectedAppTheme, normalized))
            {
                themeService.ApplyTheme(normalized, SelectedAppUiScale);
                _ = SaveUiPreferencesSafelyAsync();
            }
        }
    }

    public AppUiScale SelectedAppUiScale
    {
        get => selectedAppUiScale;
        set
        {
            AppUiScale normalized = ThemeService.NormalizeUiScale(value);
            if (SetProperty(ref selectedAppUiScale, normalized))
            {
                themeService.ApplyTheme(SelectedAppTheme, normalized);
                _ = SaveUiPreferencesSafelyAsync();
            }
        }
    }

    public string StreamHealthSummaryText
    {
        get => streamHealthSummaryText;
        private set => SetProperty(ref streamHealthSummaryText, value);
    }

    public string EpgTimelineSummaryText
    {
        get => epgTimelineSummaryText;
        private set => SetProperty(ref epgTimelineSummaryText, value);
    }

    public string SelectedVodDetailText
    {
        get => selectedVodDetailText;
        private set => SetProperty(ref selectedVodDetailText, value);
    }

    public EpgTimelineWindow SelectedEpgTimelineWindow
    {
        get => selectedEpgTimelineWindow;
        set
        {
            if (!Enum.IsDefined(value))
            {
                value = EpgTimelineWindow.Now;
            }

            if (SetProperty(ref selectedEpgTimelineWindow, value))
            {
                RefreshEpgTimeline();
            }
        }
    }

    public string ResumeProgressText => SelectedChannel?.ResumeProgressPercent is int progress
        ? $"Resume progress: {progress}%"
        : "Resume progress: not set";

    public bool RefreshScheduleEnabled
    {
        get => refreshScheduleEnabled;
        set
        {
            if (SetProperty(ref refreshScheduleEnabled, value))
            {
                RestartRefreshScheduleLoop();
                _ = SaveOrganizationPreferencesSafelyAsync();
            }
        }
    }

    public int SelectedRefreshIntervalMinutes
    {
        get => selectedRefreshIntervalMinutes;
        set
        {
            int normalized = Math.Clamp(value <= 0 ? 60 : value, 5, 24 * 60);
            if (SetProperty(ref selectedRefreshIntervalMinutes, normalized))
            {
                RestartRefreshScheduleLoop();
                _ = SaveOrganizationPreferencesSafelyAsync();
            }
        }
    }

    public string RefreshScheduleStatusText
    {
        get => refreshScheduleStatusText;
        private set => SetProperty(ref refreshScheduleStatusText, value);
    }

    public bool IsParentalLockConfigured => parentalPinSalt is not null && parentalPinHash is not null;

    public bool IsParentalUnlocked
    {
        get => isParentalUnlocked;
        private set
        {
            if (SetProperty(ref isParentalUnlocked, value))
            {
                OnPropertyChanged(nameof(IsParentalLockConfigured));
                RefreshParentalLockCommandStates();
                RaiseAuditCommandStates();
                ScheduleSearch();
            }
        }
    }

    public string ParentalLockStatusText
    {
        get => parentalLockStatusText;
        private set => SetProperty(ref parentalLockStatusText, value);
    }

    public string XmltvGuideUrl
    {
        get => xmltvGuideUrl;
        set
        {
            if (SetProperty(ref xmltvGuideUrl, value))
            {
                _ = SaveOrganizationPreferencesSafelyAsync();
            }
        }
    }

    public bool AutoLoadXmltvOnPlaylistImport
    {
        get => autoLoadXmltvOnPlaylistImport;
        set
        {
            if (SetProperty(ref autoLoadXmltvOnPlaylistImport, value))
            {
                _ = SaveOrganizationPreferencesSafelyAsync();
            }
        }
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
        private set
        {
            if (SetProperty(ref nowPlayingChannelId, value))
            {
                OnPropertyChanged(nameof(IsVideoPlaceholderVisible));
                OnPropertyChanged(nameof(IsVideoSurfaceVisible));
            }
        }
    }

    public bool IsVideoPlaceholderVisible => NowPlayingChannelId is null;

    public bool IsVideoSurfaceVisible => !IsVideoPlaceholderVisible;

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
        UiPreferences uiPreferences = await uiPreferencesStore.LoadAsync(shutdownCts.Token).ConfigureAwait(true);
        isBasicMode = uiPreferences.IsBasicMode;
        OnPropertyChanged(nameof(IsBasicMode));
        OnPropertyChanged(nameof(IsAdvancedModeVisible));
        firstRunSetupCompleted = uiPreferences.FirstRunSetupCompleted;
        OnPropertyChanged(nameof(FirstRunSetupCompleted));
        OnPropertyChanged(nameof(ShouldShowFirstRunSetup));
        selectedLogoCacheLimitMegabytes = NormalizeLogoCacheLimit(uiPreferences.LogoCacheLimitMegabytes);
        OnPropertyChanged(nameof(SelectedLogoCacheLimitMegabytes));
        selectedAppTheme = ThemeService.NormalizeTheme(uiPreferences.AppTheme);
        selectedAppUiScale = ThemeService.NormalizeUiScale(uiPreferences.AppUiScale);
        themeService.ApplyTheme(selectedAppTheme, selectedAppUiScale);
        OnPropertyChanged(nameof(SelectedAppTheme));
        OnPropertyChanged(nameof(SelectedAppUiScale));
        ApplyRecentPlaylistSources(uiPreferences.RecentPlaylistSources);
        RefreshLogoCacheStatus();
        RefreshLibraryHealth();

        ChannelOrganizationPreferences preferences = await organizationPreferencesStore
            .LoadAsync(shutdownCts.Token)
            .ConfigureAwait(true);
        selectedSortMode = Enum.IsDefined(preferences.SortMode)
            ? preferences.SortMode
            : ChannelSortMode.FavoritesFirst;
        OnPropertyChanged(nameof(SelectedSortMode));
        largeLibraryMode = preferences.LargeLibraryMode;
        OnPropertyChanged(nameof(LargeLibraryMode));
        OnPropertyChanged(nameof(VisibleResultLimitText));
        selectedChannelViewDensity = Enum.IsDefined(preferences.ChannelViewDensity)
            ? preferences.ChannelViewDensity
            : ChannelViewDensity.Comfortable;
        OnPropertyChanged(nameof(SelectedChannelViewDensity));
        sourceProfileNames.Clear();
        foreach ((string sourceId, string profileName) in preferences.SourceProfileNames)
        {
            if (!string.IsNullOrWhiteSpace(sourceId) && !string.IsNullOrWhiteSpace(profileName))
            {
                sourceProfileNames[sourceId] = profileName;
            }
        }

        sourcePlaybackProfiles.Clear();
        foreach ((string sourceId, ProviderPlaybackProfile profile) in preferences.SourcePlaybackProfiles)
        {
            if (!string.IsNullOrWhiteSpace(sourceId))
            {
                sourcePlaybackProfiles[sourceId] = NormalizePlaybackProfile(profile);
            }
        }

        ApplySourceDefaultHiddenGroups(preferences.SourceDefaultHiddenGroups);

        selectedRefreshIntervalMinutes = NormalizeRefreshInterval(preferences.RefreshIntervalMinutes);
        OnPropertyChanged(nameof(SelectedRefreshIntervalMinutes));
        refreshScheduleEnabled = preferences.RefreshScheduleEnabled;
        OnPropertyChanged(nameof(RefreshScheduleEnabled));

        parentalPinSalt = NormalizeSecret(preferences.ParentalPinSalt);
        parentalPinHash = NormalizeSecret(preferences.ParentalPinHash);
        lockedGroups.Clear();
        foreach (string group in preferences.LockedGroups
                     .Select(NormalizeCustomGroup)
                     .Where(group => group is not null)
                     .Select(group => group!))
        {
            lockedGroups.Add(group);
        }

        IsParentalUnlocked = !IsParentalLockConfigured;
        UpdateParentalLockStatus();
        xmltvGuideUrl = preferences.XmltvGuideUrl ?? string.Empty;
        OnPropertyChanged(nameof(XmltvGuideUrl));
        autoLoadXmltvOnPlaylistImport = preferences.AutoLoadXmltvOnPlaylistImport;
        OnPropertyChanged(nameof(AutoLoadXmltvOnPlaylistImport));

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

        RestartRefreshScheduleLoop();
        RaiseProfileCommandStates();
        RaiseRecentPlaylistCommandStates();
    }

    public async Task ImportPlaylistUrlAsync(string playlistUrl)
    {
        if (string.IsNullOrWhiteSpace(playlistUrl))
        {
            return;
        }

        string trimmedUrl = playlistUrl.Trim();
        PlaylistImportOperation import =
            (ct, progress) => playlistImportService.ImportUrlAsync(trimmedUrl, ct, progress);
        await ImportAsync(
            import,
            rememberForRefresh: true,
            recentSource: CreateRecentPlaylistSource(RecentPlaylistSourceKind.RemoteUrl, trimmedUrl, null)).ConfigureAwait(true);
    }

    public async Task ImportPlaylistFileAsync(string playlistPath)
    {
        if (string.IsNullOrWhiteSpace(playlistPath))
        {
            return;
        }

        string trimmedPath = playlistPath.Trim();
        PlaylistImportOperation import =
            (ct, progress) => playlistImportService.ImportFileAsync(trimmedPath, ct, progress);
        await ImportAsync(
            import,
            rememberForRefresh: true,
            recentSource: CreateRecentPlaylistSource(RecentPlaylistSourceKind.LocalFile, trimmedPath, null)).ConfigureAwait(true);
    }

    public void MarkFirstRunSetupCompleted()
    {
        FirstRunSetupCompleted = true;
        _ = SaveUiPreferencesSafelyAsync();
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

    public bool CanDropChannelOn(Channel? draggedChannel, Channel? targetChannel)
    {
        if (draggedChannel is null ||
            targetChannel is null ||
            draggedChannel.Id == Guid.Empty ||
            targetChannel.Id == Guid.Empty ||
            draggedChannel.Id == targetChannel.Id)
        {
            return false;
        }

        return draggedChannel.EffectiveGroupTitle.Equals(targetChannel.EffectiveGroupTitle, StringComparison.OrdinalIgnoreCase);
    }

    public void MoveChannelBefore(Guid draggedChannelId, Guid targetChannelId)
    {
        if (draggedChannelId == Guid.Empty || targetChannelId == Guid.Empty || draggedChannelId == targetChannelId)
        {
            return;
        }

        Channel? dragged = allChannels.FirstOrDefault(channel => channel.Id == draggedChannelId);
        Channel? target = allChannels.FirstOrDefault(channel => channel.Id == targetChannelId);
        if (dragged is null || target is null)
        {
            StatusText = "Drag reorder failed because the channel list changed.";
            return;
        }

        if (!CanDropChannelOn(dragged, target))
        {
            StatusText = "Drag reorder only works within the same effective group.";
            return;
        }

        string group = dragged.EffectiveGroupTitle;
        List<Channel> orderedGroupChannels = allChannels
            .Where(channel => channel.EffectiveGroupTitle.Equals(group, StringComparison.OrdinalIgnoreCase))
            .OrderBy(GetCustomOrderIndex)
            .ThenBy(channel => channel.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        orderedGroupChannels.RemoveAll(channel => channel.Id == draggedChannelId);
        int targetIndex = orderedGroupChannels.FindIndex(channel => channel.Id == targetChannelId);
        if (targetIndex < 0)
        {
            StatusText = "Drag reorder failed because the target channel is no longer visible.";
            return;
        }

        orderedGroupChannels.Insert(targetIndex, dragged);
        PushOrganizationUndo($"drag reorder '{dragged.DisplayName}'", orderedGroupChannels);
        Dictionary<Guid, int> newSortIndexes = orderedGroupChannels
            .Select((channel, index) => new { channel.Id, SortIndex = index * 10 })
            .ToDictionary(entry => entry.Id, entry => entry.SortIndex);

        int changed = 0;
        for (int index = 0; index < allChannels.Count; index++)
        {
            Channel channel = allChannels[index];
            if (!newSortIndexes.TryGetValue(channel.Id, out int sortIndex) || channel.CustomSortIndex == sortIndex)
            {
                continue;
            }

            Channel updated = channel with { CustomSortIndex = sortIndex };
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
            StatusText = "Drag reorder made no changes.";
            return;
        }

        SelectedSortMode = ChannelSortMode.CustomOrder;
        RefreshGroupsAndCategories();
        ScheduleSearch();
        _ = SaveChannelStatesSafelyAsync();
        StatusText = $"Moved '{dragged.DisplayName}' before '{target.DisplayName}' in '{group}'.";
    }

    public void AssignDraggedChannelsToCustomGroup(Guid draggedChannelId, string? targetGroup)
    {
        string? normalizedGroup = NormalizeCustomGroup(targetGroup);
        if (draggedChannelId == Guid.Empty || normalizedGroup is null)
        {
            StatusText = "Drop a channel on a custom group to assign it.";
            return;
        }

        HashSet<Guid> targetIds = selectedChannelIds.Contains(draggedChannelId)
            ? selectedChannelIds.ToHashSet()
            : [draggedChannelId];
        Channel[] affected = allChannels.Where(channel => targetIds.Contains(channel.Id)).ToArray();
        if (affected.Length == 0)
        {
            StatusText = "Group drop failed because the dragged channel is no longer loaded.";
            return;
        }

        PushOrganizationUndo($"drag assign to '{normalizedGroup}'", affected);
        int changed = 0;
        for (int index = 0; index < allChannels.Count; index++)
        {
            Channel channel = allChannels[index];
            if (!targetIds.Contains(channel.Id) ||
                string.Equals(channel.CustomGroup, normalizedGroup, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            Channel updated = channel with { CustomGroup = normalizedGroup };
            allChannels[index] = updated;
            UpdateChannelStateIndex(updated);
            changed++;
            if (SelectedChannel?.Id == updated.Id)
            {
                SelectedChannel = updated;
            }
        }

        knownCustomGroups.Add(normalizedGroup);
        RefreshGroupsAndCategories();
        RefreshVodLibrary();
        ScheduleSearch();
        _ = SaveChannelStatesSafelyAsync();
        _ = SaveOrganizationPreferencesSafelyAsync();
        StatusText = $"Assigned {changed:N0} channel(s) to custom group '{normalizedGroup}' by drag/drop.";
    }

    public async ValueTask DisposeAsync()
    {
        shutdownCts.Cancel();
        importCts?.Cancel();
        importCts?.Dispose();
        searchCts?.Cancel();
        searchCts?.Dispose();
        refreshScheduleCts?.Cancel();
        refreshScheduleCts?.Dispose();
        logoCts?.Cancel();
        logoCts?.Dispose();
        logoPrefetchCts?.Cancel();
        logoPrefetchCts?.Dispose();
        Clock.Dispose();
        logoHttpClient.Dispose();
        xmltvHttpClient.Dispose();
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

            await ImportPlaylistFileAsync(path).ConfigureAwait(true);
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

            PlaylistImportOperation import = (ct, progress) => playlistImportService.ImportFileAsync(samplePath, ct, progress);
            await ImportAsync(
                import,
                rememberForRefresh: true,
                recentSource: CreateRecentPlaylistSource(RecentPlaylistSourceKind.LocalFile, samplePath, "Bundled sample playlist")).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            ShowSafeError("Sample import failed", ex);
        }
    }

    private async Task ImportAsync(
        PlaylistImportOperation import,
        bool rememberForRefresh,
        RecentPlaylistSourceViewModel? recentSource = null)
    {
        CancellationTokenSource? activeImportCts = null;
        try
        {
            IsBusy = true;
            IsImporting = true;
            importCts?.Dispose();
            importCts = CancellationTokenSource.CreateLinkedTokenSource(shutdownCts.Token);
            activeImportCts = importCts;
            CancellationToken importToken = activeImportCts.Token;

            ImportProgressText = "Starting playlist import...";
            StatusText = "Importing playlist...";
            AddDiagnostic("Playlist import started.");
            Channel[] previousChannels = allChannels.ToArray();
            HashSet<Guid> previousIds = previousChannels.Select(channel => channel.Id).ToHashSet();
            var progress = new Progress<PlaylistImportProgress>(ApplyPlaylistImportProgress);
            long managedMemoryBefore = GC.GetTotalMemory(forceFullCollection: false);
            int gen0Before = GC.CollectionCount(0);
            int gen1Before = GC.CollectionCount(1);
            int gen2Before = GC.CollectionCount(2);
            PlaylistImportExecution execution = await playlistImportCoordinator.RunAsync(import, importToken, progress).ConfigureAwait(true);
            PlaylistImportResult result = execution.Result;
            lastPlaylistImportDuration = execution.Duration;
            lastLibraryHealthResourceMetrics = new LibraryHealthResourceMetrics(
                managedMemoryBefore,
                GC.GetTotalMemory(forceFullCollection: false),
                GC.CollectionCount(0) - gen0Before,
                GC.CollectionCount(1) - gen1Before,
                GC.CollectionCount(2) - gen2Before);

            if (execution.FatalError is not null)
            {
                string error = execution.FatalError;
                dialogService.ShowError("Playlist import failed", error);
                StatusText = error;
                AddDiagnostic($"Playlist import failed: {error}");
                return;
            }

            ImportProgressText = $"Applying saved organization to {result.Channels.Count:N0} imported entries...";
            allChannels.Clear();
            allChannels.AddRange(result.Channels.Select(ApplyUserState));
            OnPropertyChanged(nameof(HasChannels));
            OnPropertyChanged(nameof(ShouldShowFirstRunSetup));
            SelectedChannel = null;
            RefreshGroupsAndCategories();
            PopulateImportIssues(result.Issues);
            ImportProgressText = "Indexing visible channel list...";
            await ApplySearchAsync(importToken).ConfigureAwait(true);
            ImportProgressText = "Refreshing organization, VOD, duplicate, audit, and EPG summaries...";
            RefreshVodLibrary();
            RefreshDuplicateGroups();
            RefreshHiddenLockedAudit();
            RefreshEpgTimeline();
            RefreshLibraryHealth(result.Summary, execution.Duration);

            PlaylistDiffSummary diff = CalculateDiff(previousIds, allChannels.Select(channel => channel.Id));
            PopulateSourceProfiles();
            PopulateRefreshConflicts(previousChannels, allChannels, diff);
            RefreshDiffText = FormatDiff(diff);
            ProfileSummaryText = FormatProfileSummary();
            ReconciliationText = FormatOrganizationReconciliation(diff);
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

            if (recentSource is not null)
            {
                RememberRecentPlaylistSource(recentSource);
            }

            lastPlaylistImportedAt = DateTimeOffset.UtcNow;
            UpdateRefreshScheduleStatus();
            QueueAutoXmltvImportIfEnabled();
            ImportProgressText = $"Import complete: {result.Summary.ImportedCount:N0} channels.";
        }
        catch (OperationCanceledException)
        {
            StatusText = "Import cancelled.";
            ImportProgressText = "Import cancelled.";
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
            if (ReferenceEquals(importCts, activeImportCts))
            {
                importCts = null;
            }

            activeImportCts?.Dispose();
            IsImporting = false;
            IsBusy = false;
        }
    }

    private void CancelImport()
    {
        if (!IsImporting || importCts is null)
        {
            return;
        }

        ImportProgressText = "Cancelling playlist import...";
        StatusText = "Cancelling playlist import...";
        importCts.Cancel();
    }

    private void ApplyPlaylistImportProgress(PlaylistImportProgress progress)
    {
        string message = progress.DisplayText;
        if (!string.IsNullOrWhiteSpace(message))
        {
            ImportProgressText = message;
        }
    }

    private async Task RefreshPlaylistAsync()
    {
        if (lastPlaylistImport is null)
        {
            StatusText = "Import a playlist before refreshing.";
            return;
        }

        await PreviewRefreshAsync(lastPlaylistImport).ConfigureAwait(true);
    }

    private async Task PreviewRefreshAsync(PlaylistImportOperation import)
    {
        try
        {
            IsBusy = true;
            StatusText = "Refreshing playlist for approval preview...";
            AddDiagnostic("Playlist refresh approval preview started.");
            var progress = new Progress<PlaylistImportProgress>(ApplyPlaylistImportProgress);
            PlaylistImportExecution execution = await playlistImportCoordinator.RunAsync(import, shutdownCts.Token, progress).ConfigureAwait(true);
            PlaylistImportResult result = execution.Result;
            if (execution.FatalError is not null)
            {
                string error = execution.FatalError;
                dialogService.ShowError("Playlist refresh failed", error);
                StatusText = error;
                AddDiagnostic($"Playlist refresh failed: {error}");
                return;
            }

            pendingRefreshPreviousChannels = allChannels.ToArray();
            pendingRefreshResult = result;
            pendingRefreshChannels = result.Channels.Select(ApplyUserState).ToArray();
            PlaylistDiffSummary diff = CalculateDiff(
                pendingRefreshPreviousChannels.Select(channel => channel.Id).ToHashSet(),
                pendingRefreshChannels.Select(channel => channel.Id));

            PopulatePendingRefreshChanges(pendingRefreshPreviousChannels, pendingRefreshChannels, diff);
            PendingRefreshSummaryText =
                $"Refresh approval pending: previous {diff.PreviousCount:N0}; incoming {diff.CurrentCount:N0}; " +
                $"{diff.AddedCount:N0} added; {diff.RemovedCount:N0} removed; {diff.UnchangedCount:N0} unchanged. Apply or discard.";
            StatusText = PendingRefreshSummaryText;
            AddDiagnostic(PendingRefreshSummaryText);
            RaisePendingRefreshCommandStates();
        }
        catch (OperationCanceledException)
        {
            StatusText = "Playlist refresh preview cancelled.";
            AddDiagnostic(StatusText);
        }
        catch (Exception ex)
        {
            ShowSafeError("Playlist refresh preview failed", ex);
        }
        finally
        {
            IsBusy = false;
        }
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
            await ImportEpgFileCoreAsync(path, "XMLTV import started.").ConfigureAwait(true);
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

    private async Task ImportEpgFileCoreAsync(string path, string diagnosticMessage)
    {
        AddDiagnostic(diagnosticMessage);
        EpgImportResult result = await xmltvImportService.ImportFileAsync(path, shutdownCts.Token).ConfigureAwait(true);
        epgPrograms.Clear();
        epgPrograms.AddRange(result.Programs);
        RebuildEpgIndex();
        RefreshSelectedChannelEpgGuide();
        RefreshEpgTimeline();
        RefreshLibraryHealth();
        int matched = CountEpgMatches(result.Channels);
        EpgSummaryText = $"{result.SummaryText} Matched channels {matched:N0}.";
        StatusText = EpgSummaryText;
        AddDiagnostic(EpgSummaryText);
    }

    private async Task ImportEpgUrlAsync()
    {
        try
        {
            if (IsBusy)
            {
                return;
            }

            string? url = dialogService.PromptXmltvUrl();
            if (string.IsNullOrWhiteSpace(url))
            {
                return;
            }

            XmltvGuideUrl = url.Trim();
            await ImportEpgFromUrlAsync(XmltvGuideUrl, showErrors: true, updateBusyState: true).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            ShowSafeError("XMLTV URL import failed", ex);
        }
    }

    private void QueueAutoXmltvImportIfEnabled()
    {
        if (!AutoLoadXmltvOnPlaylistImport || string.IsNullOrWhiteSpace(XmltvGuideUrl) || shutdownCts.IsCancellationRequested)
        {
            return;
        }

        _ = ImportEpgFromUrlAsync(XmltvGuideUrl, showErrors: false, updateBusyState: false);
    }

    private async Task ImportEpgFromUrlAsync(string url, bool showErrors, bool updateBusyState)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) || uri.Scheme is not ("http" or "https"))
        {
            if (showErrors)
            {
                StatusText = "XMLTV URL must be http:// or https://.";
            }

            return;
        }

        string? tempPath = null;
        try
        {
            if (updateBusyState)
            {
                IsBusy = true;
            }
            StatusText = "Downloading XMLTV guide...";
            AddDiagnostic($"XMLTV URL import started from host {uri.Host}.");
            string extension = Path.GetExtension(uri.LocalPath);
            if (!extension.Equals(".xml", StringComparison.OrdinalIgnoreCase) &&
                !extension.Equals(".xmltv", StringComparison.OrdinalIgnoreCase) &&
                !extension.Equals(".gz", StringComparison.OrdinalIgnoreCase) &&
                !extension.Equals(".zip", StringComparison.OrdinalIgnoreCase))
            {
                extension = ".xml";
            }

            tempPath = Path.Combine(Path.GetTempPath(), $"iptv-xmltv-{Guid.NewGuid():N}{extension}");
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(shutdownCts.Token);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(25));
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.UserAgent.ParseAdd("IptvViewer/1.0");
            using HttpResponseMessage response = await xmltvHttpClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeoutCts.Token)
                .ConfigureAwait(true);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength is long length && length > MaximumRemoteXmltvBytes)
            {
                throw new InvalidDataException($"XMLTV guide exceeds the configured {MaximumRemoteXmltvBytes:N0} byte limit.");
            }

            await using (Stream remote = await response.Content.ReadAsStreamAsync(timeoutCts.Token).ConfigureAwait(true))
            await using (FileStream file = File.Create(tempPath))
            {
                await CopyWithLimitAsync(remote, file, MaximumRemoteXmltvBytes, timeoutCts.Token).ConfigureAwait(true);
            }

            await ImportEpgFileCoreAsync(tempPath, $"Downloaded XMLTV guide from {uri.Host}.").ConfigureAwait(true);
            XmltvGuideUrl = uri.ToString();
        }
        catch (OperationCanceledException) when (!shutdownCts.IsCancellationRequested)
        {
            StatusText = "XMLTV URL import timed out.";
            AddDiagnostic(StatusText);
            if (showErrors)
            {
                dialogService.ShowError("XMLTV URL import failed", StatusText);
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidDataException)
        {
            string message = SensitiveTextRedactor.RedactText(ex.Message);
            StatusText = $"XMLTV URL import failed: {message}";
            AddDiagnostic(StatusText);
            if (showErrors)
            {
                dialogService.ShowError("XMLTV URL import failed", message);
            }
        }
        finally
        {
            if (tempPath is not null && File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }

            if (updateBusyState)
            {
                IsBusy = false;
            }
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

    private async Task ExportDiagnosticsAsync()
    {
        try
        {
            if (Diagnostics.Count == 0)
            {
                StatusText = "No diagnostics are available to export.";
                return;
            }

            string? path = dialogService.PickDiagnosticsExportFile();
            if (path is null)
            {
                return;
            }

            string[] lines =
            [
                "IPTV Viewer redacted diagnostics",
                $"Exported: {DateTimeOffset.Now:O}",
                "Raw stream URLs, credentials, and token-like query values are redacted before export.",
                string.Empty,
                "Library health",
                SensitiveTextRedactor.RedactText(LibraryHealthSummaryText),
                .. LibraryHealthMetrics.Select(metric => SensitiveTextRedactor.RedactText(metric.DisplayText)),
                string.Empty,
                "Diagnostic log",
                .. Diagnostics.Select(SensitiveTextRedactor.RedactText)
            ];

            await File.WriteAllLinesAsync(path, lines, shutdownCts.Token).ConfigureAwait(true);
            StatusText = $"Exported redacted diagnostics to {Path.GetFileName(path)}.";
            AddDiagnostic($"Exported redacted diagnostics to {Path.GetFileName(path)}.");
        }
        catch (OperationCanceledException)
        {
            StatusText = "Diagnostics export cancelled.";
        }
        catch (Exception ex)
        {
            ShowSafeError("Diagnostics export failed", ex);
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

        Channel channelToPlay = SelectedChannel;
        ProviderPlaybackProfile profile = GetPlaybackProfile(channelToPlay.SourceId);
        int attempts = Math.Clamp(profile.RetryCount, 0, 3) + 1;
        try
        {
            if (SelectedBufferingPreset != profile.BufferingPreset)
            {
                selectedBufferingPreset = profile.BufferingPreset;
                OnPropertyChanged(nameof(SelectedBufferingPreset));
                await playbackEngine.SetBufferingPresetAsync(profile.BufferingPreset, shutdownCts.Token).ConfigureAwait(true);
                AddDiagnostic($"Applied provider playback profile buffer {profile.BufferingPreset}.");
            }

            Exception? lastException = null;
            for (int attempt = 1; attempt <= attempts; attempt++)
            {
                try
                {
                    await playbackEngine.PlayAsync(channelToPlay, shutdownCts.Token).ConfigureAwait(true);
                    lastException = null;
                    break;
                }
                catch (Exception ex) when (ex is not OperationCanceledException && attempt < attempts)
                {
                    lastException = ex;
                    AddDiagnostic($"Playback retry {attempt:N0}/{attempts - 1:N0} for '{channelToPlay.DisplayName}': {SensitiveTextRedactor.RedactText(ex.Message)}");
                    await Task.Delay(TimeSpan.FromMilliseconds(300 * attempt), shutdownCts.Token).ConfigureAwait(true);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    lastException = ex;
                }
            }

            if (lastException is not null)
            {
                throw lastException;
            }

            QueueResumeSeek(channelToPlay);
            UpdateSelectedChannel(channel => channel with { LastWatchedAt = DateTimeOffset.UtcNow }, refreshGroups: false);
            AddDiagnostic($"Playback requested for '{channelToPlay.DisplayName}' on host {channelToPlay.StreamUrl.Host}.");
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
        if (SelectedChannel is not null)
        {
            PushOrganizationUndo($"toggle favorite '{SelectedChannel.DisplayName}'", [SelectedChannel]);
        }

        UpdateSelectedChannel(channel => channel with { IsFavorite = !channel.IsFavorite });
    }

    private void ToggleHidden()
    {
        if (SelectedChannel is not null)
        {
            PushOrganizationUndo($"toggle hidden '{SelectedChannel.DisplayName}'", [SelectedChannel]);
        }

        UpdateSelectedChannel(channel => channel with { IsHidden = !channel.IsHidden }, visibilityOverride: true);
    }

    private void ClearCustomGroup()
    {
        if (SelectedChannel is not null)
        {
            PushOrganizationUndo($"clear group '{SelectedChannel.DisplayName}'", [SelectedChannel]);
        }

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
        PushOrganizationUndo($"move '{selected.DisplayName}'", [selected, target]);
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

    private void PreviewSmartGroup()
    {
        string? normalizedTerm = SmartGroupRuleMatcher.NormalizeTerm(SmartGroupMatchText, SelectedSmartRuleMatchMode);
        string? normalizedGroup = NormalizeCustomGroup(SmartGroupName);
        if (normalizedTerm is null || normalizedGroup is null)
        {
            SmartGroupPreviewText = "Enter both a match term and a destination custom group.";
            StatusText = SmartGroupPreviewText;
            return;
        }

        if (!SmartGroupRuleMatcher.ValidatePattern(normalizedTerm, SelectedSmartRuleMatchMode))
        {
            SmartGroupPreviewText = "Regex rule is invalid or too expensive. Use a simpler expression.";
            StatusText = SmartGroupPreviewText;
            return;
        }

        if (IsReservedGroupName(normalizedGroup))
        {
            SmartGroupPreviewText = $"'{normalizedGroup}' is reserved. Choose a different custom group name.";
            StatusText = SmartGroupPreviewText;
            return;
        }

        int matched = 0;
        int assignable = 0;
        foreach (Channel channel in allChannels)
        {
            if (!SmartGroupRuleMatcher.Matches(channel, normalizedTerm, SelectedSmartRuleMatchMode))
            {
                continue;
            }

            matched++;
            if (string.IsNullOrWhiteSpace(channel.CustomGroup))
            {
                assignable++;
            }
        }

        SmartGroupPreviewText =
            $"Preview: {matched:N0} channels match {SmartGroupRuleMatcher.FormatMode(SelectedSmartRuleMatchMode)} '{SmartGroupMatchText.Trim()}'; {assignable:N0} have no custom group and can be assigned to '{normalizedGroup}'. Existing custom groups are preserved.";
        StatusText = SmartGroupPreviewText;
    }

    private void ApplySmartGroup()
    {
        string? normalizedTerm = SmartGroupRuleMatcher.NormalizeTerm(SmartGroupMatchText, SelectedSmartRuleMatchMode);
        string? normalizedGroup = NormalizeCustomGroup(SmartGroupName);
        if (normalizedTerm is null || normalizedGroup is null)
        {
            SmartGroupPreviewText = "Enter both a match term and a destination custom group before applying.";
            StatusText = SmartGroupPreviewText;
            return;
        }

        if (!SmartGroupRuleMatcher.ValidatePattern(normalizedTerm, SelectedSmartRuleMatchMode))
        {
            SmartGroupPreviewText = "Regex rule is invalid or too expensive. Use a simpler expression.";
            StatusText = SmartGroupPreviewText;
            return;
        }

        if (IsReservedGroupName(normalizedGroup))
        {
            SmartGroupPreviewText = $"'{normalizedGroup}' is reserved. Choose a different custom group name.";
            StatusText = SmartGroupPreviewText;
            return;
        }

        Channel[] matchedChannels = allChannels
            .Where(channel => string.IsNullOrWhiteSpace(channel.CustomGroup) && SmartGroupRuleMatcher.Matches(channel, normalizedTerm, SelectedSmartRuleMatchMode))
            .ToArray();
        if (matchedChannels.Length > 0)
        {
            PushOrganizationUndo($"smart group '{normalizedGroup}'", matchedChannels);
        }

        int changed = 0;
        for (int index = 0; index < allChannels.Count; index++)
        {
            Channel channel = allChannels[index];
            if (!string.IsNullOrWhiteSpace(channel.CustomGroup) ||
                !SmartGroupRuleMatcher.Matches(channel, normalizedTerm, SelectedSmartRuleMatchMode))
            {
                continue;
            }

            Channel updated = channel with { CustomGroup = normalizedGroup };
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
            SmartGroupPreviewText = "Smart group made no changes. Matching channels may already have custom groups.";
            StatusText = SmartGroupPreviewText;
            return;
        }

        knownCustomGroups.Add(normalizedGroup);
        EnsureCustomGroupChoice(normalizedGroup);
        RefreshGroupsAndCategories();
        ScheduleSearch();
        _ = SaveChannelStatesSafelyAsync();
        _ = SaveOrganizationPreferencesSafelyAsync();
        SmartGroupPreviewText = $"Applied smart group '{normalizedGroup}' to {changed:N0} channels.";
        StatusText = SmartGroupPreviewText;
    }

    private void SaveSmartGroupPreset()
    {
        string? presetName = NormalizeCustomGroup(SmartGroupPresetName);
        string? matchText = NormalizeCustomGroup(SmartGroupMatchText);
        string? destination = NormalizeCustomGroup(SmartGroupName);
        if (presetName is null || matchText is null || destination is null)
        {
            StatusText = "Enter preset name, match text, and destination group before saving a smart rule preset.";
            return;
        }

        if (IsReservedGroupName(destination))
        {
            StatusText = $"'{destination}' is reserved. Choose a different destination group.";
            return;
        }

        RemoveSmartGroupPreset(presetName);
        var preset = new SmartGroupRulePresetViewModel(presetName, matchText, destination, SelectedSmartRuleMatchMode);
        SmartGroupPresets.Add(preset);
        SelectedSmartGroupPreset = preset;
        RaiseSmartGroupPresetCommandStates();
        StatusText = $"Saved smart group preset '{presetName}'.";
    }

    private void UseSmartGroupPreset()
    {
        if (SelectedSmartGroupPreset is null)
        {
            StatusText = "Select a smart group preset first.";
            return;
        }

        SmartGroupPresetName = SelectedSmartGroupPreset.Name;
        SmartGroupMatchText = SelectedSmartGroupPreset.MatchText;
        SmartGroupName = SelectedSmartGroupPreset.DestinationGroup;
        SelectedSmartRuleMatchMode = SelectedSmartGroupPreset.MatchMode;
        PreviewSmartGroup();
    }

    private async Task ImportSmartGroupPresetsAsync()
    {
        try
        {
            string? path = dialogService.PickSmartGroupPresetImportFile();
            if (path is null)
            {
                return;
            }

            IReadOnlyList<SmartGroupRulePreset> presets = await smartGroupPresetFileService
                .ImportAsync(path, shutdownCts.Token)
                .ConfigureAwait(true);
            foreach (SmartGroupRulePreset preset in presets)
            {
                RemoveSmartGroupPreset(preset.Name);
                SmartGroupPresets.Add(SmartGroupRulePresetViewModel.FromPreset(preset));
            }

            RaiseSmartGroupPresetCommandStates();
            StatusText = $"Imported {presets.Count:N0} smart group presets.";
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ShowSafeError("Smart group preset import failed", ex);
        }
    }

    private async Task ExportSmartGroupPresetsAsync()
    {
        try
        {
            if (SmartGroupPresets.Count == 0)
            {
                StatusText = "Save or import a smart group preset before exporting.";
                return;
            }

            string? path = dialogService.PickSmartGroupPresetExportFile();
            if (path is null)
            {
                return;
            }

            await smartGroupPresetFileService
                .ExportAsync(path, SmartGroupPresets.Select(preset => preset.ToPreset()), shutdownCts.Token)
                .ConfigureAwait(true);
            StatusText = $"Exported {SmartGroupPresets.Count:N0} smart group presets.";
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ShowSafeError("Smart group preset export failed", ex);
        }
    }

    private void ApplyBatchUpdate(Func<Channel, Channel> update, string actionDescription, bool visibilityOverride = false)
    {
        if (selectedChannelIds.Count == 0)
        {
            StatusText = "Select channels before using batch actions.";
            return;
        }

        HashSet<Guid> selectedIds = selectedChannelIds.ToHashSet();
        Channel[] selectedSnapshot = allChannels.Where(channel => selectedIds.Contains(channel.Id)).ToArray();
        if (selectedSnapshot.Length > 0)
        {
            PushOrganizationUndo($"batch {actionDescription}", selectedSnapshot);
        }

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
            UpdateChannelStateIndex(updated, visibilityOverride);
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

    private async Task ExportCustomGroupCsvAsync()
    {
        try
        {
            if (allChannels.Count == 0)
            {
                StatusText = "Import a playlist before exporting custom groups.";
                return;
            }

            string? path = dialogService.PickCustomGroupCsvExportFile();
            if (path is null)
            {
                return;
            }

            await customGroupCsvService.ExportAsync(path, allChannels, shutdownCts.Token).ConfigureAwait(true);
            StatusText = $"Exported {allChannels.Count:N0} channel custom-group rows to CSV.";
            AddDiagnostic(StatusText);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ShowSafeError("Custom group CSV export failed", ex);
        }
    }

    private async Task ImportCustomGroupCsvAsync()
    {
        try
        {
            string? path = dialogService.PickCustomGroupCsvImportFile();
            if (path is null)
            {
                return;
            }

            IReadOnlyList<CustomGroupCsvRow> rows = await customGroupCsvService.ImportAsync(path, shutdownCts.Token).ConfigureAwait(true);
            Dictionary<Guid, string?> assignments = rows.ToDictionary(row => row.ChannelId, row => NormalizeCustomGroup(row.CustomGroup));
            Channel[] affected = allChannels.Where(channel => assignments.ContainsKey(channel.Id)).ToArray();
            if (affected.Length == 0)
            {
                StatusText = "Custom group CSV did not match any loaded channels.";
                return;
            }

            PushOrganizationUndo("import custom group CSV", affected);
            int changed = 0;
            for (int index = 0; index < allChannels.Count; index++)
            {
                Channel channel = allChannels[index];
                if (!assignments.TryGetValue(channel.Id, out string? group))
                {
                    continue;
                }

                Channel updated = channel with { CustomGroup = group };
                if (updated == channel)
                {
                    continue;
                }

                allChannels[index] = updated;
                UpdateChannelStateIndex(updated);
                if (group is not null)
                {
                    knownCustomGroups.Add(group);
                }

                changed++;
                if (SelectedChannel?.Id == updated.Id)
                {
                    SelectedChannel = updated;
                }
            }

            RefreshGroupsAndCategories();
            RefreshVodLibrary();
            ScheduleSearch();
            _ = SaveChannelStatesSafelyAsync();
            _ = SaveOrganizationPreferencesSafelyAsync();
            StatusText = $"Imported custom-group CSV: {changed:N0} loaded channels updated from {rows.Count:N0} rows.";
            AddDiagnostic(StatusText);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ShowSafeError("Custom group CSV import failed", ex);
        }
    }

    private void ClearFilters()
    {
        SearchText = string.Empty;
        SelectedGroup = AllGroupsOption;
        SelectedCategory = AllCategoriesOption;
        SelectedVodYear = AllYearsOption;
        SelectedContentKind = null;
        FavoritesOnly = false;
        SelectedHiddenFilter = HiddenChannelFilter.VisibleOnly;
        SelectedSmartView = SmartViewFilter.All;
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
            ContentKind = SelectedContentKind,
            VodYear = TryParseVodYear(SelectedVodYear),
            FavoritesOnly = FavoritesOnly,
            HiddenFilter = SelectedHiddenFilter,
            SortMode = SelectedSmartView == SmartViewFilter.FavoritesByGroup ? ChannelSortMode.GroupThenName : SelectedSortMode,
            Limit = GetVisibleResultLimit()
        };

        Channel[] snapshot = ApplySmartViewFilter(GetSearchableChannels()).ToArray();
        IReadOnlyList<Channel> results = await Task.Run(
            () => channelSearchService.Search(snapshot, query),
            cancellationToken).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();

        UiDispatcher.Run(() =>
        {
            VisibleChannels.ReplaceAll(results);
            if (PrefetchVisibleLogosCommand is AsyncRelayCommand logoPrefetch)
            {
                logoPrefetch.RaiseCanExecuteChanged();
            }

            RefreshEpgTimeline();
            if (allChannels.Count > 0)
            {
                int hiddenCount = allChannels.Count(channel => channel.IsHidden);
                int lockedCount = Math.Max(0, allChannels.Count - snapshot.Length);
                string hiddenSummary = hiddenCount > 0 ? $" ({hiddenCount:N0} hidden)" : string.Empty;
                string lockedSummary = lockedCount > 0 ? $" {lockedCount:N0} locked groups hidden." : string.Empty;
                int visibleResultLimit = GetVisibleResultLimit();
                string capSummary = VisibleChannels.Count >= visibleResultLimit
                    ? $" Showing first {visibleResultLimit:N0} results."
                    : string.Empty;
                StatusText = SelectedHiddenFilter == HiddenChannelFilter.HiddenOnly
                    ? $"Showing {VisibleChannels.Count:N0} hidden channels of {allChannels.Count:N0} total."
                    : $"Showing {VisibleChannels.Count:N0} of {allChannels.Count:N0} channels{hiddenSummary}.{capSummary}{lockedSummary}";
            }
        });
    }

    private void RefreshGroupsAndCategories()
    {
        string previousGroup = SelectedGroup;
        string previousCategory = SelectedCategory;
        string previousVodYear = SelectedVodYear;
        Channel[] organizationVisibleChannels = GetSearchableChannels();

        Groups.Clear();
        Groups.Add(AllGroupsOption);
        foreach (string group in organizationVisibleChannels
                     .Select(channel => channel.EffectiveGroupTitle)
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .Order(StringComparer.OrdinalIgnoreCase))
        {
            Groups.Add(group);
        }

        Categories.Clear();
        Categories.Add(AllCategoriesOption);
        foreach (string category in organizationVisibleChannels.Select(channel => channel.Category).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase))
        {
            Categories.Add(category);
        }

        VodYears.Clear();
        VodYears.Add(AllYearsOption);
        foreach (int year in allChannels
                     .Where(IsChannelUnlockedForUi)
                     .Where(channel => channel.ContentKind is ContentKind.Vod or ContentKind.Series)
                     .Select(channel => ChannelMetadataExtractor.TryInferReleaseYear(channel.DisplayName))
                     .Where(year => year.HasValue)
                     .Select(year => year!.Value)
                     .Distinct()
                     .OrderByDescending(year => year))
        {
            VodYears.Add(year.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        RefreshCustomGroupCollections();
        PopulateSourceProfiles();

        SelectedGroup = Groups.Contains(previousGroup, StringComparer.OrdinalIgnoreCase)
            ? previousGroup
            : AllGroupsOption;
        SelectedCategory = Categories.Contains(previousCategory, StringComparer.OrdinalIgnoreCase)
            ? previousCategory
            : AllCategoriesOption;
        SelectedVodYear = VodYears.Contains(previousVodYear, StringComparer.OrdinalIgnoreCase)
            ? previousVodYear
            : AllYearsOption;
    }

    private Channel ApplyUserState(Channel channel)
    {
        if (!channelStates.TryGetValue(channel.Id, out ChannelUserState? state))
        {
            return IsHiddenBySourceDefault(channel)
                ? channel with { IsHidden = true }
                : channel;
        }

        bool hasVisibilityOverride = state.HasExplicitVisibility || state.IsHidden;
        return channel with
        {
            IsFavorite = state.IsFavorite,
            IsHidden = hasVisibilityOverride ? state.IsHidden : IsHiddenBySourceDefault(channel),
            CustomGroup = NormalizeCustomGroup(state.CustomGroup),
            CustomSortIndex = NormalizeCustomSortIndex(state.CustomSortIndex),
            LastWatchedAt = state.LastWatchedAt,
            ResumeProgressPercent = NormalizeResumeProgress(state.ResumeProgressPercent)
        };
    }

    private void UpdateSelectedChannel(Func<Channel, Channel> update, bool refreshGroups = true, bool visibilityOverride = false)
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
        UpdateChannelStateIndex(updated, visibilityOverride);
        SelectedChannel = updated;
        if (refreshGroups)
        {
            RefreshGroupsAndCategories();
            RefreshVodLibrary();
            RefreshHiddenLockedAudit();
        }

        ScheduleSearch();
        _ = SaveChannelStatesSafelyAsync();
    }

    private void ApplyCustomGroupToSelected(string? customGroup)
    {
        string? normalized = NormalizeCustomGroup(customGroup);
        UpdateSelectedChannel(channel => channel with { CustomGroup = normalized });
    }

    private void UpdateChannelStateIndex(Channel channel, bool visibilityOverride = false)
    {
        bool hasExplicitVisibility = visibilityOverride ||
            (channelStates.TryGetValue(channel.Id, out ChannelUserState? existingState) &&
                (existingState.HasExplicitVisibility || existingState.IsHidden));
        var state = new ChannelUserState
        {
            ChannelId = channel.Id,
            IsFavorite = channel.IsFavorite,
            IsHidden = hasExplicitVisibility && channel.IsHidden,
            HasExplicitVisibility = hasExplicitVisibility,
            CustomGroup = NormalizeCustomGroup(channel.CustomGroup),
            CustomSortIndex = NormalizeCustomSortIndex(channel.CustomSortIndex),
            LastWatchedAt = channel.LastWatchedAt,
            ResumeProgressPercent = NormalizeResumeProgress(channel.ResumeProgressPercent)
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
            CustomGroups = customGroups,
            LargeLibraryMode = LargeLibraryMode,
            ChannelViewDensity = SelectedChannelViewDensity,
            SourceProfileNames = sourceProfileNames
                .Where(pair => !string.IsNullOrWhiteSpace(pair.Key) && !string.IsNullOrWhiteSpace(pair.Value))
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase),
            SourcePlaybackProfiles = sourcePlaybackProfiles
                .Where(pair => !string.IsNullOrWhiteSpace(pair.Key))
                .ToDictionary(pair => pair.Key, pair => NormalizePlaybackProfile(pair.Value), StringComparer.OrdinalIgnoreCase),
            SourceDefaultHiddenGroups = sourceDefaultHiddenGroups
                .Where(pair => !string.IsNullOrWhiteSpace(pair.Key) && pair.Value.Length > 0)
                .ToDictionary(pair => pair.Key, pair => NormalizeGroups(pair.Value), StringComparer.OrdinalIgnoreCase),
            RefreshScheduleEnabled = RefreshScheduleEnabled,
            RefreshIntervalMinutes = SelectedRefreshIntervalMinutes,
            ParentalPinSalt = parentalPinSalt,
            ParentalPinHash = parentalPinHash,
            LockedGroups = lockedGroups
                .Select(NormalizeCustomGroup)
                .Where(group => group is not null)
                .Select(group => group!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            XmltvGuideUrl = NormalizeRemoteUrl(XmltvGuideUrl),
            AutoLoadXmltvOnPlaylistImport = AutoLoadXmltvOnPlaylistImport
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

        ApplySourceDefaultHiddenGroups(backup.Preferences.SourceDefaultHiddenGroups);

        for (int index = 0; index < allChannels.Count; index++)
        {
            allChannels[index] = ApplyUserState(allChannels[index] with
            {
                IsFavorite = false,
                IsHidden = false,
                CustomGroup = null,
                CustomSortIndex = null,
                LastWatchedAt = null,
                ResumeProgressPercent = null
            });
        }

        selectedSortMode = Enum.IsDefined(backup.Preferences.SortMode)
            ? backup.Preferences.SortMode
            : ChannelSortMode.FavoritesFirst;
        OnPropertyChanged(nameof(SelectedSortMode));
        largeLibraryMode = backup.Preferences.LargeLibraryMode;
        OnPropertyChanged(nameof(LargeLibraryMode));
        OnPropertyChanged(nameof(VisibleResultLimitText));
        selectedChannelViewDensity = Enum.IsDefined(backup.Preferences.ChannelViewDensity)
            ? backup.Preferences.ChannelViewDensity
            : ChannelViewDensity.Comfortable;
        OnPropertyChanged(nameof(SelectedChannelViewDensity));
        sourceProfileNames.Clear();
        foreach ((string sourceId, string profileName) in backup.Preferences.SourceProfileNames)
        {
            sourceProfileNames[sourceId] = profileName;
        }

        sourcePlaybackProfiles.Clear();
        foreach ((string sourceId, ProviderPlaybackProfile profile) in backup.Preferences.SourcePlaybackProfiles)
        {
            if (!string.IsNullOrWhiteSpace(sourceId))
            {
                sourcePlaybackProfiles[sourceId] = NormalizePlaybackProfile(profile);
            }
        }

        selectedRefreshIntervalMinutes = NormalizeRefreshInterval(backup.Preferences.RefreshIntervalMinutes);
        OnPropertyChanged(nameof(SelectedRefreshIntervalMinutes));
        refreshScheduleEnabled = backup.Preferences.RefreshScheduleEnabled;
        OnPropertyChanged(nameof(RefreshScheduleEnabled));
        parentalPinSalt = NormalizeSecret(backup.Preferences.ParentalPinSalt);
        parentalPinHash = NormalizeSecret(backup.Preferences.ParentalPinHash);
        lockedGroups.Clear();
        foreach (string group in backup.Preferences.LockedGroups
                     .Select(NormalizeCustomGroup)
                     .Where(group => group is not null)
                     .Select(group => group!))
        {
            lockedGroups.Add(group);
        }

        IsParentalUnlocked = !IsParentalLockConfigured;
        UpdateParentalLockStatus();
        RestartRefreshScheduleLoop();
        xmltvGuideUrl = backup.Preferences.XmltvGuideUrl ?? string.Empty;
        OnPropertyChanged(nameof(XmltvGuideUrl));
        autoLoadXmltvOnPlaylistImport = backup.Preferences.AutoLoadXmltvOnPlaylistImport;
        OnPropertyChanged(nameof(AutoLoadXmltvOnPlaylistImport));

        RefreshGroupsAndCategories();
        RefreshLibraryHealth();
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

    private async Task SaveUiPreferencesSafelyAsync()
    {
        try
        {
            RecentPlaylistSourcePreference[] recentSources = RecentPlaylistSources
                .Select(source => source.ToPreference())
                .ToArray();
            await uiPreferencesStore.UpdateAsync(
                preferences => preferences with
                {
                    IsBasicMode = IsBasicMode,
                    FirstRunSetupCompleted = FirstRunSetupCompleted,
                    LogoCacheLimitMegabytes = SelectedLogoCacheLimitMegabytes,
                    AppTheme = SelectedAppTheme,
                    AppUiScale = SelectedAppUiScale,
                    RecentPlaylistSources = recentSources
                },
                CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            string message = SensitiveTextRedactor.RedactText(ex.Message);
            UiDispatcher.Run(() => AddDiagnostic($"UI preference save failed: {message}"));
        }
    }

    private void ApplyRecentPlaylistSources(IEnumerable<RecentPlaylistSourcePreference> sources)
    {
        RecentPlaylistSources.Clear();
        foreach (RecentPlaylistSourceViewModel source in recentPlaylistSourceManager.FromPreferences(sources))
        {
            RecentPlaylistSources.Add(source);
        }

        SelectedRecentPlaylistSource = RecentPlaylistSources.FirstOrDefault();
        RaiseRecentPlaylistCommandStates();
    }

    private void RememberRecentPlaylistSource(RecentPlaylistSourceViewModel source)
    {
        if (!RecentPlaylistSourceManager.IsUsable(source))
        {
            return;
        }

        RecentPlaylistSourceViewModel merged = recentPlaylistSourceManager.Remember(
            RecentPlaylistSources,
            source,
            DateTimeOffset.UtcNow,
            out IReadOnlyList<RecentPlaylistSourceViewModel> updatedSources);
        ReplaceRecentPlaylistSources(updatedSources);
        SelectedRecentPlaylistSource = RecentPlaylistSources.FirstOrDefault(candidate => RecentPlaylistSourceManager.IsSame(candidate, merged));
        _ = SaveUiPreferencesSafelyAsync();
    }

    private void ReplaceRecentPlaylistSources(IEnumerable<RecentPlaylistSourceViewModel> sources)
    {
        RecentPlaylistSourceViewModel[] normalized = recentPlaylistSourceManager.Normalize(sources).ToArray();
        RecentPlaylistSources.Clear();
        foreach (RecentPlaylistSourceViewModel source in normalized)
        {
            RecentPlaylistSources.Add(source);
        }

        if (SelectedRecentPlaylistSource is not null &&
            RecentPlaylistSources.All(source => !RecentPlaylistSourceManager.IsSame(source, SelectedRecentPlaylistSource)))
        {
            SelectedRecentPlaylistSource = RecentPlaylistSources.FirstOrDefault();
        }

        RaiseRecentPlaylistCommandStates();
    }


    private static RecentPlaylistSourceViewModel CreateRecentPlaylistSource(
        RecentPlaylistSourceKind kind,
        string value,
        string? displayName)
    {
        string normalizedValue = value.Trim();
        string normalizedDisplayName = string.IsNullOrWhiteSpace(displayName)
            ? RecentPlaylistSourceViewModel.CreateDisplayName(kind, normalizedValue)
            : displayName.Trim();
        return new RecentPlaylistSourceViewModel(kind, normalizedDisplayName, normalizedValue, DateTimeOffset.UtcNow);
    }

    private async Task OpenRecentPlaylistSourceAsync()
    {
        RecentPlaylistSourceViewModel? source = SelectedRecentPlaylistSource;
        if (source is null)
        {
            StatusText = "Select a recent playlist source first.";
            return;
        }

        if (source.Kind == RecentPlaylistSourceKind.RemoteUrl)
        {
            await ImportPlaylistUrlAsync(source.Value).ConfigureAwait(true);
            return;
        }

        await ImportPlaylistFileAsync(source.Value).ConfigureAwait(true);
    }

    private void ClearRecentPlaylistSources()
    {
        RecentPlaylistSources.Clear();
        SelectedRecentPlaylistSource = null;
        RaiseRecentPlaylistCommandStates();
        _ = SaveUiPreferencesSafelyAsync();
        StatusText = "Cleared recent playlist sources.";
    }

    private void RenameRecentPlaylistSource()
    {
        if (SelectedRecentPlaylistSource is null)
        {
            StatusText = "Select a recent playlist source before renaming.";
            return;
        }

        string? normalizedName = NormalizeCustomGroup(RecentPlaylistSourceName);
        if (normalizedName is null)
        {
            StatusText = "Enter a recent playlist name.";
            return;
        }

        RecentPlaylistSourceViewModel selected = SelectedRecentPlaylistSource;
        ReplaceRecentPlaylistSources(recentPlaylistSourceManager.Rename(RecentPlaylistSources, selected, normalizedName));
        SelectedRecentPlaylistSource = RecentPlaylistSources.FirstOrDefault(source => RecentPlaylistSourceManager.IsSame(source, selected));
        _ = SaveUiPreferencesSafelyAsync();
        StatusText = $"Renamed recent playlist source to '{normalizedName}'.";
    }

    private void TogglePinRecentPlaylistSource()
    {
        if (SelectedRecentPlaylistSource is null)
        {
            StatusText = "Select a recent playlist source before pinning.";
            return;
        }

        RecentPlaylistSourceViewModel selected = SelectedRecentPlaylistSource;
        RecentPlaylistSourceViewModel updated = selected with { IsPinned = !selected.IsPinned };
        ReplaceRecentPlaylistSources(recentPlaylistSourceManager.TogglePin(RecentPlaylistSources, selected));
        SelectedRecentPlaylistSource = RecentPlaylistSources.FirstOrDefault(source => RecentPlaylistSourceManager.IsSame(source, updated));
        _ = SaveUiPreferencesSafelyAsync();
        StatusText = updated.IsPinned
            ? $"Pinned recent playlist source '{updated.DisplayName}'."
            : $"Unpinned recent playlist source '{updated.DisplayName}'.";
    }

    private void RemoveRecentPlaylistSource()
    {
        if (SelectedRecentPlaylistSource is null)
        {
            StatusText = "Select a recent playlist source before removing.";
            return;
        }

        RecentPlaylistSourceViewModel selected = SelectedRecentPlaylistSource;
        string removedName = selected.DisplayName;
        ReplaceRecentPlaylistSources(recentPlaylistSourceManager.Remove(RecentPlaylistSources, selected));
        SelectedRecentPlaylistSource = RecentPlaylistSources.FirstOrDefault();
        _ = SaveUiPreferencesSafelyAsync();
        StatusText = $"Removed recent playlist source '{removedName}'.";
    }

    private async Task ImportRecentPlaylistSourcesAsync()
    {
        try
        {
            string? path = dialogService.PickRecentPlaylistSourcesImportFile();
            if (path is null)
            {
                return;
            }

            RecentPlaylistSourcesExport imported = await recentPlaylistSourceFileService.ImportAsync(path, shutdownCts.Token).ConfigureAwait(true);
            int previousCount = RecentPlaylistSources.Count;
            ReplaceRecentPlaylistSources(RecentPlaylistSources.Concat(imported.Sources.Select(RecentPlaylistSourceViewModel.FromPreference)));
            SelectedRecentPlaylistSource = RecentPlaylistSources.FirstOrDefault();
            await SaveUiPreferencesSafelyAsync().ConfigureAwait(true);
            StatusText = $"Imported recent playlist sources; list now contains {RecentPlaylistSources.Count:N0} item(s) ({previousCount:N0} before merge).";
            AddDiagnostic(StatusText);
        }
        catch (OperationCanceledException)
        {
            StatusText = "Recent playlist source import cancelled.";
        }
        catch (Exception ex)
        {
            ShowSafeError("Recent playlist source import failed", ex);
        }
    }

    private async Task ExportRecentPlaylistSourcesAsync()
    {
        try
        {
            string? path = dialogService.PickRecentPlaylistSourcesExportFile();
            if (path is null)
            {
                return;
            }

            var export = new RecentPlaylistSourcesExport
            {
                Sources = RecentPlaylistSources.Select(source => source.ToPreference()).ToArray()
            };
            await recentPlaylistSourceFileService.ExportAsync(path, export, shutdownCts.Token).ConfigureAwait(true);
            StatusText = $"Exported {export.Sources.Length:N0} recent playlist source(s).";
            AddDiagnostic(StatusText);
        }
        catch (OperationCanceledException)
        {
            StatusText = "Recent playlist source export cancelled.";
        }
        catch (Exception ex)
        {
            ShowSafeError("Recent playlist source export failed", ex);
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

    private int GetVisibleResultLimit()
    {
        return LargeLibraryMode ? LargeLibraryVisibleChannelResults : StandardVisibleChannelResults;
    }

    private string FormatProfileSummary()
    {
        int sourceCount = allChannels.Select(channel => channel.SourceId).Distinct().Count();
        int defaultRuleCount = sourceDefaultHiddenGroups.Values.Sum(groups => groups.Length);
        string sourceText = sourceCount == 1 ? "source" : "sources";
        return $"Profile: automatic per-playlist/source organization across {sourceCount:N0} {sourceText}; saved favorites, hidden channels, custom groups, default visibility rules ({defaultRuleCount:N0}), and order are matched by stable channel IDs.";
    }

    private string FormatOrganizationReconciliation(PlaylistDiffSummary diff)
    {
        HashSet<Guid> loadedChannelIds = allChannels.Select(channel => channel.Id).ToHashSet();
        int matchedStates = channelStates.Keys.Count(loadedChannelIds.Contains);
        int retainedExternalStates = Math.Max(0, channelStates.Count - matchedStates);
        int favoriteCount = allChannels.Count(channel => channel.IsFavorite);
        int hiddenCount = allChannels.Count(channel => channel.IsHidden);
        int customGroupedCount = allChannels.Count(channel => !string.IsNullOrWhiteSpace(channel.CustomGroup));
        int logoAvailableCount = allChannels.Count(channel => !string.IsNullOrWhiteSpace(channel.TvgLogo));

        return
            $"Organization reconciliation: {matchedStates:N0} saved states matched; " +
            $"{retainedExternalStates:N0} saved states from other/removed sources retained; " +
            $"{diff.AddedCount:N0} new; {diff.RemovedCount:N0} removed; " +
            $"{favoriteCount:N0} favorites; {hiddenCount:N0} hidden; {customGroupedCount:N0} custom-grouped; " +
            $"{logoAvailableCount:N0} logos available.";
    }

    private static bool IsReservedGroupName(string group)
    {
        return group.Equals(AllGroupsOption, StringComparison.OrdinalIgnoreCase) ||
            group.Equals(AllCategoriesOption, StringComparison.OrdinalIgnoreCase) ||
            group.Equals(SourceGroupAssignmentOption, StringComparison.OrdinalIgnoreCase);
    }

    private void RemoveSmartGroupPreset(string presetName)
    {
        for (int index = SmartGroupPresets.Count - 1; index >= 0; index--)
        {
            if (SmartGroupPresets[index].Name.Equals(presetName, StringComparison.OrdinalIgnoreCase))
            {
                SmartGroupPresets.RemoveAt(index);
            }
        }
    }

    private void ApplySourceDefaultHiddenGroups(IDictionary<string, string[]>? rules)
    {
        sourceDefaultHiddenGroups.Clear();
        foreach ((string sourceId, string[] groups) in sourceDefaultVisibilityManager.NormalizeRules(rules))
        {
            sourceDefaultHiddenGroups[sourceId] = groups;
        }

        RefreshSourceDefaultVisibilityOptions();
    }

    private bool IsHiddenBySourceDefault(Channel channel)
    {
        return sourceDefaultVisibilityManager.IsHiddenByDefault(channel, sourceDefaultHiddenGroups);
    }

    private void RefreshSourceDefaultVisibilityOptions()
    {
        string previous = SelectedSourceDefaultVisibilityGroup;
        SourceDefaultVisibilityGroups.Clear();
        foreach (string group in sourceDefaultVisibilityManager.GetGroupOptions(allChannels, SelectedSourceProfile?.SourceId, AllGroupsOption))
        {
            SourceDefaultVisibilityGroups.Add(group);
        }

        SelectedSourceDefaultVisibilityGroup = SourceDefaultVisibilityGroups.Contains(previous, StringComparer.OrdinalIgnoreCase)
            ? previous
            : AllGroupsOption;
        RefreshSourceDefaultVisibilitySummary();
        RaiseSourceDefaultVisibilityCommandStates();
    }

    private void RefreshSourceDefaultVisibilitySummary()
    {
        SourceDefaultVisibilitySummaryText = sourceDefaultVisibilityManager.BuildSummary(
            SelectedSourceProfile,
            SelectedSourceDefaultVisibilityGroup,
            sourceDefaultHiddenGroups,
            AllGroupsOption);
    }

    private void HideSelectedSourceDefaultGroup()
    {
        ChangeSelectedSourceDefaultGroup(hidden: true);
    }

    private void ShowSelectedSourceDefaultGroup()
    {
        ChangeSelectedSourceDefaultGroup(hidden: false);
    }

    private void ChangeSelectedSourceDefaultGroup(bool hidden)
    {
        if (SelectedSourceProfile is null || string.Equals(SelectedSourceDefaultVisibilityGroup, AllGroupsOption, StringComparison.OrdinalIgnoreCase))
        {
            StatusText = "Select a source profile and group before changing default visibility.";
            return;
        }

        string? normalizedGroup = NormalizeCustomGroup(SelectedSourceDefaultVisibilityGroup);
        if (normalizedGroup is null)
        {
            StatusText = "Select a valid source group before changing default visibility.";
            return;
        }

        string sourceId = SelectedSourceProfile.SourceId;
        SourceDefaultVisibilityChange change = sourceDefaultVisibilityManager.SetRule(sourceDefaultHiddenGroups, sourceId, normalizedGroup, hidden);
        int affected = ApplySourceDefaultVisibilityToLoadedChannels(sourceId, change.GroupName, hidden);
        _ = SaveOrganizationPreferencesSafelyAsync();
        RefreshSourceDefaultVisibilitySummary();
        ProfileSummaryText = FormatProfileSummary();
        RefreshLibraryHealth();
        RaiseProfileCommandStates();
        StatusText = change.Changed
            ? $"{(hidden ? "Hid" : "Showed")} source group '{change.GroupName}' by default; updated {affected:N0} currently loaded channel(s) without explicit saved state."
            : $"Source group '{change.GroupName}' already had that default visibility.";
    }

    private int ApplySourceDefaultVisibilityToLoadedChannels(string sourceId, string groupName, bool hidden)
    {
        int changed = 0;
        Guid? selectedId = SelectedChannel?.Id;
        for (int index = 0; index < allChannels.Count; index++)
        {
            Channel current = allChannels[index];
            if (!current.SourceId.ToString().Equals(sourceId, StringComparison.OrdinalIgnoreCase) ||
                !current.GroupTitle.Equals(groupName, StringComparison.OrdinalIgnoreCase) ||
                channelStates.ContainsKey(current.Id) ||
                current.IsHidden == hidden)
            {
                continue;
            }

            Channel updated = current with { IsHidden = hidden };
            allChannels[index] = updated;
            changed++;
            if (selectedId == updated.Id)
            {
                SelectedChannel = updated;
            }
        }

        if (changed > 0)
        {
            RefreshGroupsAndCategories();
            RefreshHiddenLockedAudit();
            ScheduleSearch();
        }

        return changed;
    }

    private int ReapplySourceDefaultVisibilityToLoadedChannels()
    {
        int changed = 0;
        Guid? selectedId = SelectedChannel?.Id;
        for (int index = 0; index < allChannels.Count; index++)
        {
            Channel current = allChannels[index];
            if (channelStates.ContainsKey(current.Id))
            {
                continue;
            }

            bool shouldHide = IsHiddenBySourceDefault(current);
            if (current.IsHidden == shouldHide)
            {
                continue;
            }

            Channel updated = current with { IsHidden = shouldHide };
            allChannels[index] = updated;
            changed++;
            if (selectedId == updated.Id)
            {
                SelectedChannel = updated;
            }
        }

        if (changed > 0)
        {
            RefreshGroupsAndCategories();
            RefreshHiddenLockedAudit();
            ScheduleSearch();
        }

        return changed;
    }

    private void PopulateSourceProfiles()
    {
        string? previousSourceId = SelectedSourceProfile?.SourceId;
        SourceProfiles.Clear();
        foreach (IGrouping<Guid, Channel> group in allChannels.GroupBy(channel => channel.SourceId).OrderBy(group => GetSourceProfileName(group.Key, group.Count()), StringComparer.OrdinalIgnoreCase))
        {
            string sourceId = group.Key.ToString();
            SourceProfiles.Add(new SourceProfileViewModel(sourceId, GetSourceProfileName(group.Key, group.Count()), group.Count()));
        }

        SelectedSourceProfile = previousSourceId is null
            ? SourceProfiles.FirstOrDefault()
            : SourceProfiles.FirstOrDefault(profile => profile.SourceId.Equals(previousSourceId, StringComparison.OrdinalIgnoreCase)) ?? SourceProfiles.FirstOrDefault();
        RefreshSourceDefaultVisibilityOptions();
    }

    private string GetSourceProfileName(Guid sourceId, int sourceChannelCount)
    {
        string key = sourceId.ToString();
        if (sourceProfileNames.TryGetValue(key, out string? name) && !string.IsNullOrWhiteSpace(name))
        {
            return name;
        }

        return sourceChannelCount == allChannels.Count
            ? "Current playlist"
            : $"Source {key[..8]}";
    }

    private void RenameSelectedSourceProfile()
    {
        if (SelectedSourceProfile is null)
        {
            StatusText = "Select a source profile before renaming.";
            return;
        }

        string? normalized = NormalizeCustomGroup(RenameSourceProfileName);
        if (normalized is null)
        {
            StatusText = "Enter a source profile name.";
            return;
        }

        sourceProfileNames[SelectedSourceProfile.SourceId] = normalized;
        PopulateSourceProfiles();
        _ = SaveOrganizationPreferencesSafelyAsync();
        RaiseProfileCommandStates();
        StatusText = $"Renamed source profile to '{normalized}'.";
    }

    private void LoadSelectedSourcePlaybackProfile(string? sourceId)
    {
        ProviderPlaybackProfile profile = sourceId is not null && sourcePlaybackProfiles.TryGetValue(sourceId, out ProviderPlaybackProfile? saved)
            ? NormalizePlaybackProfile(saved)
            : new ProviderPlaybackProfile { RetryCount = 0, BufferingPreset = BufferingPreset.Balanced };

        selectedSourceRetryCount = profile.RetryCount;
        selectedSourceBufferingPreset = profile.BufferingPreset;
        OnPropertyChanged(nameof(SelectedSourceRetryCount));
        OnPropertyChanged(nameof(SelectedSourceBufferingPreset));
    }

    private void SaveSelectedSourcePlaybackProfile()
    {
        if (SelectedSourceProfile is null)
        {
            StatusText = "Select a source profile before saving playback fallback settings.";
            return;
        }

        sourcePlaybackProfiles[SelectedSourceProfile.SourceId] = new ProviderPlaybackProfile
        {
            RetryCount = SelectedSourceRetryCount,
            BufferingPreset = SelectedSourceBufferingPreset
        };
        _ = SaveOrganizationPreferencesSafelyAsync();
        RaiseProfileCommandStates();
        StatusText = $"Saved playback profile for '{SelectedSourceProfile.DisplayName}': {SelectedSourceRetryCount:N0} retries, {SelectedSourceBufferingPreset} buffer.";
    }

    private IReadOnlyList<string> BuildSourceProfileImportConflicts(SourceProfileExport imported)
    {
        var conflicts = new List<string>();
        foreach (string sourceId in imported.SourceProfileNames.Keys.Where(sourceId => sourceProfileNames.ContainsKey(sourceId)).Take(25))
        {
            conflicts.Add($"Profile name for source {sourceId}: '{sourceProfileNames[sourceId]}' -> '{imported.SourceProfileNames[sourceId]}'.");
        }

        foreach (string sourceId in imported.SourcePlaybackProfiles.Keys.Where(sourceId => sourcePlaybackProfiles.ContainsKey(sourceId)).Take(25))
        {
            ProviderPlaybackProfile current = NormalizePlaybackProfile(sourcePlaybackProfiles[sourceId]);
            ProviderPlaybackProfile incoming = NormalizePlaybackProfile(imported.SourcePlaybackProfiles[sourceId]);
            conflicts.Add($"Playback profile for source {sourceId}: {current.RetryCount:N0}/{current.BufferingPreset} -> {incoming.RetryCount:N0}/{incoming.BufferingPreset}.");
        }

        foreach (string sourceId in imported.SourceDefaultHiddenGroups.Keys.Where(sourceId => sourceDefaultHiddenGroups.ContainsKey(sourceId)).Take(25))
        {
            string current = string.Join(", ", sourceDefaultHiddenGroups[sourceId]);
            string incoming = string.Join(", ", NormalizeGroups(imported.SourceDefaultHiddenGroups[sourceId]));
            conflicts.Add($"Default hidden groups for source {sourceId}: [{current}] -> [{incoming}].");
        }

        if (conflicts.Count == 0)
        {
            return conflicts;
        }

        int importedTotal = imported.SourceProfileNames.Count + imported.SourcePlaybackProfiles.Count + imported.SourceDefaultHiddenGroups.Count;
        conflicts.Insert(0, $"Import contains {importedTotal:N0} source profile setting group(s); {conflicts.Count:N0} shown may overwrite existing settings.");
        return conflicts;
    }

    private async Task ImportSourceProfilesAsync()
    {
        try
        {
            string? path = dialogService.PickSourceProfileImportFile();
            if (path is null)
            {
                return;
            }

            SourceProfileExport imported = await sourceProfileFileService.ImportAsync(path, shutdownCts.Token).ConfigureAwait(true);
            IReadOnlyList<string> conflicts = BuildSourceProfileImportConflicts(imported);
            if (conflicts.Count > 0 && !dialogService.ConfirmSourceProfileImport("Review Source Profile Conflicts", conflicts))
            {
                StatusText = "Source profile import cancelled before applying conflicts.";
                return;
            }

            int importedNameCount = 0;
            foreach ((string sourceId, string profileName) in imported.SourceProfileNames)
            {
                if (!string.IsNullOrWhiteSpace(sourceId) && !string.IsNullOrWhiteSpace(profileName))
                {
                    sourceProfileNames[sourceId.Trim()] = profileName.Trim();
                    importedNameCount++;
                }
            }

            int importedPlaybackCount = 0;
            foreach ((string sourceId, ProviderPlaybackProfile profile) in imported.SourcePlaybackProfiles)
            {
                if (!string.IsNullOrWhiteSpace(sourceId))
                {
                    sourcePlaybackProfiles[sourceId.Trim()] = NormalizePlaybackProfile(profile);
                    importedPlaybackCount++;
                }
            }

            int importedDefaultGroupCount = 0;
            foreach ((string sourceId, string[] hiddenGroups) in imported.SourceDefaultHiddenGroups)
            {
                if (string.IsNullOrWhiteSpace(sourceId))
                {
                    continue;
                }

                string[] normalizedGroups = NormalizeGroups(hiddenGroups);
                if (normalizedGroups.Length == 0)
                {
                    continue;
                }

                sourceDefaultHiddenGroups[sourceId.Trim()] = normalizedGroups;
                importedDefaultGroupCount += normalizedGroups.Length;
            }

            int defaultVisibilityAffected = ReapplySourceDefaultVisibilityToLoadedChannels();
            PopulateSourceProfiles();
            LoadSelectedSourcePlaybackProfile(SelectedSourceProfile?.SourceId);
            await SaveOrganizationPreferencesSafelyAsync().ConfigureAwait(true);
            ProfileSummaryText = FormatProfileSummary();
            RefreshLibraryHealth();
            StatusText = $"Imported {importedNameCount:N0} source profile names, {importedPlaybackCount:N0} playback profiles, and {importedDefaultGroupCount:N0} default hidden group rules; updated {defaultVisibilityAffected:N0} loaded channel(s).";
            AddDiagnostic(StatusText);
        }
        catch (OperationCanceledException)
        {
            StatusText = "Source profile import cancelled.";
        }
        catch (Exception ex)
        {
            ShowSafeError("Source profile import failed", ex);
        }
        finally
        {
            RaiseProfileCommandStates();
        }
    }

    private async Task ExportSourceProfilesAsync()
    {
        try
        {
            string? path = dialogService.PickSourceProfileExportFile();
            if (path is null)
            {
                return;
            }

            var export = new SourceProfileExport
            {
                SourceProfileNames = sourceProfileNames
                    .Where(pair => !string.IsNullOrWhiteSpace(pair.Key) && !string.IsNullOrWhiteSpace(pair.Value))
                    .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase),
                SourcePlaybackProfiles = sourcePlaybackProfiles
                    .Where(pair => !string.IsNullOrWhiteSpace(pair.Key))
                    .ToDictionary(pair => pair.Key, pair => NormalizePlaybackProfile(pair.Value), StringComparer.OrdinalIgnoreCase),
                SourceDefaultHiddenGroups = sourceDefaultHiddenGroups
                    .Where(pair => !string.IsNullOrWhiteSpace(pair.Key) && pair.Value.Length > 0)
                    .ToDictionary(pair => pair.Key, pair => NormalizeGroups(pair.Value), StringComparer.OrdinalIgnoreCase)
            };
            await sourceProfileFileService.ExportAsync(path, export, shutdownCts.Token).ConfigureAwait(true);
            int defaultRuleCount = export.SourceDefaultHiddenGroups.Values.Sum(groups => groups.Length);
            StatusText = $"Exported {export.SourceProfileNames.Count:N0} source profile names, {export.SourcePlaybackProfiles.Count:N0} playback profiles, and {defaultRuleCount:N0} default hidden group rules.";
            AddDiagnostic(StatusText);
        }
        catch (OperationCanceledException)
        {
            StatusText = "Source profile export cancelled.";
        }
        catch (Exception ex)
        {
            ShowSafeError("Source profile export failed", ex);
        }
    }

    private ProviderPlaybackProfile GetPlaybackProfile(Guid sourceId)
    {
        string key = sourceId.ToString();
        return sourcePlaybackProfiles.TryGetValue(key, out ProviderPlaybackProfile? profile)
            ? NormalizePlaybackProfile(profile)
            : new ProviderPlaybackProfile { RetryCount = 0, BufferingPreset = SelectedBufferingPreset };
    }

    private void RefreshLibraryHealth(PlaylistImportSummary? importSummary = null, TimeSpan? importDuration = null)
    {
        if (importSummary is not null)
        {
            lastPlaylistImportSummary = importSummary;
        }

        if (importDuration is not null)
        {
            lastPlaylistImportDuration = importDuration;
        }

        LibraryHealthMetrics.Clear();
        foreach (LibraryHealthMetricViewModel metric in LibraryHealthAnalyzer.BuildMetrics(
                     allChannels,
                     channelStates.Keys.ToArray(),
                     sourceDefaultHiddenGroups,
                     epgPrograms.Count,
                     lastPlaylistImportDuration,
                     lastPlaylistImportSummary,
                     lastLibraryHealthResourceMetrics))
        {
            LibraryHealthMetrics.Add(metric);
        }

        LibraryHealthSummaryText = allChannels.Count == 0
            ? "Library health: import a playlist to inspect channel, organization, VOD, logo, EPG, and import metrics."
            : $"Library health: {allChannels.Count:N0} channels, {allChannels.Count(channel => !channel.IsHidden):N0} visible, {sourceDefaultHiddenGroups.Values.Sum(groups => groups.Length):N0} default visibility rule(s).";
    }

    private void RefreshVodLibrary()
    {
        string? previousId = SelectedVodLibraryItem?.ChannelId.ToString();
        VodLibraryItems.Clear();
        Channel[] allVodChannels = allChannels
            .Where(IsChannelUnlockedForUi)
            .Where(channel => channel.ContentKind is ContentKind.Vod or ContentKind.Series)
            .OrderByDescending(channel => channel.ResumeProgressPercent ?? -1)
            .ThenByDescending(channel => channel.LastWatchedAt ?? DateTimeOffset.MinValue)
            .ThenBy(channel => channel.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Take(MaximumVodLibraryItems)
            .ToArray();
        int totalVod = allChannels.Count(channel => channel.ContentKind is ContentKind.Vod or ContentKind.Series);
        int maxPageIndex = allVodChannels.Length == 0 ? 0 : (allVodChannels.Length - 1) / VodLibraryPageSize;
        if (VodLibraryPageIndex > maxPageIndex)
        {
            VodLibraryPageIndex = maxPageIndex;
        }

        Channel[] vodChannels = allVodChannels
            .Skip(VodLibraryPageIndex * VodLibraryPageSize)
            .Take(VodLibraryPageSize)
            .ToArray();

        foreach (Channel channel in vodChannels)
        {
            int? year = ChannelMetadataExtractor.TryInferReleaseYear(channel.DisplayName);
            string? posterPath = logoCacheService.TryGetCachedLogoPath(channel.TvgLogo);
            VodLibraryItems.Add(new VodLibraryItemViewModel(
                channel.Id,
                channel.DisplayName,
                channel.EffectiveGroupTitle,
                year?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "Year unknown",
                channel.ResumeProgressPercent is int progress ? $"{progress}% watched" : "Not started",
                string.IsNullOrWhiteSpace(channel.TvgLogo) ? "No poster" : posterPath is null ? "Poster available" : "Poster cached",
                CanPreviewLogoPath(posterPath) ? posterPath : null));
        }

        SelectedVodLibraryItem = previousId is null
            ? null
            : VodLibraryItems.FirstOrDefault(item => item.ChannelId.ToString().Equals(previousId, StringComparison.OrdinalIgnoreCase));
        VodLibrarySummaryText = totalVod == 0
            ? "VOD library: no VOD or series entries detected."
            : $"VOD library: page {VodLibraryPageIndex + 1:N0}/{maxPageIndex + 1:N0}; showing {VodLibraryItems.Count:N0} of {Math.Min(totalVod, MaximumVodLibraryItems):N0} capped VOD/series entries, sorted by resume progress.";
        RaiseVodPageCommandStates();
    }

    private void MoveVodPage(int delta)
    {
        int next = Math.Max(0, VodLibraryPageIndex + delta);
        if (next == VodLibraryPageIndex)
        {
            return;
        }

        VodLibraryPageIndex = next;
        RefreshVodLibrary();
    }

    private bool HasNextVodPage()
    {
        int totalVod = allChannels
            .Where(IsChannelUnlockedForUi)
            .Count(channel => channel.ContentKind is ContentKind.Vod or ContentKind.Series);
        int capped = Math.Min(totalVod, MaximumVodLibraryItems);
        return (VodLibraryPageIndex + 1) * VodLibraryPageSize < capped;
    }

    private void RefreshDuplicateGroups()
    {
        string? previousKey = SelectedDuplicateGroup?.Key;
        DuplicateChannelGroups.Clear();

        foreach (IGrouping<string, Channel> group in allChannels
                     .Where(channel => !string.IsNullOrWhiteSpace(channel.NormalizedName))
                     .GroupBy(CreateDuplicateKey, StringComparer.Ordinal)
                     .Where(group => group.Count() > 1)
                     .OrderByDescending(group => group.Count())
                     .ThenBy(group => group.First().DisplayName, StringComparer.OrdinalIgnoreCase)
                     .Take(MaximumDuplicateGroups))
        {
            Channel first = group.OrderBy(GetCustomOrderIndex).First();
            string groupText = string.Join(", ", group
                .Select(channel => channel.EffectiveGroupTitle)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(3));
            DuplicateChannelGroups.Add(new DuplicateChannelGroupViewModel(
                group.Key,
                group.Count(),
                first.DisplayName,
                groupText));
        }

        SelectedDuplicateGroup = previousKey is null
            ? DuplicateChannelGroups.FirstOrDefault()
            : DuplicateChannelGroups.FirstOrDefault(group => group.Key.Equals(previousKey, StringComparison.Ordinal)) ?? DuplicateChannelGroups.FirstOrDefault();

        DuplicateAssistantSummaryText = DuplicateChannelGroups.Count == 0
            ? "Duplicate assistant: no same-name/same-host duplicates detected."
            : $"Duplicate assistant: showing {DuplicateChannelGroups.Count:N0} duplicate groups. Hide duplicates keeps the first playlist/order entry visible.";
    }

    private void HideSelectedDuplicateGroup()
    {
        if (SelectedDuplicateGroup is null)
        {
            StatusText = "Select a duplicate group first.";
            return;
        }

        Channel[] duplicateChannels = allChannels
            .Where(channel => CreateDuplicateKey(channel).Equals(SelectedDuplicateGroup.Key, StringComparison.Ordinal))
            .OrderBy(GetCustomOrderIndex)
            .ToArray();
        if (duplicateChannels.Length <= 1)
        {
            StatusText = "Selected duplicate group no longer has duplicates.";
            RefreshDuplicateGroups();
            return;
        }

        Channel[] toHide = duplicateChannels.Skip(1).Where(channel => !channel.IsHidden).ToArray();
        if (toHide.Length == 0)
        {
            StatusText = "Duplicate group is already hidden except for the first channel.";
            return;
        }

        string title = $"Hide {toHide.Length:N0} duplicate(s) for '{duplicateChannels[0].DisplayName}'?";
        string[] previewLines = duplicateChannels
            .Select((channel, index) => index == 0
                ? $"KEEP: {channel.DisplayName} Â· {channel.EffectiveGroupTitle} Â· {channel.StreamUrl.Host}"
                : $"HIDE: {channel.DisplayName} Â· {channel.EffectiveGroupTitle} Â· {channel.StreamUrl.Host}")
            .ToArray();
        if (!dialogService.ConfirmDuplicateHide(title, previewLines))
        {
            StatusText = "Duplicate hide cancelled.";
            return;
        }

        PushOrganizationUndo($"hide duplicate group '{duplicateChannels[0].DisplayName}'", toHide);
        HashSet<Guid> idsToHide = toHide.Select(channel => channel.Id).ToHashSet();
        int changed = 0;
        for (int index = 0; index < allChannels.Count; index++)
        {
            Channel channel = allChannels[index];
            if (!idsToHide.Contains(channel.Id))
            {
                continue;
            }

            Channel updated = channel with { IsHidden = true };
            allChannels[index] = updated;
            UpdateChannelStateIndex(updated, visibilityOverride: true);
            changed++;
            if (SelectedChannel?.Id == updated.Id)
            {
                SelectedChannel = updated;
            }
        }

        RefreshGroupsAndCategories();
        RefreshDuplicateGroups();
        ScheduleSearch();
        _ = SaveChannelStatesSafelyAsync();
        StatusText = $"Hid {changed:N0} duplicate channels; kept '{duplicateChannels[0].DisplayName}' visible.";
    }

    private void RefreshHiddenLockedAudit()
    {
        string? previousGroup = SelectedAuditRow?.GroupName;
        HiddenLockedAuditRows.Clear();
        foreach (IGrouping<string, Channel> group in allChannels
                     .GroupBy(channel => channel.EffectiveGroupTitle, StringComparer.OrdinalIgnoreCase)
                     .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
        {
            int total = group.Count();
            int hidden = group.Count(channel => channel.IsHidden);
            bool locked = lockedGroups.Contains(group.Key);
            if (hidden == 0 && !locked)
            {
                continue;
            }

            HiddenLockedAuditRows.Add(new HiddenLockedAuditRowViewModel(group.Key, total, hidden, locked));
        }

        SelectedAuditRow = previousGroup is null
            ? HiddenLockedAuditRows.FirstOrDefault()
            : HiddenLockedAuditRows.FirstOrDefault(row => row.GroupName.Equals(previousGroup, StringComparison.OrdinalIgnoreCase)) ?? HiddenLockedAuditRows.FirstOrDefault();
        int hiddenCount = allChannels.Count(channel => channel.IsHidden);
        AuditSummaryText = HiddenLockedAuditRows.Count == 0
            ? "Hidden/locked audit: no hidden channels or locked groups."
            : $"Hidden/locked audit: {hiddenCount:N0} hidden channels, {lockedGroups.Count:N0} locked groups across {HiddenLockedAuditRows.Count:N0} flagged groups.";
        RaiseAuditCommandStates();
    }

    private void UnhideSelectedAuditGroup()
    {
        if (SelectedAuditRow is null)
        {
            StatusText = "Select an audit group first.";
            return;
        }

        Channel[] hiddenChannels = allChannels
            .Where(channel => channel.IsHidden && channel.EffectiveGroupTitle.Equals(SelectedAuditRow.GroupName, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (hiddenChannels.Length == 0)
        {
            StatusText = $"Group '{SelectedAuditRow.GroupName}' has no hidden channels.";
            return;
        }

        PushOrganizationUndo($"unhide audit group '{SelectedAuditRow.GroupName}'", hiddenChannels);
        HashSet<Guid> hiddenIds = hiddenChannels.Select(channel => channel.Id).ToHashSet();
        int changed = 0;
        for (int index = 0; index < allChannels.Count; index++)
        {
            Channel channel = allChannels[index];
            if (!hiddenIds.Contains(channel.Id))
            {
                continue;
            }

            Channel updated = channel with { IsHidden = false };
            allChannels[index] = updated;
            UpdateChannelStateIndex(updated, visibilityOverride: true);
            changed++;
        }

        RefreshGroupsAndCategories();
        RefreshHiddenLockedAudit();
        ScheduleSearch();
        _ = SaveChannelStatesSafelyAsync();
        StatusText = $"Unhid {changed:N0} channels in '{SelectedAuditRow?.GroupName}'.";
    }

    private void UnlockSelectedAuditGroup()
    {
        if (SelectedAuditRow is null)
        {
            StatusText = "Select an audit group first.";
            return;
        }

        if (!IsParentalUnlocked)
        {
            StatusText = "Unlock parental controls before unlocking groups.";
            return;
        }

        if (!lockedGroups.Remove(SelectedAuditRow.GroupName))
        {
            StatusText = $"Group '{SelectedAuditRow.GroupName}' is not locked.";
            return;
        }

        UpdateParentalLockStatus();
        RefreshGroupsAndCategories();
        RefreshHiddenLockedAudit();
        ScheduleSearch();
        _ = SaveOrganizationPreferencesSafelyAsync();
        StatusText = $"Unlocked group '{SelectedAuditRow.GroupName}'.";
    }

    private void PopulateRefreshConflicts(IReadOnlyList<Channel> previousChannels, IReadOnlyList<Channel> currentChannels, PlaylistDiffSummary diff)
    {
        HashSet<Guid> currentIds = currentChannels.Select(channel => channel.Id).ToHashSet();
        HashSet<Guid> previousIds = previousChannels.Select(channel => channel.Id).ToHashSet();
        lastRemovedChannelIds = previousChannels
            .Where(channel => !currentIds.Contains(channel.Id))
            .Select(channel => channel.Id)
            .ToHashSet();

        RefreshConflicts.Clear();
        foreach (Channel channel in previousChannels.Where(channel => !currentIds.Contains(channel.Id)).Take(100))
        {
            RefreshConflicts.Add(new PlaylistRefreshConflictViewModel(
                "Removed",
                channel.Id,
                channel.DisplayName,
                $"was present before refresh in '{channel.EffectiveGroupTitle}'"));
        }

        foreach (Channel channel in currentChannels.Where(channel => !previousIds.Contains(channel.Id)).Take(100))
        {
            RefreshConflicts.Add(new PlaylistRefreshConflictViewModel(
                "New",
                channel.Id,
                channel.DisplayName,
                $"appeared after refresh in '{channel.EffectiveGroupTitle}'"));
        }

        ConflictReviewText = RefreshConflicts.Count == 0
            ? "Refresh review: no new or removed channels detected."
            : $"Refresh review: showing {RefreshConflicts.Count:N0} of {diff.AddedCount + diff.RemovedCount:N0} new/removed channels.";
        RaiseConflictCommandStates();
    }

    private void PopulatePendingRefreshChanges(IReadOnlyList<Channel> previousChannels, IReadOnlyList<Channel> currentChannels, PlaylistDiffSummary diff)
    {
        HashSet<Guid> currentIds = currentChannels.Select(channel => channel.Id).ToHashSet();
        HashSet<Guid> previousIds = previousChannels.Select(channel => channel.Id).ToHashSet();
        PendingRefreshChanges.Clear();
        foreach (Channel channel in previousChannels.Where(channel => !currentIds.Contains(channel.Id)).Take(100))
        {
            PendingRefreshChanges.Add(new RefreshApprovalChangeViewModel(
                "Remove",
                channel.Id,
                channel.DisplayName,
                $"currently in '{channel.EffectiveGroupTitle}'"));
        }

        foreach (Channel channel in currentChannels.Where(channel => !previousIds.Contains(channel.Id)).Take(100))
        {
            PendingRefreshChanges.Add(new RefreshApprovalChangeViewModel(
                "Add",
                channel.Id,
                channel.DisplayName,
                $"incoming in '{channel.EffectiveGroupTitle}'"));
        }

        if (PendingRefreshChanges.Count == 0)
        {
            PendingRefreshChanges.Add(new RefreshApprovalChangeViewModel(
                "No diff",
                Guid.Empty,
                "No added or removed channels",
                $"{diff.UnchangedCount:N0} unchanged channels"));
        }
    }

    private async Task ApplyPendingRefreshAsync()
    {
        if (pendingRefreshChannels is null || pendingRefreshResult is null)
        {
            StatusText = "No pending refresh to apply.";
            return;
        }

        try
        {
            IsBusy = true;
            Channel[] previousChannels = pendingRefreshPreviousChannels;
            PlaylistImportResult result = pendingRefreshResult;
            allChannels.Clear();
            allChannels.AddRange(pendingRefreshChannels);
            OnPropertyChanged(nameof(HasChannels));
            OnPropertyChanged(nameof(ShouldShowFirstRunSetup));
            SelectedChannel = null;
            RefreshGroupsAndCategories();
            PopulateImportIssues(result.Issues);
            await ApplySearchAsync(shutdownCts.Token).ConfigureAwait(true);
            RefreshVodLibrary();
            RefreshDuplicateGroups();
            RefreshHiddenLockedAudit();
            RefreshEpgTimeline();
            RefreshLibraryHealth(result.Summary, lastPlaylistImportDuration);

            PlaylistDiffSummary diff = CalculateDiff(previousChannels.Select(channel => channel.Id).ToHashSet(), allChannels.Select(channel => channel.Id));
            PopulateSourceProfiles();
            PopulateRefreshConflicts(previousChannels, allChannels, diff);
            RefreshDiffText = FormatDiff(diff);
            ProfileSummaryText = FormatProfileSummary();
            ReconciliationText = FormatOrganizationReconciliation(diff);
            ImportSummaryText =
                $"Applied refresh: imported {result.Summary.ImportedCount:N0}; valid {result.Summary.ValidCount:N0}; " +
                $"warnings {result.Summary.WarningCount:N0}; errors {result.Summary.ErrorCount:N0}; duplicates {result.Summary.DuplicateCount:N0}.";
            ClearPendingRefresh();
            lastPlaylistImportedAt = DateTimeOffset.UtcNow;
            UpdateRefreshScheduleStatus();
            StatusText = $"Applied approved playlist refresh. {RefreshDiffText}";
            AddDiagnostic(StatusText);
            QueueAutoXmltvImportIfEnabled();
        }
        catch (OperationCanceledException)
        {
            StatusText = "Applying playlist refresh was cancelled.";
            AddDiagnostic(StatusText);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void DiscardPendingRefresh()
    {
        if (pendingRefreshChannels is null)
        {
            StatusText = "No pending refresh to discard.";
            return;
        }

        ClearPendingRefresh();
        StatusText = "Discarded pending playlist refresh; current library unchanged.";
    }

    private void ClearPendingRefresh()
    {
        pendingRefreshChannels = null;
        pendingRefreshPreviousChannels = [];
        pendingRefreshResult = null;
        PendingRefreshChanges.Clear();
        PendingRefreshSummaryText = "Refresh approval: no pending refresh.";
        RaisePendingRefreshCommandStates();
    }

    private void ClearRemovedConflictStates()
    {
        if (lastRemovedChannelIds.Count == 0)
        {
            StatusText = "No removed-channel states to clear from the last refresh.";
            return;
        }

        int removed = 0;
        foreach (Guid channelId in lastRemovedChannelIds)
        {
            if (channelStates.Remove(channelId))
            {
                removed++;
            }
        }

        lastRemovedChannelIds.Clear();
        RefreshConflicts.Clear();
        ConflictReviewText = $"Cleared {removed:N0} saved states for channels removed in the last refresh.";
        _ = SaveChannelStatesSafelyAsync();
        RaiseConflictCommandStates();
        StatusText = ConflictReviewText;
    }

    private void RefreshSelectedChannelEpgGuide()
    {
        SelectedChannelEpgPrograms.Clear();
        if (SelectedChannel is null || epgPrograms.Count == 0)
        {
            return;
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        foreach (EpgProgram program in GetProgramsForChannel(SelectedChannel)
                     .Where(MatchesEpgSearch)
                     .Where(program => program.Stop is null || program.Stop >= now.AddHours(-1))
                     .OrderBy(program => program.Start ?? DateTimeOffset.MaxValue)
                     .Take(8))
        {
            SelectedChannelEpgPrograms.Add(EpgProgramViewModel.FromProgram(program));
        }
    }

    private void RefreshSelectedChannelFallbacks()
    {
        ChannelFallbacks.Clear();
        if (SelectedChannel is null)
        {
            SelectedChannelFallback = null;
            FallbackSummaryText = "Fallback streams appear when playlist alternates share the selected channel name.";
            return;
        }

        Channel selected = SelectedChannel;
        foreach (Channel fallback in allChannels
                     .Where(channel => channel.Id != selected.Id)
                     .Where(channel => channel.ContentKind == selected.ContentKind)
                     .Where(channel => channel.NormalizedName.Equals(selected.NormalizedName, StringComparison.Ordinal))
                     .OrderByDescending(CalculateFallbackScore)
                     .ThenBy(channel => channel.IsHidden)
                     .ThenBy(channel => channel.EffectiveGroupTitle, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(channel => channel.StreamUrl.Host, StringComparer.OrdinalIgnoreCase)
                     .Take(20))
        {
            (int score, string reason) = CalculateFallbackScoreDetail(fallback);
            ChannelFallbacks.Add(new ChannelFallbackViewModel(
                fallback.Id,
                fallback.DisplayName,
                fallback.EffectiveGroupTitle,
                fallback.StreamUrl.Host,
                fallback.IsHidden,
                score,
                reason));
        }

        SelectedChannelFallback = ChannelFallbacks.FirstOrDefault();
        FallbackSummaryText = ChannelFallbacks.Count == 0
            ? "No fallback alternates found for the selected channel."
            : $"Fallback alternates: {ChannelFallbacks.Count:N0} same-name stream entries available.";
    }

    private async Task PlaySelectedFallbackAsync()
    {
        if (SelectedChannelFallback is null)
        {
            return;
        }

        Channel? fallback = allChannels.FirstOrDefault(channel => channel.Id == SelectedChannelFallback.ChannelId);
        if (fallback is null)
        {
            StatusText = "Selected fallback is no longer available.";
            RefreshSelectedChannelFallbacks();
            return;
        }

        SelectedChannel = fallback;
        await PlaySelectedAsync().ConfigureAwait(true);
    }

    private void RefreshEpgTimeline()
    {
        EpgTimelineRows.Clear();
        if (epgProgramsByChannelKey.Count == 0 || VisibleChannels.Count == 0)
        {
            EpgTimelineSummaryText = epgProgramsByChannelKey.Count == 0
                ? "EPG timeline appears after importing XMLTV guide data."
                : "EPG timeline: no visible channels match the current filters.";
            return;
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        DateTimeOffset windowStart = GetEpgWindowStart(now, SelectedEpgTimelineWindow);
        int matched = 0;
        foreach (Channel channel in VisibleChannels.Take(MaximumEpgTimelineRows))
        {
            EpgProgram[] upcoming = GetProgramsForChannel(channel)
                .Where(MatchesEpgSearch)
                .Where(program => program.Stop is null || program.Stop >= windowStart)
                .Where(program => program.Start is null || program.Start <= windowStart.AddHours(4))
                .OrderBy(program => program.Start ?? DateTimeOffset.MaxValue)
                .Take(3)
                .ToArray();
            if (upcoming.Length == 0)
            {
                continue;
            }

            matched++;
            EpgTimelineRows.Add(new EpgTimelineRowViewModel(
                channel.DisplayName,
                FormatTimelineProgram(upcoming.ElementAtOrDefault(0), now),
                FormatTimelineProgram(upcoming.ElementAtOrDefault(1), now),
                FormatTimelineProgram(upcoming.ElementAtOrDefault(2), now),
                windowStart.ToLocalTime().ToString("g")));
        }

        string windowLabel = EpgTimelineWindowOptions.First(option => option.Value == SelectedEpgTimelineWindow).Label;
        EpgTimelineSummaryText = matched == 0
            ? $"EPG timeline ({windowLabel}): no guide matches for the visible channels{FormatEpgSearchSuffix()}."
            : $"EPG timeline ({windowLabel}): {matched:N0} visible channels with programs around {windowStart.ToLocalTime():g}{FormatEpgSearchSuffix()} (capped at {MaximumEpgTimelineRows:N0}).";
    }

    private void RebuildEpgIndex()
    {
        epgProgramsByChannelKey.Clear();
        foreach (EpgProgram program in epgPrograms)
        {
            string key = ChannelNormalizer.NormalizeForSearch(program.ChannelId);
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            if (!epgProgramsByChannelKey.TryGetValue(key, out List<EpgProgram>? programs))
            {
                programs = [];
                epgProgramsByChannelKey[key] = programs;
            }

            programs.Add(program);
        }

        foreach (List<EpgProgram> programs in epgProgramsByChannelKey.Values)
        {
            programs.Sort((left, right) => Nullable.Compare(left.Start, right.Start));
        }
    }

    private IEnumerable<EpgProgram> GetProgramsForChannel(Channel channel)
    {
        HashSet<EpgProgram> seen = [];
        foreach (string key in GetEpgLookupKeys(channel))
        {
            if (!epgProgramsByChannelKey.TryGetValue(key, out List<EpgProgram>? programs))
            {
                continue;
            }

            foreach (EpgProgram program in programs)
            {
                if (seen.Add(program))
                {
                    yield return program;
                }
            }
        }
    }

    private static IEnumerable<string> GetEpgLookupKeys(Channel channel)
    {
        if (!string.IsNullOrWhiteSpace(channel.TvgId))
        {
            yield return ChannelNormalizer.NormalizeForSearch(channel.TvgId);
        }

        if (!string.IsNullOrWhiteSpace(channel.TvgName))
        {
            yield return ChannelNormalizer.NormalizeForSearch(channel.TvgName);
        }

        yield return channel.NormalizedName;
    }

    private bool MatchesEpgSearch(EpgProgram program)
    {
        string normalizedSearch = ChannelNormalizer.NormalizeForSearch(EpgSearchText);
        return string.IsNullOrWhiteSpace(normalizedSearch) ||
            ChannelNormalizer.NormalizeForSearch(program.Title).Contains(normalizedSearch, StringComparison.Ordinal) ||
            ChannelNormalizer.NormalizeForSearch(program.Description).Contains(normalizedSearch, StringComparison.Ordinal);
    }

    private string FormatEpgSearchSuffix()
    {
        return string.IsNullOrWhiteSpace(EpgSearchText)
            ? string.Empty
            : $" matching '{EpgSearchText.Trim()}'";
    }

    private int CalculateFallbackScore(Channel channel)
    {
        return CalculateFallbackScoreDetail(channel).Score;
    }

    private (int Score, string Reason) CalculateFallbackScoreDetail(Channel channel)
    {
        int score = channel.IsHidden ? 40 : 60;
        List<string> reasons = [];
        if (channel.StreamUrl.Uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            score += 10;
            reasons.Add("https");
        }
        else
        {
            reasons.Add(channel.StreamUrl.Uri.Scheme);
        }

        if (streamHealthTracker.TryGetFallbackScoreImpact(channel.Id, out int healthScoreAdjustment, out string healthReason))
        {
            score += healthScoreAdjustment;
            reasons.Add(healthReason);
        }
        else
        {
            reasons.Add("health unknown");
        }

        ProviderPlaybackProfile profile = GetPlaybackProfile(channel.SourceId);
        if (profile.BufferingPreset == BufferingPreset.PoorNetwork)
        {
            score += 5;
            reasons.Add("stable buffer");
        }

        reasons.Add("bitrate n/a");
        return (Math.Clamp(score, 0, 100), string.Join(", ", reasons));
    }

    private void SetSelectedResumeProgress(int? progress)
    {
        if (SelectedChannel is null)
        {
            return;
        }

        if (SelectedChannel.ContentKind is not (ContentKind.Vod or ContentKind.Series))
        {
            StatusText = "Resume progress is intended for VOD and series entries.";
            return;
        }

        int? normalizedProgress = NormalizeResumeProgress(progress);
        PushOrganizationUndo($"set resume progress '{SelectedChannel.DisplayName}'", [SelectedChannel]);
        UpdateSelectedChannel(channel => channel with { ResumeProgressPercent = normalizedProgress }, refreshGroups: false);
        OnPropertyChanged(nameof(ResumeProgressText));
        SelectedVodDetailText = SelectedChannel is null
            ? "Select VOD or series content to view detail and resume controls."
            : FormatVodDetail(SelectedChannel);
        StatusText = normalizedProgress is null
            ? $"Cleared resume progress for '{SelectedChannel?.DisplayName}'."
            : $"Set resume progress for '{SelectedChannel?.DisplayName}' to {normalizedProgress.Value:N0}%.";
    }

    public void SetParentalPin(string? pin)
    {
        string? normalizedPin = ParentalPinService.NormalizePin(pin);
        if (normalizedPin is null)
        {
            ParentalLockStatusText = "PIN must be 4-12 digits.";
            StatusText = ParentalLockStatusText;
            return;
        }

        parentalPinSalt = ParentalPinService.CreateSalt();
        parentalPinHash = ParentalPinService.Hash(parentalPinSalt, normalizedPin);
        IsParentalUnlocked = true;
        UpdateParentalLockStatus();
        _ = SaveOrganizationPreferencesSafelyAsync();
        StatusText = "Parental PIN configured. Use locked groups to hide restricted content until unlocked.";
    }

    public void UnlockParentalControls(string? pin)
    {
        if (!IsParentalLockConfigured)
        {
            ParentalLockStatusText = "Parental lock is not configured.";
            return;
        }

        string? normalizedPin = ParentalPinService.NormalizePin(pin);
        if (normalizedPin is null || !ParentalPinService.Verify(parentalPinSalt, parentalPinHash, normalizedPin))
        {
            IsParentalUnlocked = false;
            ParentalLockStatusText = "PIN unlock failed.";
            StatusText = ParentalLockStatusText;
            return;
        }

        IsParentalUnlocked = true;
        UpdateParentalLockStatus();
        RefreshGroupsAndCategories();
        RefreshHiddenLockedAudit();
        ScheduleSearch();
        StatusText = "Parental controls unlocked for this session.";
    }

    private void LockParentalControls()
    {
        if (!IsParentalLockConfigured)
        {
            ParentalLockStatusText = "Set a PIN before locking groups.";
            return;
        }

        IsParentalUnlocked = false;
        UpdateParentalLockStatus();
        RefreshGroupsAndCategories();
        RefreshHiddenLockedAudit();
        ScheduleSearch();
        StatusText = "Parental controls locked.";
    }

    private void LockSelectedGroup()
    {
        if (!IsParentalLockConfigured)
        {
            StatusText = "Set a PIN before locking groups.";
            return;
        }

        string? group = GetSelectedLockGroup();
        if (group is null)
        {
            StatusText = "Select a group or channel before locking a group.";
            return;
        }

        lockedGroups.Add(group);
        UpdateParentalLockStatus();
        RefreshGroupsAndCategories();
        RefreshHiddenLockedAudit();
        ScheduleSearch();
        _ = SaveOrganizationPreferencesSafelyAsync();
        StatusText = $"Locked group '{group}'.";
    }

    private void UnlockSelectedGroup()
    {
        if (!IsParentalUnlocked)
        {
            StatusText = "Unlock parental controls before changing locked groups.";
            return;
        }

        string? group = GetSelectedLockGroup();
        if (group is null)
        {
            StatusText = "Select a group or channel before unlocking a group.";
            return;
        }

        if (!lockedGroups.Remove(group))
        {
            StatusText = $"Group '{group}' is not locked.";
            return;
        }

        UpdateParentalLockStatus();
        RefreshGroupsAndCategories();
        RefreshHiddenLockedAudit();
        ScheduleSearch();
        _ = SaveOrganizationPreferencesSafelyAsync();
        StatusText = $"Unlocked group '{group}'.";
    }

    private void ClearParentalPin()
    {
        parentalPinSalt = null;
        parentalPinHash = null;
        lockedGroups.Clear();
        IsParentalUnlocked = true;
        UpdateParentalLockStatus();
        RefreshGroupsAndCategories();
        RefreshHiddenLockedAudit();
        ScheduleSearch();
        _ = SaveOrganizationPreferencesSafelyAsync();
        StatusText = "Cleared parental PIN and locked groups.";
    }

    private async Task RunSearchBenchmarkAsync()
    {
        try
        {
            IsBusy = true;
            SearchBenchmarkResults.Clear();
            SearchBenchmarkSummaryText = $"Search benchmark running against {SearchBenchmarkChannelCount:N0} synthetic channels...";
            Channel[] source = allChannels.Count == 0 ? CreateSyntheticBenchmarkChannels(SearchBenchmarkChannelCount) : ExpandBenchmarkChannels(allChannels, SearchBenchmarkChannelCount);
            var scenarios = new (string Name, ChannelSearchQuery Query)[]
            {
                ("name search", new ChannelSearchQuery { Text = "news", Limit = 500 }),
                ("VOD filter", new ChannelSearchQuery { ContentKind = ContentKind.Vod, Limit = 500 }),
                ("group sort", new ChannelSearchQuery { SortMode = ChannelSortMode.GroupThenName, Limit = 1_000 }),
                ("resume sort", new ChannelSearchQuery { SortMode = ChannelSortMode.RecentlyWatched, Limit = 1_000 })
            };

            foreach ((string name, ChannelSearchQuery query) in scenarios)
            {
                (int count, long elapsedMs) = await Task.Run(() =>
                {
                    Stopwatch stopwatch = Stopwatch.StartNew();
                    int count = channelSearchService.Search(source, query).Count;
                    stopwatch.Stop();
                    return (count, stopwatch.ElapsedMilliseconds);
                }, shutdownCts.Token).ConfigureAwait(true);
                SearchBenchmarkResults.Add(new SearchBenchmarkResultViewModel(name, source.Length, count, elapsedMs));
            }

            long maxMs = SearchBenchmarkResults.Count == 0 ? 0 : SearchBenchmarkResults.Max(result => result.ElapsedMilliseconds);
            SearchBenchmarkSummaryText = $"Search benchmark complete: {source.Length:N0} channels, slowest scenario {maxMs:N0} ms.";
            StatusText = SearchBenchmarkSummaryText;
            AddDiagnostic(SearchBenchmarkSummaryText);
        }
        catch (OperationCanceledException)
        {
            SearchBenchmarkSummaryText = "Search benchmark cancelled.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task PrefetchVisibleLogosAsync()
    {
        logoPrefetchCts?.Cancel();
        logoPrefetchCts?.Dispose();
        logoPrefetchCts = CancellationTokenSource.CreateLinkedTokenSource(shutdownCts.Token);
        CancellationToken token = logoPrefetchCts.Token;

        Channel[] candidates = VisibleChannels
            .Where(channel => !string.IsNullOrWhiteSpace(channel.TvgLogo))
            .DistinctBy(channel => channel.TvgLogo)
            .Take(MaximumLogoPrefetchCount)
            .ToArray();
        if (candidates.Length == 0)
        {
            LogoPrefetchStatusText = "Logo prefetch skipped: visible channels do not include logo URLs.";
            return;
        }

        LogoPrefetchStatusText = $"Logo prefetch started for {candidates.Length:N0} visible channels.";
        int cached = 0;
        int skipped = 0;
        using var gate = new SemaphoreSlim(4, 4);
        IEnumerable<Task> tasks = candidates.Select(async channel =>
        {
            await gate.WaitAsync(token).ConfigureAwait(false);
            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(75), token).ConfigureAwait(false);
                LogoCacheResult result = await logoCacheService.CacheLogoAsync(channel.TvgLogo, logoHttpClient, token).ConfigureAwait(false);
                if (result.Success)
                {
                    Interlocked.Increment(ref cached);
                }
                else
                {
                    Interlocked.Increment(ref skipped);
                }
            }
            finally
            {
                gate.Release();
            }
        });

        try
        {
            await Task.WhenAll(tasks).ConfigureAwait(true);
            LogoPrefetchStatusText = $"Logo prefetch complete: {cached:N0} cached, {skipped:N0} skipped.";
            StatusText = LogoPrefetchStatusText;
        }
        catch (OperationCanceledException)
        {
            LogoPrefetchStatusText = "Logo prefetch cancelled.";
        }
    }

    private async Task TrimLogoCacheAsync()
    {
        try
        {
            IsBusy = true;
            long maxBytes = Math.Max(1, SelectedLogoCacheLimitMegabytes) * 1024L * 1024L;
            int removed = await logoCacheService.TrimAsync(maxBytes, shutdownCts.Token).ConfigureAwait(true);
            RefreshLogoCacheStatus();
            StatusText = $"Trimmed logo cache to {SelectedLogoCacheLimitMegabytes:N0} MB; removed {removed:N0} files.";
            AddDiagnostic(StatusText);
        }
        catch (OperationCanceledException)
        {
            StatusText = "Logo cache trim cancelled.";
        }
        catch (Exception ex)
        {
            ShowSafeError("Logo cache trim failed", ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ClearLogoCacheAsync()
    {
        try
        {
            IsBusy = true;
            int removed = await logoCacheService.ClearAsync(shutdownCts.Token).ConfigureAwait(true);
            SelectedChannelLogoPath = null;
            RefreshLogoCacheStatus();
            RefreshVodLibrary();
            StatusText = $"Cleared logo cache; removed {removed:N0} files.";
            AddDiagnostic(StatusText);
        }
        catch (OperationCanceledException)
        {
            StatusText = "Logo cache clear cancelled.";
        }
        catch (Exception ex)
        {
            ShowSafeError("Logo cache clear failed", ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void RefreshLogoCacheStatus()
    {
        LogoCacheStatistics statistics = logoCacheService.GetStatistics();
        LogoCacheStatusText = $"{statistics.DisplayText} Limit: {SelectedLogoCacheLimitMegabytes:N0} MB.";
    }

    private void RestartRefreshScheduleLoop()
    {
        refreshScheduleCts?.Cancel();
        refreshScheduleCts?.Dispose();
        refreshScheduleCts = null;
        UpdateRefreshScheduleStatus();

        if (!RefreshScheduleEnabled || shutdownCts.IsCancellationRequested)
        {
            return;
        }

        refreshScheduleCts = CancellationTokenSource.CreateLinkedTokenSource(shutdownCts.Token);
        CancellationToken token = refreshScheduleCts.Token;
        _ = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(1), token).ConfigureAwait(false);
                    UiDispatcher.Run(UpdateRefreshScheduleStatus);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }, token);
    }

    private void UpdateRefreshScheduleStatus()
    {
        if (!RefreshScheduleEnabled)
        {
            RefreshScheduleStatusText = "Provider refresh schedule is off.";
            return;
        }

        if (lastPlaylistImport is null || lastPlaylistImportedAt is null)
        {
            RefreshScheduleStatusText = $"Provider refresh schedule: every {SelectedRefreshIntervalMinutes:N0} minutes after playlist import; manual approval required.";
            return;
        }

        DateTimeOffset dueAt = lastPlaylistImportedAt.Value.AddMinutes(SelectedRefreshIntervalMinutes);
        if (DateTimeOffset.UtcNow >= dueAt)
        {
            RefreshScheduleStatusText = $"Provider refresh due since {dueAt.ToLocalTime():g}. Review changes with the Refresh button.";
            return;
        }

        RefreshScheduleStatusText = $"Next provider refresh review due {dueAt.ToLocalTime():g}. Refresh is manual only.";
    }

    private void PushOrganizationUndo(string description, IEnumerable<Channel> channels)
    {
        ChannelUndoSnapshot[] snapshots = channels
            .GroupBy(channel => channel.Id)
            .Select(group =>
            {
                Channel channel = group.First();
                bool hasExplicitVisibility = channelStates.TryGetValue(channel.Id, out ChannelUserState? state) &&
                    (state.HasExplicitVisibility || state.IsHidden);
                return ChannelUndoSnapshot.FromChannel(channel, hasExplicitVisibility);
            })
            .ToArray();
        if (snapshots.Length == 0)
        {
            return;
        }

        organizationUndoStack.Push(new ChannelUndoAction(description, snapshots));
        if (UndoOrganizationActionCommand is RelayCommand undo)
        {
            undo.RaiseCanExecuteChanged();
        }
    }

    private void UndoLastOrganizationAction()
    {
        if (!organizationUndoStack.TryPop(out ChannelUndoAction? action))
        {
            StatusText = "No organization action to undo.";
            return;
        }

        int restored = 0;
        foreach (ChannelUndoSnapshot snapshot in action.Snapshots)
        {
            int index = allChannels.FindIndex(channel => channel.Id == snapshot.ChannelId);
            if (index < 0)
            {
                continue;
            }

            Channel restoredChannel = snapshot.Apply(allChannels[index]);
            allChannels[index] = restoredChannel;
            UpdateChannelStateIndex(restoredChannel, visibilityOverride: snapshot.HasExplicitVisibility);
            restored++;
            if (SelectedChannel?.Id == restoredChannel.Id)
            {
                SelectedChannel = restoredChannel;
            }
        }

        RefreshGroupsAndCategories();
        ScheduleSearch();
        _ = SaveChannelStatesSafelyAsync();
        if (UndoOrganizationActionCommand is RelayCommand undo)
        {
            undo.RaiseCanExecuteChanged();
        }

        StatusText = $"Undid {action.Description}; restored {restored:N0} loaded channels.";
    }

    private void UpdateStreamHealth(PlaybackStateSnapshot state)
    {
        if (streamHealthTracker.Record(state))
        {
            RefreshStreamHealthRows();
        }
    }

    private void RefreshStreamHealthRows()
    {
        StreamHealthRows.Clear();
        foreach (StreamHealthViewModel row in streamHealthTracker.CreateRows())
        {
            StreamHealthRows.Add(row);
        }

        StreamHealthSummaryText = streamHealthTracker.SummaryText;
        if (ClearStreamHealthCommand is RelayCommand clear)
        {
            clear.RaiseCanExecuteChanged();
        }
    }

    private void ClearStreamHealth()
    {
        streamHealthTracker.Clear();
        StreamHealthRows.Clear();
        StreamHealthSummaryText = "Stream health cleared.";
        if (ClearStreamHealthCommand is RelayCommand clear)
        {
            clear.RaiseCanExecuteChanged();
        }
    }

    private static int? TryParseVodYear(string selectedYear)
    {
        return selectedYear == AllYearsOption
            ? null
            : int.TryParse(selectedYear, out int year) ? year : null;
    }

    private static bool HasUserState(ChannelUserState state)
    {
        return state.ChannelId != Guid.Empty &&
            (state.IsFavorite ||
                state.IsHidden ||
                state.HasExplicitVisibility ||
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

    private static string[] NormalizeGroups(IEnumerable<string>? groups)
    {
        return (groups ?? [])
            .Select(NormalizeCustomGroup)
            .Where(group => group is not null)
            .Select(group => group!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static int? NormalizeCustomSortIndex(int? value)
    {
        return value < 0 ? null : value;
    }

    private static int? NormalizeResumeProgress(int? value)
    {
        return value is null ? null : Math.Clamp(value.Value, 0, 100);
    }

    private static int NormalizeRefreshInterval(int value)
    {
        return Math.Clamp(value <= 0 ? 60 : value, 5, 24 * 60);
    }

    private static int NormalizeLogoCacheLimit(int value)
    {
        return Math.Clamp(value <= 0 ? 100 : value, 25, 500);
    }

    private static ProviderPlaybackProfile NormalizePlaybackProfile(ProviderPlaybackProfile profile)
    {
        BufferingPreset bufferingPreset = Enum.IsDefined(profile.BufferingPreset)
            ? profile.BufferingPreset
            : BufferingPreset.Balanced;
        return new ProviderPlaybackProfile
        {
            RetryCount = Math.Clamp(profile.RetryCount, 0, 3),
            BufferingPreset = bufferingPreset
        };
    }

    private static string? NormalizeSecret(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string? NormalizeRemoteUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return Uri.TryCreate(value.Trim(), UriKind.Absolute, out Uri? uri) &&
            uri.Scheme is "http" or "https"
            ? uri.ToString()
            : null;
    }

    private static Channel[] ExpandBenchmarkChannels(IReadOnlyList<Channel> channels, int targetCount)
    {
        if (channels.Count == 0)
        {
            return CreateSyntheticBenchmarkChannels(targetCount);
        }

        var expanded = new Channel[targetCount];
        for (int index = 0; index < expanded.Length; index++)
        {
            Channel source = channels[index % channels.Count];
            expanded[index] = source with
            {
                Id = StableId.Create("benchmark", $"{source.Id:N}-{index}"),
                ImportIndex = index,
                DisplayName = $"{source.DisplayName} {index:N0}",
                RawName = $"{source.RawName} {index:N0}",
                NormalizedName = ChannelNormalizer.NormalizeForSearch($"{source.DisplayName} {index:N0}"),
                ContentKind = index % 3 == 0 ? ContentKind.Vod : source.ContentKind,
                LastWatchedAt = index % 7 == 0 ? DateTimeOffset.UtcNow.AddMinutes(-index) : source.LastWatchedAt,
                ResumeProgressPercent = index % 11 == 0 ? index % 100 : source.ResumeProgressPercent
            };
        }

        return expanded;
    }

    private static Channel[] CreateSyntheticBenchmarkChannels(int count)
    {
        SensitiveUri.TryCreate("https://benchmark.example/stream.m3u8", out SensitiveUri? uri, out _);
        SensitiveUri streamUri = uri ?? throw new InvalidOperationException("Benchmark URI could not be created.");
        var channels = new Channel[count];
        for (int index = 0; index < channels.Length; index++)
        {
            string name = index % 3 == 0 ? $"Benchmark Movie {index:000000} (2025)" : $"Benchmark News {index:000000}";
            channels[index] = new Channel
            {
                Id = StableId.Create("benchmark", index.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                SourceId = StableId.Create("benchmark-source", "local"),
                RawName = name,
                DisplayName = name,
                NormalizedName = ChannelNormalizer.NormalizeForSearch(name),
                StreamUrl = streamUri,
                ImportIndex = index,
                GroupTitle = index % 3 == 0 ? "Benchmark VOD" : "Benchmark Live",
                Category = index % 3 == 0 ? "Movies" : "News",
                ContentKind = index % 3 == 0 ? ContentKind.Vod : ContentKind.LiveTv,
                LastWatchedAt = index % 7 == 0 ? DateTimeOffset.UtcNow.AddMinutes(-index) : null,
                ResumeProgressPercent = index % 11 == 0 ? index % 100 : null
            };
        }

        return channels;
    }

    private Channel[] GetSearchableChannels()
    {
        return allChannels.Where(IsChannelUnlockedForUi).ToArray();
    }

    private IEnumerable<Channel> ApplySmartViewFilter(IEnumerable<Channel> channels)
    {
        return SelectedSmartView switch
        {
            SmartViewFilter.UnwatchedMovies => channels.Where(channel =>
                channel.ContentKind is ContentKind.Vod or ContentKind.Series &&
                (channel.ResumeProgressPercent is null or <= 0)),
            SmartViewFilter.RecentlyAdded => channels.Where(channel =>
                channel.ImportedAt >= DateTimeOffset.UtcNow.AddDays(-7)),
            SmartViewFilter.FavoritesByGroup => channels.Where(channel => channel.IsFavorite),
            _ => channels
        };
    }

    private bool IsChannelUnlockedForUi(Channel channel)
    {
        return IsParentalUnlocked || !lockedGroups.Contains(channel.EffectiveGroupTitle);
    }

    private string? GetSelectedLockGroup()
    {
        if (SelectedManagedCustomGroup is not null)
        {
            return SelectedManagedCustomGroup;
        }

        if (!SelectedGroup.Equals(AllGroupsOption, StringComparison.OrdinalIgnoreCase))
        {
            return SelectedGroup;
        }

        return SelectedChannel?.EffectiveGroupTitle;
    }

    private void UpdateParentalLockStatus()
    {
        OnPropertyChanged(nameof(IsParentalLockConfigured));
        string lockState = IsParentalLockConfigured
            ? IsParentalUnlocked ? "unlocked" : "locked"
            : "not configured";
        ParentalLockStatusText = $"Parental lock: {lockState}; {lockedGroups.Count:N0} locked groups.";
        RefreshParentalLockCommandStates();
    }

    private static string CreateDuplicateKey(Channel channel)
    {
        return $"{(int)channel.ContentKind}|{channel.NormalizedName}|{ChannelNormalizer.NormalizeForSearch(channel.StreamUrl.Host)}";
    }

    private static string FormatTimelineProgram(EpgProgram? program, DateTimeOffset now)
    {
        if (program is null)
        {
            return "â€”";
        }

        string prefix = program.Start is not null && program.Stop is not null && program.Start <= now && program.Stop >= now
            ? "Now"
            : program.Start?.ToLocalTime().ToString("t") ?? "Time TBA";
        return $"{prefix}: {program.Title}";
    }

    private static DateTimeOffset GetEpgWindowStart(DateTimeOffset now, EpgTimelineWindow window)
    {
        DateTimeOffset localNow = now.ToLocalTime();
        DateTimeOffset localStart = window switch
        {
            EpgTimelineWindow.PlusTwoHours => localNow.AddHours(2),
            EpgTimelineWindow.Tonight => new DateTimeOffset(localNow.Year, localNow.Month, localNow.Day, 20, 0, 0, localNow.Offset),
            EpgTimelineWindow.Tomorrow => new DateTimeOffset(localNow.Year, localNow.Month, localNow.Day, 20, 0, 0, localNow.Offset).AddDays(1),
            _ => localNow
        };

        return localStart.ToUniversalTime();
    }

    private static async Task CopyWithLimitAsync(Stream source, Stream destination, long maxBytes, CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[64 * 1024];
        long totalBytes = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            totalBytes += read;
            if (totalBytes > maxBytes)
            {
                throw new InvalidDataException($"Download exceeds the configured {maxBytes:N0} byte limit.");
            }

            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }
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
        string favoriteText = channel.IsFavorite ? " | Favorite" : string.Empty;
        string logoText = string.IsNullOrWhiteSpace(channel.TvgLogo) ? " | Logo: none" : " | Logo: available";
        string resumeText = channel.ResumeProgressPercent is int progress ? $" | Resume: {progress}%" : string.Empty;
        return $"Group: {groupText} | Category: {channel.Category} | Type: {FormatContentKind(channel.ContentKind)} | Host: {channel.StreamUrl.Host}{favoriteText}{hiddenText}{logoText}{resumeText}";
    }

    private static string FormatSelectedChannelMetadata(Channel channel)
    {
        string tvgId = string.IsNullOrWhiteSpace(channel.TvgId) ? "not provided" : channel.TvgId;
        string tvgName = string.IsNullOrWhiteSpace(channel.TvgName) ? "not provided" : channel.TvgName;
        string customGroup = string.IsNullOrWhiteSpace(channel.CustomGroup) ? "source group" : channel.CustomGroup;
        string lastWatched = channel.LastWatchedAt is null
            ? "never"
            : channel.LastWatchedAt.Value.ToLocalTime().ToString("g");
        string resumeProgress = channel.ResumeProgressPercent is null
            ? "not set"
            : $"{channel.ResumeProgressPercent.Value}%";

        return
            $"Name: {channel.DisplayName}\n" +
            $"Content: {FormatContentKind(channel.ContentKind)}\n" +
            $"Source group: {channel.GroupTitle}\n" +
            $"Custom group: {customGroup}\n" +
            $"Category: {channel.Category}\n" +
            $"TVG ID: {tvgId}\n" +
            $"TVG name: {tvgName}\n" +
            $"Logo: {(string.IsNullOrWhiteSpace(channel.TvgLogo) ? "not provided" : "available")}\n" +
            $"Host: {channel.StreamUrl.Host}\n" +
            $"Imported index: {channel.ImportIndex:N0}\n" +
            $"Last watched: {lastWatched}\n" +
            $"Resume progress: {resumeProgress}";
    }

    private static string FormatVodDetail(Channel channel)
    {
        if (channel.ContentKind is not (ContentKind.Vod or ContentKind.Series))
        {
            return "Selected item is not VOD/series content. Resume controls are disabled by convention.";
        }

        string year = ChannelMetadataExtractor.TryInferReleaseYear(channel.DisplayName)?.ToString(System.Globalization.CultureInfo.InvariantCulture)
            ?? "unknown year";
        string progress = channel.ResumeProgressPercent is int resumeProgress
            ? $"{resumeProgress}%"
            : "not started";
        string artwork = string.IsNullOrWhiteSpace(channel.TvgLogo)
            ? "No poster/backdrop URL in playlist metadata."
            : "Poster/backdrop loaded from playlist logo metadata when supported.";

        return
            $"Title: {channel.DisplayName}\n" +
            $"Type: {FormatContentKind(channel.ContentKind)} Â· {year}\n" +
            $"Group: {channel.EffectiveGroupTitle}\n" +
            $"Resume: {progress}\n" +
            artwork;
    }

    private void QueueLogoLoad(Channel? channel)
    {
        logoCts?.Cancel();
        logoCts?.Dispose();
        SelectedChannelLogoPath = null;

        if (channel is null)
        {
            SelectedChannelLogoStatusText = "Logo: no channel selected.";
            logoCts = null;
            return;
        }

        if (string.IsNullOrWhiteSpace(channel.TvgLogo))
        {
            SelectedChannelLogoStatusText = "Logo: no logo provided.";
            logoCts = null;
            return;
        }

        string? cachedPath = logoCacheService.TryGetCachedLogoPath(channel.TvgLogo);
        if (cachedPath is not null)
        {
            SelectedChannelLogoPath = CanPreviewLogoPath(cachedPath) ? cachedPath : null;
            SelectedChannelLogoStatusText = SelectedChannelLogoPath is null
                ? "Logo: cached, but preview format is unsupported."
                : "Logo: loaded from cache.";
            logoCts = null;
            return;
        }

        logoCts = CancellationTokenSource.CreateLinkedTokenSource(shutdownCts.Token);
        SelectedChannelLogoStatusText = "Logo: loading...";
        _ = LoadSelectedLogoAsync(channel.Id, channel.TvgLogo, logoCts.Token);
    }

    private async Task LoadSelectedLogoAsync(Guid channelId, string logoUrl, CancellationToken cancellationToken)
    {
        try
        {
            LogoCacheResult result = await logoCacheService
                .CacheLogoAsync(logoUrl, logoHttpClient, cancellationToken)
                .ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();
            UiDispatcher.Run(() =>
            {
                if (SelectedChannel?.Id != channelId)
                {
                    return;
                }

                bool canPreview = result.Success && CanPreviewLogoPath(result.FilePath);
                SelectedChannelLogoPath = canPreview ? result.FilePath : null;
                SelectedChannelLogoStatusText = result.Success && !canPreview
                    ? "Logo: cached, but preview format is unsupported."
                    : result.Message;
                RefreshLogoCacheStatus();
            });
        }
        catch (OperationCanceledException)
        {
            // Expected when selection changes or the app shuts down.
        }
        catch (Exception ex)
        {
            string message = SensitiveTextRedactor.RedactText(ex.Message);
            UiDispatcher.Run(() =>
            {
                if (SelectedChannel?.Id == channelId)
                {
                    SelectedChannelLogoPath = null;
                    SelectedChannelLogoStatusText = $"Logo skipped: {message}";
                }

                AddDiagnostic($"Logo load failed: {message}");
            });
        }
    }

    private static string FormatContentKind(ContentKind contentKind)
    {
        return contentKind switch
        {
            ContentKind.LiveTv => "Live TV",
            ContentKind.Radio => "Radio",
            ContentKind.Vod => "VOD",
            ContentKind.Series => "Series",
            ContentKind.Unknown => "Unknown",
            _ => contentKind.ToString()
        };
    }

    private static bool CanPreviewLogoPath(string? path)
    {
        return Path.GetExtension(path) switch
        {
            ".bmp" or ".gif" or ".jpeg" or ".jpg" or ".png" => true,
            _ => false
        };
    }

    private void ApplyPlaybackState(PlaybackStateSnapshot state)
    {
        PlaybackStatusText = state.Channel is null
            ? state.Message
            : $"{state.Status}: {state.Channel.DisplayName} â€” {state.Message}";
        UpdateNowPlayingMarker(state);
        UpdateStreamHealth(state);
        AddDiagnostic(PlaybackStatusText);
    }

    private void ApplyPlaybackProgress(PlaybackProgressSnapshot progress)
    {
        PlaybackProgressText = progress.DisplayText;
        if (progress.Channel is null ||
            progress.ProgressPercent is not int percent ||
            progress.Channel.ContentKind is not (ContentKind.Vod or ContentKind.Series) ||
            percent is < 0 or > 98)
        {
            return;
        }

        int index = allChannels.FindIndex(channel => channel.Id == progress.Channel.Id);
        if (index < 0)
        {
            return;
        }

        Channel current = allChannels[index];
        int? previousProgress = current.ResumeProgressPercent;
        if (previousProgress == percent)
        {
            return;
        }

        Channel updated = current with
        {
            ResumeProgressPercent = percent,
            LastWatchedAt = DateTimeOffset.UtcNow
        };
        allChannels[index] = updated;
        UpdateChannelStateIndex(updated);
        if (SelectedChannel?.Id == updated.Id)
        {
            selectedChannel = updated;
            OnPropertyChanged(nameof(SelectedChannel));
            OnPropertyChanged(nameof(ResumeProgressText));
            SelectedVodDetailText = FormatVodDetail(updated);
        }

        if (previousProgress is null || Math.Abs(previousProgress.Value - percent) >= 5)
        {
            RefreshVodLibrary();
        }

        if (DateTimeOffset.UtcNow - lastResumeStateSaveAt > TimeSpan.FromSeconds(12))
        {
            lastResumeStateSaveAt = DateTimeOffset.UtcNow;
            _ = SaveChannelStatesSafelyAsync();
        }
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

        if (SetResume25Command is RelayCommand resume25)
        {
            resume25.RaiseCanExecuteChanged();
        }

        if (SetResume50Command is RelayCommand resume50)
        {
            resume50.RaiseCanExecuteChanged();
        }

        if (SetResume75Command is RelayCommand resume75)
        {
            resume75.RaiseCanExecuteChanged();
        }

        if (ClearResumeCommand is RelayCommand clearResume)
        {
            clearResume.RaiseCanExecuteChanged();
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

    private void RaiseProfileCommandStates()
    {
        if (RenameSourceProfileCommand is RelayCommand rename)
        {
            rename.RaiseCanExecuteChanged();
        }

        if (SaveSourcePlaybackProfileCommand is RelayCommand playbackProfile)
        {
            playbackProfile.RaiseCanExecuteChanged();
        }

        if (ImportSourceProfilesCommand is AsyncRelayCommand importProfiles)
        {
            importProfiles.RaiseCanExecuteChanged();
        }

        if (ExportSourceProfilesCommand is AsyncRelayCommand exportProfiles)
        {
            exportProfiles.RaiseCanExecuteChanged();
        }

        RaiseSourceDefaultVisibilityCommandStates();
    }

    private void RaiseRecentPlaylistCommandStates()
    {
        if (OpenRecentPlaylistSourceCommand is AsyncRelayCommand openRecent)
        {
            openRecent.RaiseCanExecuteChanged();
        }

        if (RenameRecentPlaylistSourceCommand is RelayCommand renameRecent)
        {
            renameRecent.RaiseCanExecuteChanged();
        }

        if (TogglePinRecentPlaylistSourceCommand is RelayCommand pinRecent)
        {
            pinRecent.RaiseCanExecuteChanged();
        }

        if (RemoveRecentPlaylistSourceCommand is RelayCommand removeRecent)
        {
            removeRecent.RaiseCanExecuteChanged();
        }

        if (ImportRecentPlaylistSourcesCommand is AsyncRelayCommand importRecent)
        {
            importRecent.RaiseCanExecuteChanged();
        }

        if (ExportRecentPlaylistSourcesCommand is AsyncRelayCommand exportRecent)
        {
            exportRecent.RaiseCanExecuteChanged();
        }

        if (ClearRecentPlaylistSourcesCommand is RelayCommand clearRecent)
        {
            clearRecent.RaiseCanExecuteChanged();
        }

        OnPropertyChanged(nameof(PinRecentPlaylistSourceLabel));
    }

    private void RaiseSourceDefaultVisibilityCommandStates()
    {
        if (HideSourceDefaultGroupCommand is RelayCommand hideDefault)
        {
            hideDefault.RaiseCanExecuteChanged();
        }

        if (ShowSourceDefaultGroupCommand is RelayCommand showDefault)
        {
            showDefault.RaiseCanExecuteChanged();
        }
    }

    private void RefreshParentalLockCommandStates()
    {
        if (LockParentalControlsCommand is RelayCommand lockControls)
        {
            lockControls.RaiseCanExecuteChanged();
        }

        if (LockSelectedGroupCommand is RelayCommand lockGroup)
        {
            lockGroup.RaiseCanExecuteChanged();
        }

        if (UnlockSelectedGroupCommand is RelayCommand unlockGroup)
        {
            unlockGroup.RaiseCanExecuteChanged();
        }

        if (ClearParentalPinCommand is RelayCommand clearPin)
        {
            clearPin.RaiseCanExecuteChanged();
        }
    }

    private void RaiseConflictCommandStates()
    {
        if (ClearRemovedConflictStatesCommand is RelayCommand clear)
        {
            clear.RaiseCanExecuteChanged();
        }
    }

    private void RaiseSmartGroupPresetCommandStates()
    {
        if (ExportSmartGroupPresetsCommand is AsyncRelayCommand export)
        {
            export.RaiseCanExecuteChanged();
        }

        if (UseSmartGroupPresetCommand is RelayCommand use)
        {
            use.RaiseCanExecuteChanged();
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

        if (OpenRecentPlaylistSourceCommand is AsyncRelayCommand openRecent)
        {
            openRecent.RaiseCanExecuteChanged();
        }

        if (ImportRecentPlaylistSourcesCommand is AsyncRelayCommand importRecent)
        {
            importRecent.RaiseCanExecuteChanged();
        }

        if (ExportRecentPlaylistSourcesCommand is AsyncRelayCommand exportRecent)
        {
            exportRecent.RaiseCanExecuteChanged();
        }

        if (RefreshPlaylistCommand is AsyncRelayCommand refresh)
        {
            refresh.RaiseCanExecuteChanged();
        }

        if (CancelImportCommand is RelayCommand cancelImport)
        {
            cancelImport.RaiseCanExecuteChanged();
        }

        if (ImportEpgCommand is AsyncRelayCommand epg)
        {
            epg.RaiseCanExecuteChanged();
        }

        if (ImportEpgUrlCommand is AsyncRelayCommand epgUrl)
        {
            epgUrl.RaiseCanExecuteChanged();
        }

        if (ImportCustomGroupCsvCommand is AsyncRelayCommand importCustomGroupCsv)
        {
            importCustomGroupCsv.RaiseCanExecuteChanged();
        }

        if (ExportCustomGroupCsvCommand is AsyncRelayCommand exportCustomGroupCsv)
        {
            exportCustomGroupCsv.RaiseCanExecuteChanged();
        }

        if (RunSearchBenchmarkCommand is AsyncRelayCommand benchmark)
        {
            benchmark.RaiseCanExecuteChanged();
        }

        if (ImportOrganizationCommand is AsyncRelayCommand importOrganization)
        {
            importOrganization.RaiseCanExecuteChanged();
        }

        if (ExportOrganizationCommand is AsyncRelayCommand exportOrganization)
        {
            exportOrganization.RaiseCanExecuteChanged();
        }

        if (ExportDiagnosticsCommand is AsyncRelayCommand exportDiagnostics)
        {
            exportDiagnostics.RaiseCanExecuteChanged();
        }

        if (ImportSmartGroupPresetsCommand is AsyncRelayCommand importPresets)
        {
            importPresets.RaiseCanExecuteChanged();
        }

        if (ExportSmartGroupPresetsCommand is AsyncRelayCommand exportPresets)
        {
            exportPresets.RaiseCanExecuteChanged();
        }

        if (PrefetchVisibleLogosCommand is AsyncRelayCommand logoPrefetch)
        {
            logoPrefetch.RaiseCanExecuteChanged();
        }

        if (TrimLogoCacheCommand is AsyncRelayCommand trimLogos)
        {
            trimLogos.RaiseCanExecuteChanged();
        }

        if (ClearLogoCacheCommand is AsyncRelayCommand clearLogos)
        {
            clearLogos.RaiseCanExecuteChanged();
        }
    }

    private void RaiseAuditCommandStates()
    {
        if (UnhideAuditGroupCommand is RelayCommand unhide)
        {
            unhide.RaiseCanExecuteChanged();
        }

        if (UnlockAuditGroupCommand is RelayCommand unlock)
        {
            unlock.RaiseCanExecuteChanged();
        }
    }

    private void RaisePendingRefreshCommandStates()
    {
        if (ApplyPendingRefreshCommand is AsyncRelayCommand apply)
        {
            apply.RaiseCanExecuteChanged();
        }

        if (DiscardPendingRefreshCommand is RelayCommand discard)
        {
            discard.RaiseCanExecuteChanged();
        }
    }

    private void QueueResumeSeek(Channel channel)
    {
        if (channel.ContentKind is not (ContentKind.Vod or ContentKind.Series) ||
            channel.ResumeProgressPercent is not int progress ||
            progress is <= 0 or >= 95)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(900), shutdownCts.Token).ConfigureAwait(false);
                await playbackEngine.SeekToProgressAsync(progress, shutdownCts.Token).ConfigureAwait(false);
                UiDispatcher.Run(() => AddDiagnostic($"Resume seek requested for '{channel.DisplayName}' at {progress:N0}%."));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                UiDispatcher.Run(() => AddDiagnostic($"Resume seek skipped: {SensitiveTextRedactor.RedactText(ex.Message)}"));
            }
        }, shutdownCts.Token);
    }

    private void RaiseVodPageCommandStates()
    {
        if (PreviousVodPageCommand is RelayCommand previous)
        {
            previous.RaiseCanExecuteChanged();
        }

        if (NextVodPageCommand is RelayCommand next)
        {
            next.RaiseCanExecuteChanged();
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

        if (ExportDiagnosticsCommand is AsyncRelayCommand exportDiagnostics)
        {
            exportDiagnostics.RaiseCanExecuteChanged();
        }
    }

    private sealed record ChannelUndoAction(string Description, ChannelUndoSnapshot[] Snapshots);

    private sealed record ChannelUndoSnapshot(
        Guid ChannelId,
        bool IsFavorite,
        bool IsHidden,
        bool HasExplicitVisibility,
        string? CustomGroup,
        int? CustomSortIndex,
        DateTimeOffset? LastWatchedAt,
        int? ResumeProgressPercent)
    {
        public static ChannelUndoSnapshot FromChannel(Channel channel, bool hasExplicitVisibility)
        {
            return new ChannelUndoSnapshot(
                channel.Id,
                channel.IsFavorite,
                channel.IsHidden,
                hasExplicitVisibility,
                channel.CustomGroup,
                channel.CustomSortIndex,
                channel.LastWatchedAt,
                channel.ResumeProgressPercent);
        }

        public Channel Apply(Channel channel)
        {
            return channel with
            {
                IsFavorite = IsFavorite,
                IsHidden = IsHidden,
                CustomGroup = CustomGroup,
                CustomSortIndex = CustomSortIndex,
                LastWatchedAt = LastWatchedAt,
                ResumeProgressPercent = ResumeProgressPercent
            };
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
