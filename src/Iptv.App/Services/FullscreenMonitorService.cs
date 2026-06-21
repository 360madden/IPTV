using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Iptv.App.ViewModels;

namespace Iptv.App.Services;

public static class FullscreenMonitorService
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
