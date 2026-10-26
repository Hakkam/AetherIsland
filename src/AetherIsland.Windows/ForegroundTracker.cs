using System.Diagnostics;
using AetherIsland.Windows.Interop;

namespace AetherIsland.Windows;

public sealed record ForegroundApp(string Exe, string DisplayName, string Category);

/// <summary>
/// Tracks the foreground application using GetForegroundWindow. Never captures
/// window titles, keystrokes, or window contents — only the owning process name.
/// </summary>
public static class ForegroundTracker
{
    public static uint GetIdleSeconds()
    {
        var info = new NativeMethods.LASTINPUTINFO { cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.LASTINPUTINFO>() };
        if (!NativeMethods.GetLastInputInfo(ref info)) return 0;
        var now = Environment.TickCount;
        var last = unchecked((int)info.dwTime);
        var idleMs = now >= last ? now - last : int.MaxValue;
        return (uint)(idleMs / 1000);
    }

    public static ForegroundApp? GetForegroundApp()
    {
        try
        {
            var hwnd = NativeMethods.GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return null;
            NativeMethods.GetWindowThreadProcessId(hwnd, out var pid);
            if (pid == 0) return null;
            using var proc = Process.GetProcessById((int)pid);
            var exe = proc.ProcessName;
            return new ForegroundApp(exe, FriendlyName(exe), Classify(exe));
        }
        catch
        {
            // elevated or exiting process — ignore
            return null;
        }
    }

    public static string Classify(string exe)
    {
        var name = exe.ToLowerInvariant();
        if (Browsing.Contains(name)) return "browsing";
        if (Communication.Contains(name)) return "communication";
        if (Development.Contains(name)) return "development";
        if (Media.Contains(name)) return "media";
        if (Work.Contains(name)) return "work";
        if (Creative.Contains(name)) return "creative";
        return "other";
    }

    public static string FriendlyName(string exe)
    {
        var name = exe.ToLowerInvariant();
        return KnownNames.TryGetValue(name, out var friendly) ? friendly : Capitalize(exe);
    }

    private static string Capitalize(string s) =>
        string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s[1..];

    private static readonly HashSet<string> Browsing = new(StringComparer.OrdinalIgnoreCase)
        { "chrome", "msedge", "firefox", "brave", "opera", "opera_gx", "vivaldi", "arc", "librewolf" };

    private static readonly HashSet<string> Communication = new(StringComparer.OrdinalIgnoreCase)
        { "slack", "teams", "ms-teams", "discord", "telegram", "whatsapp", "zoom", "signal", "thunderbird", "olk", "outlook" };

    private static readonly HashSet<string> Development = new(StringComparer.OrdinalIgnoreCase)
        { "code", "devenv", "rider", "pycharm64", "idea64", "webstorm64", "goland64", "clion64", "studio64", "x64dbg",
          "windowsterminal", "cmd", "powershell", "pwsh", "conhost", "wt", "notepad", "notepad++", "gitkraken",
          "sublime_text", "zcode", "claude", "codex", "opencode", "python", "dotnet", "node" };

    private static readonly HashSet<string> Media = new(StringComparer.OrdinalIgnoreCase)
        { "spotify", "vlc", "mpc-hc64", "mpv", "foobar2000", "itunes", "musictv" };

    private static readonly HashSet<string> Work = new(StringComparer.OrdinalIgnoreCase)
        { "winword", "excel", "powerpnt", "onenote", "outlook", "acropdf", "acrobat" };

    private static readonly HashSet<string> Creative = new(StringComparer.OrdinalIgnoreCase)
        { "photoshop", "illustrator", "figma", "gimp", "krita", "blender", "premiere" };

    private static readonly Dictionary<string, string> KnownNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["msedge"] = "Microsoft Edge", ["chrome"] = "Google Chrome", ["firefox"] = "Firefox",
        ["brave"] = "Brave", ["code"] = "VS Code", ["devenv"] = "Visual Studio",
        ["explorer"] = "File Explorer", ["winword"] = "Word", ["excel"] = "Excel",
        ["powerpnt"] = "PowerPoint", ["windowsterminal"] = "Terminal", ["powershell"] = "PowerShell",
        ["pwsh"] = "PowerShell", ["slack"] = "Slack", ["teams"] = "Teams", ["discord"] = "Discord",
        ["telegram"] = "Telegram", ["spotify"] = "Spotify", ["vlc"] = "VLC",
        ["zcode"] = "ZCode", ["claude"] = "Claude Code", ["codex"] = "Codex CLI",
        ["opencode"] = "OpenCode", ["python"] = "Python", ["node"] = "Node.js",
        ["acropdf"] = "Acrobat Reader", ["steam"] = "Steam", ["obs64"] = "OBS Studio"
    };
}
