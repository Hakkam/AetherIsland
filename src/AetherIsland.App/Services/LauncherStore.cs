using System.IO;
using System.Text.Json;

namespace AetherIsland.App.Services;

public sealed class AppShortcut
{
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
}

/// <summary>Persists the application launcher favorites (dashboard widget).</summary>
public sealed class LauncherStore
{
    private readonly string _path;
    private readonly object _lock = new();

    public LauncherStore(string? dir = null)
    {
        var baseDir = dir ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AetherIsland");
        Directory.CreateDirectory(baseDir);
        _path = Path.Combine(baseDir, "launcher.json");
    }

    public List<AppShortcut> Load()
    {
        lock (_lock)
        {
            try
            {
                if (File.Exists(_path))
                    return JsonSerializer.Deserialize<List<AppShortcut>>(File.ReadAllText(_path)) ?? new List<AppShortcut>();
            }
            catch { }
            return DefaultApps();
        }
    }

    public void Save(List<AppShortcut> apps)
    {
        lock (_lock)
        {
            try { File.WriteAllText(_path, JsonSerializer.Serialize(apps, new JsonSerializerOptions { WriteIndented = true })); }
            catch { }
        }
    }

    private static List<AppShortcut> DefaultApps()
    {
        var list = new List<AppShortcut>();
        var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var windir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

        void TryAdd(string name, params string[] paths)
        {
            foreach (var p in paths)
                if (File.Exists(p)) { list.Add(new AppShortcut { Name = name, Path = p }); return; }
        }

        TryAdd("Edge", Path.Combine(pf, @"Microsoft\Edge\Application\msedge.exe"));
        TryAdd("Explorer", Path.Combine(windir, "explorer.exe"));
        TryAdd("Notepad", Path.Combine(windir, "notepad.exe"));
        TryAdd("Terminal", Path.Combine(pf, @"WindowsApps\Microsoft.WindowsTerminal_8wekyb3d8bbwe\wt.exe"));
        TryAdd("VS Code", Path.Combine(pf86, @"Microsoft VS Code\Code.exe"), Path.Combine(pf, @"Microsoft VS Code\Code.exe"));
        TryAdd("Spotify", Path.Combine(pf, @"Spotify\Spotify.exe"), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Spotify\Spotify.exe"));
        TryAdd("Calculator", @"Calculator://");
        return list;
    }
}
