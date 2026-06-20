using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
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
    private Rect previousWindowBounds;
    private double previousMinHeight;
    private double previousMinWidth;
    private Thickness previousMainContentMargin;
    private Thickness previousPlayerPanelMargin;
    private Thickness previousPlayerPanelPadding;
    private CornerRadius previousPlayerPanelCornerRadius;
    private CornerRadius previousPlayerVideoCornerRadius;
    private Brush? previousRootBackground;
    private Brush? previousPlayerPanelBackground;
    private GridLength previousPlayerVideoRowHeight;
    private GridLength previousPlayerDetailsRowHeight;
    private GridLength previousPlayerControlsRowHeight;
    private GridLength previousPlayerDiagnosticsRowHeight;
    private int previousMainContentRow;
    private int previousMainContentRowSpan;
    private int previousPlayerPanelColumn;
    private int previousPlayerPanelColumnSpan;
    private bool isFullscreen;

    public MainWindow()
    {
        InitializeComponent();

        var parser = new M3uPlaylistParser();
        var importService = new PlaylistImportService(parser);
        var searchService = new ChannelSearchService();
        IPlaybackEngine playbackEngine = CreatePlaybackEngine();
        var stateStore = new JsonChannelStateStore();
        var uiPreferencesStore = new JsonUiPreferencesStore();
        var epgImportService = new XmltvImportService();
        var dialogService = new PlaylistDialogService();

        viewModel = new MainViewModel(
            importService,
            searchService,
            playbackEngine,
            stateStore,
            uiPreferencesStore,
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
            case Key.C when TryExecute(viewModel.Clock.ToggleClockCommand):
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
        previousWindowBounds = WindowState == WindowState.Maximized
            ? RestoreBounds
            : new Rect(Left, Top, Width, Height);
        previousMinHeight = MinHeight;
        previousMinWidth = MinWidth;
        SaveLayoutState();

        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        MinHeight = 0;
        MinWidth = 0;
        WindowState = WindowState.Normal;
        Rect monitorBounds = MonitorBounds.GetForWindow(this);
        Left = monitorBounds.Left;
        Top = monitorBounds.Top;
        Width = monitorBounds.Width;
        Height = monitorBounds.Height;
        Topmost = true;
        ApplyVideoFullscreenLayout();
        isFullscreen = true;
        Activate();
    }

    private void ExitFullscreen()
    {
        if (!isFullscreen)
        {
            return;
        }

        RestoreLayoutState();
        Topmost = false;
        WindowStyle = previousWindowStyle;
        ResizeMode = previousResizeMode;
        MinHeight = previousMinHeight;
        MinWidth = previousMinWidth;
        WindowState = WindowState.Normal;
        Left = previousWindowBounds.Left;
        Top = previousWindowBounds.Top;
        Width = previousWindowBounds.Width;
        Height = previousWindowBounds.Height;
        WindowState = previousWindowState;
        isFullscreen = false;
        Activate();
    }

    private void SaveLayoutState()
    {
        previousRootBackground = RootGrid.Background;
        previousPlayerPanelBackground = PlayerPanel.Background;
        previousMainContentMargin = MainContentGrid.Margin;
        previousPlayerPanelMargin = PlayerPanel.Margin;
        previousPlayerPanelPadding = PlayerPanel.Padding;
        previousPlayerPanelCornerRadius = PlayerPanel.CornerRadius;
        previousPlayerVideoCornerRadius = PlayerVideoHost.CornerRadius;
        previousPlayerVideoRowHeight = PlayerVideoRow.Height;
        previousPlayerDetailsRowHeight = PlayerDetailsRow.Height;
        previousPlayerControlsRowHeight = PlayerControlsRow.Height;
        previousPlayerDiagnosticsRowHeight = PlayerDiagnosticsRow.Height;
        previousMainContentRow = Grid.GetRow(MainContentGrid);
        previousMainContentRowSpan = Grid.GetRowSpan(MainContentGrid);
        previousPlayerPanelColumn = Grid.GetColumn(PlayerPanel);
        previousPlayerPanelColumnSpan = Grid.GetColumnSpan(PlayerPanel);
    }

    private void ApplyVideoFullscreenLayout()
    {
        HeaderBar.Visibility = Visibility.Collapsed;
        FooterBar.Visibility = Visibility.Collapsed;
        LibraryPanel.Visibility = Visibility.Collapsed;
        ChannelsPanel.Visibility = Visibility.Collapsed;
        PlayerDetailsPanel.Visibility = Visibility.Collapsed;
        PlayerControlsPanel.Visibility = Visibility.Collapsed;
        DiagnosticsPanel.Visibility = Visibility.Collapsed;

        RootGrid.Background = Brushes.Black;
        MainContentGrid.Margin = new Thickness(0);
        PlayerPanel.Margin = new Thickness(0);
        PlayerPanel.Padding = new Thickness(0);
        PlayerPanel.CornerRadius = new CornerRadius(0);
        PlayerPanel.Background = Brushes.Black;
        PlayerVideoHost.CornerRadius = new CornerRadius(0);

        Grid.SetRow(MainContentGrid, 0);
        Grid.SetRowSpan(MainContentGrid, 3);
        Grid.SetColumn(PlayerPanel, 0);
        Grid.SetColumnSpan(PlayerPanel, 3);

        PlayerVideoRow.Height = new GridLength(1, GridUnitType.Star);
        PlayerDetailsRow.Height = new GridLength(0);
        PlayerControlsRow.Height = new GridLength(0);
        PlayerDiagnosticsRow.Height = new GridLength(0);
    }

    private void RestoreLayoutState()
    {
        HeaderBar.Visibility = Visibility.Visible;
        FooterBar.Visibility = Visibility.Visible;
        LibraryPanel.Visibility = Visibility.Visible;
        ChannelsPanel.Visibility = Visibility.Visible;
        PlayerDetailsPanel.Visibility = Visibility.Visible;
        PlayerControlsPanel.Visibility = Visibility.Visible;
        DiagnosticsPanel.Visibility = Visibility.Visible;

        RootGrid.Background = previousRootBackground;
        MainContentGrid.Margin = previousMainContentMargin;
        PlayerPanel.Margin = previousPlayerPanelMargin;
        PlayerPanel.Padding = previousPlayerPanelPadding;
        PlayerPanel.CornerRadius = previousPlayerPanelCornerRadius;
        PlayerPanel.Background = previousPlayerPanelBackground;
        PlayerVideoHost.CornerRadius = previousPlayerVideoCornerRadius;

        Grid.SetRow(MainContentGrid, previousMainContentRow);
        Grid.SetRowSpan(MainContentGrid, previousMainContentRowSpan);
        Grid.SetColumn(PlayerPanel, previousPlayerPanelColumn);
        Grid.SetColumnSpan(PlayerPanel, previousPlayerPanelColumnSpan);

        PlayerVideoRow.Height = previousPlayerVideoRowHeight;
        PlayerDetailsRow.Height = previousPlayerDetailsRowHeight;
        PlayerControlsRow.Height = previousPlayerControlsRowHeight;
        PlayerDiagnosticsRow.Height = previousPlayerDiagnosticsRowHeight;
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

    private static class MonitorBounds
    {
        private const int MonitorDefaultToNearest = 2;

        public static Rect GetForWindow(Window window)
        {
            IntPtr windowHandle = new WindowInteropHelper(window).Handle;
            IntPtr monitor = MonitorFromWindow(windowHandle, MonitorDefaultToNearest);
            var monitorInfo = new MonitorInfo
            {
                Size = Marshal.SizeOf<MonitorInfo>()
            };

            if (monitor != IntPtr.Zero && GetMonitorInfo(monitor, ref monitorInfo))
            {
                int width = monitorInfo.Monitor.Right - monitorInfo.Monitor.Left;
                int height = monitorInfo.Monitor.Bottom - monitorInfo.Monitor.Top;
                return new Rect(monitorInfo.Monitor.Left, monitorInfo.Monitor.Top, width, height);
            }

            return new Rect(0, 0, SystemParameters.PrimaryScreenWidth, SystemParameters.PrimaryScreenHeight);
        }

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, int dwFlags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfo lpmi);

        [StructLayout(LayoutKind.Sequential)]
        private struct MonitorInfo
        {
            public int Size;
            public NativeRect Monitor;
            public NativeRect WorkArea;
            public uint Flags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }
    }
}
