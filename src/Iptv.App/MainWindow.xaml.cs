using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
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
        ToggleFullscreen();
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        Key key = e.Key == Key.System ? e.SystemKey : e.Key;

        if (key == Key.F11 || (key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Alt))
        {
            ToggleFullscreen();
            e.Handled = true;
            return;
        }

        if (key == Key.Escape && isFullscreen)
        {
            ExitFullscreen();
            e.Handled = true;
            return;
        }

        bool textInputFocused = IsFocusWithin<TextBoxBase>() || IsFocusWithin<PasswordBox>();
        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            switch (key)
            {
                case Key.F:
                    SearchBox.Focus();
                    SearchBox.SelectAll();
                    e.Handled = true;
                    return;
                case Key.L when TryExecute(viewModel.ImportUrlCommand):
                case Key.O when TryExecute(viewModel.ImportFileCommand):
                case Key.R when TryExecute(viewModel.RefreshPlaylistCommand):
                    e.Handled = true;
                    return;
            }
        }

        if (textInputFocused ||
            IsFocusWithin<ComboBox>() ||
            IsFocusWithin<ComboBoxItem>() ||
            IsFocusWithin<Popup>() ||
            IsFocusWithin<Slider>())
        {
            return;
        }

        if (key == Key.Space && IsFocusWithin<ButtonBase>())
        {
            return;
        }

        if (Keyboard.Modifiers != ModifierKeys.None)
        {
            return;
        }

        switch (key)
        {
            case Key.Space when TryExecute(viewModel.PlaySelectedCommand):
            case Key.P when TryExecute(viewModel.PauseCommand):
            case Key.S when TryExecute(viewModel.StopCommand):
                e.Handled = true;
                break;
            case Key.F:
                ToggleFullscreen();
                e.Handled = true;
                break;
        }
    }

    private void ToggleFullscreen()
    {
        if (!isFullscreen)
        {
            EnterFullscreen();
            return;
        }

        ExitFullscreen();
    }

    private void EnterFullscreen()
    {
        previousWindowState = WindowState;
        previousWindowStyle = WindowStyle;
        previousResizeMode = ResizeMode;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        WindowState = WindowState.Maximized;
        Topmost = true;
        isFullscreen = true;
    }

    private void ExitFullscreen()
    {
        Topmost = false;
        WindowStyle = previousWindowStyle;
        ResizeMode = previousResizeMode;
        WindowState = previousWindowState;
        isFullscreen = false;
    }

    private static bool TryExecute(ICommand command)
    {
        if (!command.CanExecute(null))
        {
            return false;
        }

        command.Execute(null);
        return true;
    }

    private static bool IsFocusWithin<T>() where T : DependencyObject
    {
        DependencyObject? current = Keyboard.FocusedElement as DependencyObject;
        while (current is not null)
        {
            if (current is T)
            {
                return true;
            }

            current = GetElementParent(current);
        }

        return false;
    }

    private static DependencyObject? GetElementParent(DependencyObject current)
    {
        if (current is Visual)
        {
            DependencyObject? visualParent = VisualTreeHelper.GetParent(current);
            if (visualParent is not null)
            {
                return visualParent;
            }
        }

        if (current is FrameworkElement frameworkElement && frameworkElement.Parent is not null)
        {
            return frameworkElement.Parent;
        }

        if (current is FrameworkContentElement frameworkContentElement && frameworkContentElement.Parent is not null)
        {
            return frameworkContentElement.Parent;
        }

        return LogicalTreeHelper.GetParent(current);
    }
}
