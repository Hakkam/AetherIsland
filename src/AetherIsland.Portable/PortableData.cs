using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AetherIsland.Windows;

namespace AetherIsland.Portable;

/// <summary>
/// All user-owned application data lives next to the portable executable.
/// No registry writes, no Windows installer and no silent LocalAppData fallback.
/// Windows DPAPI protects API credentials, which cannot be moved to another Windows user.
/// </summary>
public static class PortableData
{
    private static readonly object Gate = new();
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    public static string Root { get; } = Path.Combine(AppContext.BaseDirectory, "Data");
    public static string InstanceKey { get; } = Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes(Path.GetFullPath(Root).ToUpperInvariant())))[..16];
    public static PortableSettings Settings { get; private set; } = new();
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
            var path = Path.Combine(Root, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(data, JsonOptions), Encoding.UTF8);
            File.Move(tmp, path, true);
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
    public bool Pinned { get; set; }
    public double? Left { get; set; }
    public double? Top { get; set; }
    public bool ReducedMotion { get; set; }
    public bool ScreenTimeConsent { get; set; }
    public string Language { get; set; } = "en";
    public string WeatherLocation { get; set; } = "";
    public double? WeatherLatitude { get; set; }
    public double? WeatherLongitude { get; set; }
    public int WakeHour { get; set; } = 7;
    public int BedHour { get; set; } = 23;
}
public sealed class PortableTask
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = "";
    public bool Done { get; set; }
}
public sealed class PortableNote
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Content { get; set; } = "";
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
