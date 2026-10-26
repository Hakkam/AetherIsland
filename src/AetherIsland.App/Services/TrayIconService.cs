using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

namespace AetherIsland.App.Services;

/// <summary>
/// System tray icon implemented directly on Shell_NotifyIcon — left click toggles the
/// island, right click opens a context menu, balloons surface important events.
/// </summary>
public sealed class TrayIconService : IDisposable
{
    private const int WM_TRAY = 0x8000;              // WM_APP
    private const int WM_TASKBARCREATED = 0x001C;    // TaskbarCreated (registered)
    private const int NIM_ADD = 0x0, NIM_MODIFY = 0x1, NIM_DELETE = 0x2;
    private const int NIF_MESSAGE = 0x1, NIF_ICON = 0x2, NIF_TIP = 0x4, NIF_INFO = 0x10;

    private IntPtr _hwnd;
    private HwndSource? _source;
    private System.Drawing.Icon? _icon;
    private uint _taskbarCreatedMsg;
    private bool _added;
    private ContextMenu? _menu;

    public event Action? LeftClick;
    public event Action? DoubleClick;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATAW
    {
        public int cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szTip;
        public uint dwState;
        public uint dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string szInfo;
        public uint uVersion; // union with uTimeout
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string szInfoTitle;
        public uint dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool Shell_NotifyIconW(uint message, ref NOTIFYICONDATAW data);

    [DllImport("user32.dll")]
    private static extern uint RegisterWindowMessage(string name);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT point);

    public void Initialize(ContextMenu menu)
    {
        _menu = menu;
        _taskbarCreatedMsg = RegisterWindowMessage("TaskbarCreated");

        _source = new HwndSource(new HwndSourceParameters("AetherIslandTray")
        {
            Width = 0,
            Height = 0,
            WindowStyle = 0,
            PositionX = -1000,
            PositionY = -1000
        });
        _hwnd = _source.Handle;
        _source.AddHook(WndProc);

        _icon = Infrastructure.IconFactory.CreateAppIcon(32);
        Add();
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_TRAY)
        {
            var mouse = lParam.ToInt32() & 0xFFFF;
            switch (mouse)
            {
                case 0x0201: // WM_LBUTTONDOWN
                    break;
                case 0x0202: // WM_LBUTTONUP
                    LeftClick?.Invoke();
                    handled = true;
                    break;
                case 0x0203: // WM_LBUTTONDBLCLK
                    DoubleClick?.Invoke();
                    handled = true;
                    break;
                case 0x0205: // WM_RBUTTONUP
                    ShowMenu();
                    handled = true;
                    break;
            }
        }
        else if (msg == WM_TASKBARCREATED && _taskbarCreatedMsg == (uint)msg)
        {
            // Explorer restarted — re-add the icon
            Add();
            handled = true;
        }
        return IntPtr.Zero;
    }

    private void ShowMenu()
    {
        if (_menu is null) return;
        GetCursorPos(out var p);
        _menu.Placement = System.Windows.Controls.Primitives.PlacementMode.AbsolutePoint;
        _menu.HorizontalOffset = p.X;
        _menu.VerticalOffset = p.Y;
        _menu.IsOpen = true;
    }

    private void Add()
    {
        if (_hwnd == IntPtr.Zero || _icon is null) return;
        var data = new NOTIFYICONDATAW
        {
            cbSize = Marshal.SizeOf<NOTIFYICONDATAW>(),
            hWnd = _hwnd,
            uID = 1,
            uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
            uCallbackMessage = WM_TRAY,
            hIcon = _icon.Handle,
            szTip = "Aether Island"
        };
        _added = Shell_NotifyIconW(NIM_ADD, ref data);
    }

    private void Modify(Action<NOTIFYICONDATAW> change)
    {
        if (!_added) return;
        var data = new NOTIFYICONDATAW
        {
            cbSize = Marshal.SizeOf<NOTIFYICONDATAW>(),
            hWnd = _hwnd,
            uID = 1,
            uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
            uCallbackMessage = WM_TRAY,
            hIcon = _icon?.Handle ?? IntPtr.Zero,
            szTip = "Aether Island"
        };
        change(data);
        Shell_NotifyIconW(NIM_MODIFY, ref data);
    }

    public void ShowBalloon(string title, string message)
    {
        if (!_added) return;
        var data = new NOTIFYICONDATAW
        {
            cbSize = Marshal.SizeOf<NOTIFYICONDATAW>(),
            hWnd = _hwnd,
            uID = 1,
            uFlags = NIF_INFO,
            szInfoTitle = title.Length > 63 ? title[..63] : title,
            szInfo = message.Length > 255 ? message[..255] : message,
            dwInfoFlags = 0
        };
        Shell_NotifyIconW(NIM_MODIFY, ref data);
    }

    public void CloseBalloon()
    {
        if (!_added) return;
        var data = new NOTIFYICONDATAW
        {
            cbSize = Marshal.SizeOf<NOTIFYICONDATAW>(),
            hWnd = _hwnd,
            uID = 1,
            uFlags = NIF_INFO,
            szInfo = "",
            szInfoTitle = ""
        };
        Shell_NotifyIconW(NIM_MODIFY, ref data);
    }

    public void Dispose()
    {
        if (_added)
        {
            var data = new NOTIFYICONDATAW
            {
                cbSize = Marshal.SizeOf<NOTIFYICONDATAW>(),
                hWnd = _hwnd,
                uID = 1
            };
            Shell_NotifyIconW(NIM_DELETE, ref data);
            _added = false;
        }
        _icon?.Dispose();
        _source?.Dispose();
    }
}
