using System.Runtime.InteropServices;

namespace AetherIsland.Windows.Interop;

internal static class NativeMethods
{
    // user32
    [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
    [DllImport("user32.dll")] internal static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);
    [DllImport("user32.dll")] internal static extern bool LockWorkStation();
    [DllImport("user32.dll")] internal static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
    [DllImport("user32.dll")] internal static extern uint SendMessageTimeout(IntPtr hWnd, uint msg, UIntPtr wParam, IntPtr lParam, uint fuFlags, uint uTimeout, out UIntPtr result);

    [StructLayout(LayoutKind.Sequential)]
    internal struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }

    // kernel32
    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool GetSystemTimes(out FILETIME idle, out FILETIME kernel, out FILETIME user);

    [StructLayout(LayoutKind.Sequential)]
    internal struct FILETIME { public uint Low; public uint High; }

    [DllImport("kernel32.dll")]
    internal static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX buffer);

    [StructLayout(LayoutKind.Sequential)]
    internal struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    // powrprof
    [DllImport("powrprof.dll")]
    internal static extern bool GetSystemPowerStatus(ref SYSTEM_POWER_STATUS status);

    [StructLayout(LayoutKind.Sequential)]
    internal struct SYSTEM_POWER_STATUS
    {
        public byte ACLineStatus;        // 0 offline, 1 online, 255 unknown
        public byte BatteryFlag;         // 1..8 high..low, 128 no battery, 255 unknown
        public byte BatteryLifePercent;  // 0..100, 255 unknown
        public byte Reserved;
        public uint BatteryLifeTime;     // seconds, -1 unknown
        public uint BatteryFullLifeTime;
    }

    // kernel32 — Keep awake
    [Flags]
    internal enum EXECUTION_STATE : uint
    {
        ES_CONTINUOUS = 0x80000000,
        ES_SYSTEM_REQUIRED = 0x00000001,
        ES_DISPLAY_REQUIRED = 0x00000002
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern EXECUTION_STATE SetThreadExecutionState(EXECUTION_STATE esFlags);

    // shell32 — file icons
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    internal static extern IntPtr SHGetFileInfo(string pszPath, uint fileAttributes, ref SHFILEINFO info, uint cbSizeFile, uint uFlags);

    [DllImport("user32.dll")] internal static extern bool DestroyIcon(IntPtr hIcon);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string szTypeName;
    }

    internal const uint SHGFI_ICON = 0x100;
    internal const uint SHGFI_SMALLICON = 0x1;
    internal const uint SHGFI_LARGEICON = 0x0;
    internal const uint SHGFI_USEFILEATTRIBUTES = 0x10;

    // Virtual key codes for media transport
    internal const byte VK_MEDIA_PLAY_PAUSE = 0xB3;
    internal const byte VK_MEDIA_NEXT_TRACK = 0xB0;
    internal const byte VK_MEDIA_PREV_TRACK = 0xB1;
    internal const byte VK_VOLUME_MUTE = 0xAD;
    internal const byte VK_VOLUME_UP = 0xAF;
    internal const byte VK_VOLUME_DOWN = 0xAE;

    internal const uint KEYEVENTF_KEYUP = 0x0002;

    internal const uint SMTO_ABORTIFHUNG = 0x0002;
}
