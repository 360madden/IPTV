using System.Windows;
using System.Windows.Input;
using Iptv.App.Playback;
using Iptv.App.Services;
using Iptv.App.ViewModels;
using Iptv.Epg;
using Iptv.Persistence;
using Iptv.Playback;
using Iptv.Playlists;
using Iptv.Search;

namespace Iptv.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel viewModel;
    private WindowState previousWindowState;
    private WindowStyle previousWindowStyle;
    private ResizeMode previousResizeMode;
    private bool isFullscreen;

    public MainWindow()
    {
        InitializeComponent();

        var parser = new M3uPlaylistParser();
        var importService = new PlaylistImportService(parser);
        var searchService = new ChannelSearchService();
        IPlaybackEngine playbackEngine = CreatePlaybackEngine();
        var stateStore = new JsonChannelStateStore();
        var epgImportService = new XmltvImportService();
        var dialogService = new PlaylistDialogService();

        viewModel = new MainViewModel(
            importService,
            searchService,
            playbackEngine,
            stateStore,
            epgImportService,
            dialogService);
        DataContext = viewModel;

        Loaded += async (_, _) => await viewModel.InitializeAsync().ConfigureAwait(true);
    }

    private IPlaybackEngine CreatePlaybackEngine()
    {
        try
        {
            return new LibVlcPlaybackEngine(PlayerView);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                $"LibVLC playback could not be initialized. The app will remain usable for playlist import/search, but video playback is disabled.\n\n{ex.Message}",
                "Playback initialization failed",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return new DisabledPlaybackEngine("Video playback is disabled because LibVLC could not be initialized.");
        }
    }

    private void Channels_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (viewModel.PlaySelectedCommand.CanExecute(null))
        {
            viewModel.PlaySelectedCommand.Execute(null);
        }
    }

    private async void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        await viewModel.DisposeAsync().ConfigureAwait(true);
    }

    private void Fullscreen_Click(object sender, RoutedEventArgs e)
    {
        if (!isFullscreen)
        {
            previousWindowState = WindowState;
            previousWindowStyle = WindowStyle;
            previousResizeMode = ResizeMode;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            WindowState = WindowState.Maximized;
            Topmost = true;
            isFullscreen = true;
            return;
        }

        Topmost = false;
        WindowStyle = previousWindowStyle;
        ResizeMode = previousResizeMode;
        WindowState = previousWindowState;
        isFullscreen = false;
    }
}
