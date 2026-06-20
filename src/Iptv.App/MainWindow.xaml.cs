using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Iptv.App.Playback;
using Iptv.App.Services;
using Iptv.App.ViewModels;
using Iptv.Core.Channels;
using Iptv.Epg;
using Iptv.Persistence;
using Iptv.Persistence.Logos;
using Iptv.Persistence.SmartGroups;
using Iptv.Playback;
using Iptv.Playlists;
using Iptv.Search;

namespace Iptv.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel viewModel;
    private readonly DispatcherTimer fullscreenControlsHideTimer;
    private readonly string? startupPlaylistUrl;
    private WindowState previousWindowState;
    private WindowStyle previousWindowStyle;
    private ResizeMode previousResizeMode;
    private Rect previousWindowBounds;
    private double previousMinHeight;
    private double previousMinWidth;
    private bool previousTopmost;
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
    private Point channelDragStartPoint;
    private bool isFullscreen;

    public MainWindow(string? startupPlaylistUrl = null)
    {
        this.startupPlaylistUrl = string.IsNullOrWhiteSpace(startupPlaylistUrl) ? null : startupPlaylistUrl.Trim();
        InitializeComponent();

        var parser = new M3uPlaylistParser();
        var importService = new PlaylistImportService(parser);
        var searchService = new ChannelSearchService();
        IPlaybackEngine playbackEngine = CreatePlaybackEngine();
        var stateStore = new JsonChannelStateStore();
        var organizationPreferencesStore = new JsonChannelOrganizationPreferencesStore();
        var organizationBackupService = new JsonChannelOrganizationBackupService();
        var logoCacheService = new LogoCacheService();
        var smartGroupPresetFileService = new JsonSmartGroupPresetFileService();
        var uiPreferencesStore = new JsonUiPreferencesStore();
        var epgImportService = new XmltvImportService();
        var dialogService = new PlaylistDialogService();

        viewModel = new MainViewModel(
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
        DataContext = viewModel;

        fullscreenControlsHideTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(3)
        };
        fullscreenControlsHideTimer.Tick += (_, _) => HideFullscreenHudIfAutoHide();
        viewModel.Clock.PropertyChanged += Clock_PropertyChanged;

        Loaded += Window_Loaded;
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

    private void Channels_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ListBox listBox)
        {
            viewModel.SetSelectedChannels(listBox.SelectedItems.OfType<Channel>());
        }
    }

    private void SelectAllVisible_Click(object sender, RoutedEventArgs e)
    {
        SelectAllVisibleChannels();
        e.Handled = true;
    }

    private void ClearSelection_Click(object sender, RoutedEventArgs e)
    {
        ClearChannelSelection();
        e.Handled = true;
    }

    private void Channels_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        channelDragStartPoint = e.GetPosition(null);
    }

    private void Channels_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        Point currentPosition = e.GetPosition(null);
        if (Math.Abs(currentPosition.X - channelDragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(currentPosition.Y - channelDragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        if (FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject) is not { DataContext: Channel channel } item)
        {
            return;
        }

        DragDrop.DoDragDrop(item, channel, DragDropEffects.Move);
    }

    private void Channels_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = DragDropEffects.None;
        if (e.Data.GetDataPresent(typeof(Channel)) &&
            e.Data.GetData(typeof(Channel)) is Channel dragged &&
            FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject) is { DataContext: Channel target } &&
            viewModel.CanDropChannelOn(dragged, target))
        {
            e.Effects = DragDropEffects.Move;
        }

        e.Handled = true;
    }

    private void Channels_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(typeof(Channel)) &&
            e.Data.GetData(typeof(Channel)) is Channel dragged &&
            FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject) is { DataContext: Channel target })
        {
            viewModel.MoveChannelBefore(dragged.Id, target.Id);
            e.Handled = true;
        }
    }

    private async void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        fullscreenControlsHideTimer.Stop();
        viewModel.Clock.PropertyChanged -= Clock_PropertyChanged;
        await viewModel.DisposeAsync().ConfigureAwait(true);
    }

    private void Fullscreen_Click(object sender, RoutedEventArgs e)
    {
        ToggleFullscreen();
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        viewModel.Clock.SetMonitorOptions(MonitorBounds.GetMonitorOptions(this));
        await viewModel.InitializeAsync().ConfigureAwait(true);
        viewModel.Clock.SetMonitorOptions(MonitorBounds.GetMonitorOptions(this));

        if (startupPlaylistUrl is not null)
        {
            await viewModel.ImportPlaylistUrlAsync(startupPlaylistUrl).ConfigureAwait(true);
        }
    }

    private void Clock_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ClockOverlayViewModel.AutoHideFullscreenControls) && isFullscreen)
        {
            ShowFullscreenHud();
        }
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
                case Key.A when !textInputFocused && !IsFocusWithin<ComboBox>() && !IsFocusWithin<ComboBoxItem>():
                    SelectAllVisibleChannels();
                    e.Handled = true;
                    return;
                case Key.D when !textInputFocused:
                    ClearChannelSelection();
                    e.Handled = true;
                    return;
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
            case Key.V when TryExecute(viewModel.ToggleFavoriteCommand):
            case Key.H when TryExecute(viewModel.ToggleHiddenCommand):
            case Key.B when TryExecute(viewModel.BatchFavoriteCommand):
            case Key.Delete when TryExecute(viewModel.BatchHideCommand):
            case Key.U when TryExecute(viewModel.BatchUnhideCommand):
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
        previousTopmost = Topmost;
        SaveLayoutState();

        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        MinHeight = 0;
        MinWidth = 0;
        WindowState = WindowState.Normal;
        Rect monitorBounds = MonitorBounds.GetForPreference(this, viewModel.Clock.FullscreenMonitorIndex);
        Left = monitorBounds.Left;
        Top = monitorBounds.Top;
        Width = monitorBounds.Width;
        Height = monitorBounds.Height;
        Topmost = true;
        ApplyVideoFullscreenLayout();
        isFullscreen = true;
        ShowFullscreenHud();
        Activate();
    }

    private void ExitFullscreen()
    {
        if (!isFullscreen)
        {
            return;
        }

        RestoreLayoutState();
        fullscreenControlsHideTimer.Stop();
        FullscreenHudOverlay.Visibility = Visibility.Collapsed;
        Topmost = previousTopmost;
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
        FullscreenHudOverlay.Visibility = Visibility.Visible;

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
        FullscreenHudOverlay.Visibility = Visibility.Collapsed;

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

    private void FullscreenExit_Click(object sender, RoutedEventArgs e)
    {
        ExitFullscreen();
        e.Handled = true;
    }

    private void PlayerOverlay_MouseMove(object sender, MouseEventArgs e)
    {
        if (isFullscreen)
        {
            ShowFullscreenHud();
        }
    }

    private void PlayerOverlay_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount < 2)
        {
            return;
        }

        if (IsEventFromInteractiveElement(e.OriginalSource as DependencyObject))
        {
            return;
        }

        ToggleFullscreen();
        e.Handled = true;
    }

    private void ShowFullscreenHud()
    {
        if (!isFullscreen)
        {
            FullscreenHudOverlay.Visibility = Visibility.Collapsed;
            return;
        }

        FullscreenHudOverlay.Visibility = Visibility.Visible;
        fullscreenControlsHideTimer.Stop();
        if (viewModel.Clock.AutoHideFullscreenControls)
        {
            fullscreenControlsHideTimer.Start();
        }
    }

    private void HideFullscreenHudIfAutoHide()
    {
        fullscreenControlsHideTimer.Stop();
        if (isFullscreen && viewModel.Clock.AutoHideFullscreenControls)
        {
            FullscreenHudOverlay.Visibility = Visibility.Collapsed;
        }
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

    private void SelectAllVisibleChannels()
    {
        ChannelsListBox.SelectAll();
        viewModel.SetSelectedChannels(ChannelsListBox.SelectedItems.OfType<Channel>());
    }

    private void ClearChannelSelection()
    {
        ChannelsListBox.UnselectAll();
        viewModel.SetSelectedChannels(Array.Empty<Channel>());
    }

    private static T? FindAncestor<T>(DependencyObject? source) where T : DependencyObject
    {
        DependencyObject? current = source;
        while (current is not null)
        {
            if (current is T target)
            {
                return target;
            }

            current = GetElementParent(current);
        }

        return null;
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

    private static bool IsEventFromInteractiveElement(DependencyObject? source)
    {
        DependencyObject? current = source;
        while (current is not null)
        {
            if (current is ButtonBase or TextBoxBase or PasswordBox or ComboBox or ComboBoxItem or Slider or Thumb)
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
        private const uint MonitorInfoPrimary = 1;

        public static IReadOnlyList<FullscreenMonitorOption> GetMonitorOptions(Window window)
        {
            List<FullscreenMonitorOption> options = [new(-1, "Current window monitor")];
            List<MonitorRecord> monitors = EnumerateMonitors();
            for (int index = 0; index < monitors.Count; index++)
            {
                MonitorRecord monitor = monitors[index];
                string primary = monitor.IsPrimary ? " · Primary" : string.Empty;
                options.Add(new FullscreenMonitorOption(
                    index,
                    $"Monitor {index + 1}{primary} · {monitor.Bounds.Width:0}x{monitor.Bounds.Height:0}"));
            }

            if (options.Count == 1)
            {
                Rect current = GetForWindow(window);
                options.Add(new FullscreenMonitorOption(0, $"Primary monitor · {current.Width:0}x{current.Height:0}"));
            }

            return options;
        }

        public static Rect GetForPreference(Window window, int monitorIndex)
        {
            if (monitorIndex >= 0)
            {
                List<MonitorRecord> monitors = EnumerateMonitors();
                if (monitorIndex < monitors.Count)
                {
                    return monitors[monitorIndex].Bounds;
                }
            }

            return GetForWindow(window);
        }

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

        private static List<MonitorRecord> EnumerateMonitors()
        {
            List<MonitorRecord> monitors = [];
            MonitorEnumProc callback = (IntPtr monitor, IntPtr hdcMonitor, ref NativeRect monitorRect, IntPtr data) =>
            {
                var monitorInfo = new MonitorInfo
                {
                    Size = Marshal.SizeOf<MonitorInfo>()
                };

                if (monitor != IntPtr.Zero && GetMonitorInfo(monitor, ref monitorInfo))
                {
                    monitors.Add(new MonitorRecord(
                        ToRect(monitorInfo.Monitor),
                        (monitorInfo.Flags & MonitorInfoPrimary) == MonitorInfoPrimary));
                }

                return true;
            };

            if (!EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, IntPtr.Zero) || monitors.Count == 0)
            {
                monitors.Clear();
                monitors.Add(new MonitorRecord(
                    new Rect(0, 0, SystemParameters.PrimaryScreenWidth, SystemParameters.PrimaryScreenHeight),
                    true));
            }

            return monitors
                .OrderByDescending(monitor => monitor.IsPrimary)
                .ThenBy(monitor => monitor.Bounds.Left)
                .ThenBy(monitor => monitor.Bounds.Top)
                .ToList();
        }

        private static Rect ToRect(NativeRect nativeRect)
        {
            int width = nativeRect.Right - nativeRect.Left;
            int height = nativeRect.Bottom - nativeRect.Top;
            return new Rect(nativeRect.Left, nativeRect.Top, width, height);
        }

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, int dwFlags);

        [DllImport("user32.dll")]
        private static extern bool EnumDisplayMonitors(
            IntPtr hdc,
            IntPtr lprcClip,
            MonitorEnumProc lpfnEnum,
            IntPtr dwData);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfo lpmi);

        private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref NativeRect lprcMonitor, IntPtr dwData);

        private sealed record MonitorRecord(Rect Bounds, bool IsPrimary);

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
