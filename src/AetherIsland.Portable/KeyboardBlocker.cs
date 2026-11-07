using System.Runtime.InteropServices;

namespace AetherIsland.Portable;

/// <summary>
/// Blocks all laptop keyboard input while active via a low-level keyboard
/// hook — the same approach as the well-known keyboard-locker utilities.
/// No admin rights, instant on/off, and fail-safe: if the island exits, the
/// hook is released and the keyboard returns automatically. Ctrl+Alt
/// combinations stay alive (Ctrl+Alt+Del is kernel-level anyway), and the
/// mouse is never touched.
/// </summary>
public static class KeyboardBlocker
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int VK_CONTROL = 0x11;
    private const int VK_MENU = 0x12;

    private delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, HookProc proc, IntPtr module, int threadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hook);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? name);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int key);

    private static IntPtr _hook;
    private static HookProc? _proc;

    public static bool IsOn => _hook != IntPtr.Zero;

    public static void Set(bool on)
    {
        if (on && _hook == IntPtr.Zero)
        {
            _proc = Hook;
            _hook = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(null), 0);
            if (_hook == IntPtr.Zero)
            {
                _proc = null;
                PortableData.Log("KEYBOARD", "hook install failed");
            }
            else PortableData.Log("KEYBOARD", "blocked");
        }
        else if (!on && _hook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
            _proc = null;
            PortableData.Log("KEYBOARD", "restored");
        }
    }

    private static IntPtr Hook(int code, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            if (code >= 0 && (wParam.ToInt64() is WM_KEYDOWN or WM_SYSKEYDOWN))
            {
                var ctrl = (GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0;
                var alt = (GetAsyncKeyState(VK_MENU) & 0x8000) != 0;
                if (!(ctrl && alt)) return 1; // swallow the key
            }
        }
        catch { /* the hook must never break keyboard input */ }
        return CallNextHookEx(_hook, code, wParam, lParam);
    }
}
