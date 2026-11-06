using System.IO;
using System.Runtime.InteropServices;
using AetherIsland.Windows;
using Windows.Devices.Radios;

namespace AetherIsland.Portable;

/// <summary>Minimal P/Invoke used only by the portable shell itself.</summary>
internal static class Native
{
    [DllImport("user32.dll")] internal static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll")] internal static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    internal const uint MOD_CONTROL = 0x2, MOD_ALT = 0x1;
    internal const int WM_HOTKEY = 0x0312;
    internal static readonly IntPtr HwndBroadcast = new(0xFFFF);
    internal const uint WM_SETTINGCHANGE = 0x001A;

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint msg, UIntPtr wParam, IntPtr lParam,
        uint fuFlags, uint uTimeout, out UIntPtr result);
}

/// <summary>
/// Real Windows toggle actions behind the reference QUICK TOGGLES grid. Every
/// control reflects or changes actual OS state; where Windows has no supported
/// API the action is marked unavailable instead of simulating success.
/// </summary>
internal static class Toggles
{
    // ---- Wi-Fi / Bluetooth radio state (Windows.Devices.Radios) ----

    public static (bool available, bool on) GetRadioState(string kind)
    {
        try
        {
            var radios = Radio.GetRadiosAsync().GetAwaiter().GetResult();
            var target = kind == "wifi" ? RadioKind.WiFi : RadioKind.Bluetooth;
            var radio = radios.FirstOrDefault(r => r.Kind == target);
            return radio is null ? (false, false) : (true, radio.State == RadioState.On);
        }
        catch (Exception ex)
        {
            PortableData.Log("RADIOS", $"{kind}: {ex.Message}");
            return (false, false);
        }
    }

    public static async Task<(bool ok, string message)> SetRadioAsync(string kind, bool on)
    {
        try
        {
            var radios = Radio.GetRadiosAsync();
            var target = kind == "wifi" ? RadioKind.WiFi : RadioKind.Bluetooth;
            var radio = (await radios).FirstOrDefault(r => r.Kind == target);
            if (radio is null) return (false, $"{kind} radio not present on this device");
            await radio.SetStateAsync(on ? RadioState.On : RadioState.Off);
            return (true, on ? "On" : "Off");
        }
        catch (Exception ex)
        {
            PortableData.Log("RADIOS", $"{kind}: {ex.Message}");
            return (false, "Windows refused the radio change");
        }
    }

    public static bool WifiApiAvailable => Environment.OSVersion.Version >= new Version(10, 0, 16299, 0);

    // ---- Personalization (dark mode) ----

    public static bool IsDarkMode()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int light && light == 0;
        }
        catch { return true; }
    }

    public static bool SetDarkMode(bool dark)
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", writable: true);
            if (key is null) return false;
            key.SetValue("AppsUseLightTheme", dark ? 0 : 1, Microsoft.Win32.RegistryValueKind.DWord);
            key.SetValue("SystemUsesLightTheme", dark ? 0 : 1, Microsoft.Win32.RegistryValueKind.DWord);
            // Broadcast so running apps pick the change up without logoff.
            Native.SendMessageTimeout(Native.HwndBroadcast, Native.WM_SETTINGCHANGE, UIntPtr.Zero,
                IntPtr.Zero, 0x0002 /* SMTO_ABORTIFHUNG */, 300, out _);
            return true;
        }
        catch (Exception ex)
        {
            PortableData.Log("DARKMODE", ex.Message);
            return false;
        }
    }

    // ---- Audio mute states (real endpoint controls) ----

    public static (bool available, bool muted) SpeakerState()
    {
        var s = AudioEndpoints.GetSpeaker();
        return (s.Available, s.Muted);
    }

    public static (bool available, bool muted) MicrophoneState()
    {
        var s = AudioEndpoints.GetMicrophone();
        return (s.Available, s.Muted);
    }

    public static bool SetSpeakerMute(bool muted)
    {
        try { AudioEndpoints.SetSpeakerMute(muted); return AudioEndpoints.GetSpeaker() is { Available: true } s && s.Muted == muted; }
        catch (Exception ex) { PortableData.Log("AUDIO", ex.Message); return false; }
    }

    public static bool SetMicrophoneMute(bool muted)
    {
        try { AudioEndpoints.SetMicrophoneMute(muted); return AudioEndpoints.GetMicrophone() is { Available: true } s && s.Muted == muted; }
        catch (Exception ex) { PortableData.Log("AUDIO", ex.Message); return false; }
    }

    // ---- Keep awake (dedicated thread, ES_CONTINUOUS refresh) ----

    private static Thread? _awakeThread;
    private static volatile bool _awake;

    public static bool KeepAwakeOn => _awake;

    public static void SetKeepAwake(bool on)
    {
        if (on == _awake && _awakeThread is { IsAlive: true }) return;
        _awake = on;
        if (on)
        {
            _awakeThread = new Thread(() =>
            {
                // Must keep re-asserting on a live thread; ES_CONTINUOUS alone
                // is cleared when the thread exits.
                while (_awake)
                {
                    PowerHelper.KeepAwake(true);
                    Thread.Sleep(50_000);
                }
                PowerHelper.AllowSleep();
            })
            { IsBackground = true, Name = "AetherIsland-KeepAwake" };
            _awakeThread.Start();
        }
        // Off: loop exits and AllowSleep() restores default behavior.
    }

    // ---- Screenshot (virtual screen → PNG + clipboard) ----

    public static string? CaptureScreen()
    {
        try
        {
            var bounds = System.Windows.Forms.SystemInformation.VirtualScreen;
            using var bmp = new System.Drawing.Bitmap(bounds.Width, bounds.Height);
            using (var g = System.Drawing.Graphics.FromImage(bmp))
                g.CopyFromScreen(bounds.X, bounds.Y, 0, 0, bmp.Size);
            var dir = Path.Combine(PortableData.Root, "screenshots");
            Directory.CreateDirectory(dir);
            var file = Path.Combine(dir, $"shot-{DateTime.Now:yyyyMMdd-HHmmss}.png");
            bmp.Save(file, System.Drawing.Imaging.ImageFormat.Png);
            // Copy to clipboard as WPF bitmap source for instant paste.
            var handle = bmp.GetHbitmap();
            try
            {
                var source = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                    handle, IntPtr.Zero, System.Windows.Int32Rect.Empty,
                    System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
                source.Freeze();
                System.Windows.Clipboard.SetImage(source);
            }
            finally { DeleteObject(handle); }
            return file;
        }
        catch (Exception ex)
        {
            PortableData.Log("SCREENSHOT", ex.Message);
            return null;
        }
    }

    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr hObject);

    public static void LockScreen()
    {
        try { PowerHelper.LockWorkstation(); }
        catch (Exception ex) { PortableData.Log("LOCK", ex.Message); }
    }
}
