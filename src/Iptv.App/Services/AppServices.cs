using Iptv.App.ViewModels;
using Iptv.Epg;
using Iptv.Persistence;
using Iptv.Persistence.Logos;
using Iptv.Persistence.SmartGroups;
using Iptv.Playback;
using Iptv.Playlists;
using Iptv.Search;

namespace Iptv.App.Services;

public static class AppServices
{
    public static MainViewModel CreateMainViewModel(IPlaybackEngine playbackEngine)
    {
        ArgumentNullException.ThrowIfNull(playbackEngine);

        var parser = new M3uPlaylistParser();
        var importService = new PlaylistImportService(parser);
        var searchService = new ChannelSearchService();
        var stateStore = new JsonChannelStateStore();
        var organizationPreferencesStore = new JsonChannelOrganizationPreferencesStore();
        var organizationBackupService = new JsonChannelOrganizationBackupService();
        var logoCacheService = new LogoCacheService();
        var smartGroupPresetFileService = new JsonSmartGroupPresetFileService();
        var uiPreferencesStore = new JsonUiPreferencesStore();
        var epgImportService = new XmltvImportService();
        var dialogService = new PlaylistDialogService();

        return new MainViewModel(
            importService,
            searchService,
            playbackEngine,
            stateStore,
            organizationPreferencesStore,
            organizationBackupService,
            logoCacheService,
            smartGroupPresetFileService,
            uiPreferencesStore,
            epgImportService,
            dialogService);
    }
}
