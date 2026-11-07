using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AetherIsland.Windows;

namespace AetherIsland.Portable;

/// <summary>
/// All user-owned application data lives next to the portable executable.
/// No registry writes for data, no silent LocalAppData fallback. The only
/// registry write the app performs is the optional opt-in Windows startup
/// entry managed by <see cref="StartupManager"/>. Windows DPAPI protects API
/// credentials; those cannot be moved to another Windows user.
/// </summary>
public static class PortableData
{
    private static readonly object Gate = new();
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    public static string Root { get; } = Path.Combine(AppContext.BaseDirectory, "Data");
    public static string InstanceKey { get; } = Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes(Path.GetFullPath(Root).ToUpperInvariant())))[..16];
    public static PortableSettings Settings { get; private set; } = new();
    /// <summary>Replace the whole settings object (used by Reset settings).</summary>
    public static void ReplaceSettings(PortableSettings replacement) { Settings = replacement; SaveSettings(); }
    public static DpapiCredentialStore Credentials { get; private set; } = null!;

    public static void Initialize()
    {
        Directory.CreateDirectory(Root); // Refuse if read-only; do not silently use another path.
        using (var stream = new FileStream(Path.Combine(Root, ".writable"), FileMode.OpenOrCreate,
            FileAccess.ReadWrite, FileShare.ReadWrite)) { }
        File.Delete(Path.Combine(Root, ".writable"));
        Settings = Read<PortableSettings>("settings.json") ?? new PortableSettings();
        Credentials = new DpapiCredentialStore(Root);
    }

    public static T? Read<T>(string relative)
    {
        lock (Gate)
        {
            try
            {
                var path = Path.Combine(Root, relative);
                return File.Exists(path) ? JsonSerializer.Deserialize<T>(File.ReadAllText(path)) : default;
            }
            catch (Exception ex) { Log("DATA READ", ex.Message); return default; }
        }
    }

    public static void Save<T>(string relative, T data)
    {
        lock (Gate)
        {
            try
            {
                var path = Path.Combine(Root, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                var tmp = path + ".tmp";
                File.WriteAllText(tmp, JsonSerializer.Serialize(data, JsonOptions), Encoding.UTF8);
                File.Move(tmp, path, true);
            }
            catch (Exception ex) { Log("DATA WRITE", ex.Message); }
        }
    }

    public static void SaveSettings() => Save("settings.json", Settings);

    public static void Log(string area, string message)
    {
        try
        {
            lock (Gate)
            {
                var dir = Path.Combine(Root, "logs");
                Directory.CreateDirectory(dir);
                var path = Path.Combine(dir, "app.log");
                if (File.Exists(path) && new FileInfo(path).Length > 256 * 1024)
                    File.Move(path, Path.Combine(dir, "app.previous.log"), true);
                File.AppendAllText(path, $"{DateTimeOffset.Now:O} [{area}] {message}\n");
            }
        }
        catch { /* logging must not cascade into failures */ }
    }
}

public sealed class PortableSettings
{
    // Window state
    public bool Pinned { get; set; }
    public double? Left { get; set; }
    public double? Top { get; set; }
    public bool ReducedMotion { get; set; }

    // Appearance
    public string Theme { get; set; } = "dark";          // dark | light | system
    public string Accent { get; set; } = "#33A6F0";
    public string Language { get; set; } = "en";         // en | ar | tr

    // System integration
    public bool LaunchAtStartup { get; set; } = true;
    public bool HotkeyEnabled { get; set; } = true;      // Ctrl+Alt+Space toggles the island
    public bool CollapseOnOutsideClick { get; set; } = true; // any click outside collapses to the pill

    // Screen time
    public bool ScreenTimeConsent { get; set; }

    // Weather
    public string WeatherLocation { get; set; } = "";
    public double? WeatherLatitude { get; set; }
    public double? WeatherLongitude { get; set; }
    public string WeatherUnit { get; set; } = "c";       // c | f
    public bool WeatherAutoLocated { get; set; }

    // Day schedule
    public int WakeHour { get; set; } = 7;
    public int BedHour { get; set; } = 23;

    // Calendar (ICS; OAuth providers are not supported and never pretended)
    public List<string> CalendarSources { get; set; } = new();

    // Wellbeing
    public bool WellbeingEnabled { get; set; }
    public int EyeBreakMinutes { get; set; } = 20;
    public int StandMinutes { get; set; } = 50;
    public int HydrationCups { get; set; } = 0;          // today's logged cups
    public DateTime HydrationDay { get; set; } = DateTime.MinValue;
}

public sealed class PortableTask
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = "";
    public bool Done { get; set; }
    public bool Trashed { get; set; }
    public DateTime? DueAt { get; set; }                 // parsed from "15m", "2h", "14:30" suffixes
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? DoneAt { get; set; }
}

public sealed class PortableNote
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Content { get; set; } = "";
    public bool Pinned { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
