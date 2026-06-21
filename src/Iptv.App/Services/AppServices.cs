using System.IO;
using Iptv.App.ViewModels;
using Iptv.Epg;
using Iptv.Persistence;
using Iptv.Persistence.Logos;
using Iptv.Persistence.RecentPlaylists;
using Iptv.Persistence.SmartGroups;
using Iptv.Persistence.SourceProfiles;
using Iptv.Playback;
using Iptv.Playlists;
using Iptv.Search;

namespace Iptv.App.Services;

public static class AppServices
{
    private const string AppDataOverrideEnvironmentVariable = "IPTV_VIEWER_APPDATA_DIR";

    public static MainViewModel CreateMainViewModel(IPlaybackEngine playbackEngine)
    {
        ArgumentNullException.ThrowIfNull(playbackEngine);

        string? appDataDirectory = GetAppDataDirectoryOverride();
        var parser = new M3uPlaylistParser();
        var importService = new PlaylistImportService(parser);
        var searchService = new ChannelSearchService();
        var stateStore = new JsonChannelStateStore(appDataDirectory);
        var organizationPreferencesStore = new JsonChannelOrganizationPreferencesStore(appDataDirectory);
        var organizationBackupService = new JsonChannelOrganizationBackupService();
        var logoCacheService = new LogoCacheService(appDataDirectory is null ? null : Path.Combine(appDataDirectory, "logos"));
        var recentPlaylistSourceFileService = new JsonRecentPlaylistSourceFileService();
        var sourceProfileFileService = new JsonSourceProfileFileService();
        var smartGroupPresetFileService = new JsonSmartGroupPresetFileService();
        var uiPreferencesStore = new JsonUiPreferencesStore(appDataDirectory);
        var epgImportService = new XmltvImportService();
        var themeService = new ThemeService();
        var dialogService = new PlaylistDialogService();

        return new MainViewModel(
            importService,
            searchService,
            playbackEngine,
            stateStore,
            organizationPreferencesStore,
            organizationBackupService,
            logoCacheService,
            recentPlaylistSourceFileService,
            sourceProfileFileService,
            smartGroupPresetFileService,
            uiPreferencesStore,
            themeService,
            epgImportService,
            dialogService);
    }

    public static string? GetAppDataDirectoryOverride()
    {
        string? value = Environment.GetEnvironmentVariable(AppDataOverrideEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return Path.GetFullPath(Environment.ExpandEnvironmentVariables(value.Trim().Trim('"')));
    }
}
