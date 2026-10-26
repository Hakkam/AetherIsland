using System.Runtime.InteropServices;

namespace AetherIsland.App.Services;

public static class MonitorService
{
    public readonly record struct WorkArea(int Left, int Top, int Right, int Bottom);

    public static IReadOnlyList<string> DeviceNames()
    {
        var result = new List<string>();
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (monitor, _, _, _) =>
        {
            var info = CreateInfo();
            if (GetMonitorInfo(monitor, ref info)) result.Add(info.szDevice);
            return true;
        }, IntPtr.Zero);
        return result;
    }

    public static WorkArea SelectedWorkArea(string? device)
    {
        WorkArea? selected = null;
        WorkArea? primary = null;
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (monitor, _, _, _) =>
        {
            var info = CreateInfo();
            if (!GetMonitorInfo(monitor, ref info)) return true;
            var area = new WorkArea(info.rcWork.Left, info.rcWork.Top, info.rcWork.Right, info.rcWork.Bottom);
            if ((info.dwFlags & 1) != 0) primary = area;
            if (!string.IsNullOrWhiteSpace(device) &&
                string.Equals(info.szDevice, device, StringComparison.OrdinalIgnoreCase))
                selected = area;
            return true;
        }, IntPtr.Zero);
        return selected ?? primary ?? new WorkArea(0, 0, 1920, 1080);
    }

    public static string? DeviceNameForWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return null;
        var monitor = MonitorFromWindow(hwnd, 2); // MONITOR_DEFAULTTONEAREST
        var info = CreateInfo();
        return monitor != IntPtr.Zero && GetMonitorInfo(monitor, ref info) ? info.szDevice : null;
    }

    public static bool IsSelectedMonitor(IntPtr monitor, string? device)
    {
        var info = CreateInfo();
        if (!GetMonitorInfo(monitor, ref info)) return false;
        return string.IsNullOrWhiteSpace(device)
            ? (info.dwFlags & 1) != 0
            : string.Equals(info.szDevice, device, StringComparison.OrdinalIgnoreCase);
    }

    private static MonitorInfoEx CreateInfo() => new()
    {
        cbSize = Marshal.SizeOf<MonitorInfoEx>(),
        szDevice = new string('\0', 32)
    };

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MonitorInfoEx
    {
        public int cbSize;
        public Rect rcMonitor;
        public Rect rcWork;
        public int dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szDevice;
    }

    private delegate bool MonitorEnumProc(IntPtr monitor, IntPtr hdc, IntPtr rect, IntPtr data);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip,
        MonitorEnumProc callback, IntPtr data);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetMonitorInfoW")]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfoEx info);
}

