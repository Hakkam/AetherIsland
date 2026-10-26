using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace AetherIsland.App.Services;

/// <summary>Registers process-global hotkeys through RegisterHotKey and raises parsed events.</summary>
public sealed class HotkeyService : IDisposable
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint vk);
    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private const uint MOD_ALT = 0x1, MOD_CONTROL = 0x2, MOD_SHIFT = 0x4, MOD_WIN = 0x8;
    private const int WM_HOTKEY = 0x0312;

    private IntPtr _hwnd;
    private HwndSource? _source;
    private readonly Dictionary<int, string> _registered = new();

    public event Action<string>? HotkeyPressed; // yields hotkey name

    public void Attach(Window window)
    {
        _hwnd = new WindowInteropHelper(window).EnsureHandle();
        _source = HwndSource.FromHwnd(_hwnd);
        _source?.AddHook(WndProc);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && _registered.TryGetValue(wParam.ToInt32(), out var name))
        {
            HotkeyPressed?.Invoke(name);
            handled = true;
        }
        return IntPtr.Zero;
    }

    /// <summary>Registers a hotkey string like "Ctrl+Alt+Space". Returns false on conflict.</summary>
    public bool TryRegister(string name, string combo)
    {
        if (_hwnd == IntPtr.Zero || string.IsNullOrWhiteSpace(combo)) return false;
        if (!TryParse(combo, out var mods, out var vk)) return false;
        var id = name.GetHashCode() & 0x7FFFFFF;
        if (!RegisterHotKey(_hwnd, id, mods, vk)) return false;
        _registered[id] = name;
        return true;
    }

    public void UnregisterAll()
    {
        foreach (var id in _registered.Keys) UnregisterHotKey(_hwnd, id);
        _registered.Clear();
    }

    public static bool TryParse(string combo, out uint mods, out uint vk)
    {
        mods = 0; vk = 0;
        var parts = combo.Split('+').Select(p => p.Trim()).ToList();
        if (parts.Count == 0) return false;
        var keyPart = parts[^1];
        foreach (var m in parts.Take(parts.Count - 1))
        {
            switch (m.ToLowerInvariant())
            {
                case "ctrl" or "control": mods |= MOD_CONTROL; break;
                case "alt": mods |= MOD_ALT; break;
                case "shift": mods |= MOD_SHIFT; break;
                case "win" or "windows": mods |= MOD_WIN; break;
                default: return false;
            }
        }
        if (mods == 0) return false; // require a modifier for global hotkeys

        if (keyPart.Length == 1)
        {
            var key = Key.None;
            if (char.IsLetterOrDigit(keyPart[0]))
                key = new KeyConverter().ConvertFromString(keyPart.ToUpperInvariant()) is Key k ? k : Key.None;
            if (key == Key.None) return false;
            vk = (uint)KeyInterop.VirtualKeyFromKey(key);
            return vk != 0;
        }
        if (Enum.TryParse<Key>(keyPart, ignoreCase: true, out var named))
        {
            vk = (uint)KeyInterop.VirtualKeyFromKey(named);
            return vk != 0;
        }
        return false;
    }

    public void Dispose() => UnregisterAll();
}
