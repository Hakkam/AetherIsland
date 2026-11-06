using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AetherIsland.Windows;

namespace AetherIsland.Portable;

/// <summary>
/// Real shell icon extraction for the apps grid, file shelf and screen-time
/// rows. Falls back to a deterministic colored tile when an icon cannot be
/// resolved (elevated processes, stripped-down systems).
/// </summary>
internal static class AppIcons
{
    private static readonly Dictionary<string, ImageSource?> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, string> ExePathCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Icon for a concrete file/folder path.</summary>
    public static ImageSource? ForPath(string path)
    {
        if (Cache.TryGetValue(path, out var cached)) return cached;
        var source = Extract(path);
        Cache[path] = source;
        return source;
    }

    /// <summary>Best-effort icon for an exe name (screen-time rows).</summary>
    public static ImageSource? ForExe(string exeName)
    {
        var path = ResolveExePath(exeName);
        return path is null ? null : ForPath(path);
    }

    public static string? ResolveExePath(string exeName)
    {
        if (ExePathCache.TryGetValue(exeName, out var known)) return known;
        string? found = null;
        try
        {
            // A running instance is the most reliable source.
            var proc = System.Diagnostics.Process.GetProcessesByName(exeName)
                .FirstOrDefault(p => !string.IsNullOrEmpty(p.MainModule?.FileName));
            found = proc?.MainModule?.FileName;
            proc?.Dispose();
            if (found is null)
            {
                foreach (var dir in new[]
                         {
                             Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                             Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32"),
                             Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WindowsApps")
                         })
                {
                    var candidate = Path.Combine(dir, exeName + ".exe");
                    if (File.Exists(candidate)) { found = candidate; break; }
                }
            }
        }
        catch { found = null; }
        ExePathCache[exeName] = found ?? "";
        return found;
    }

    private static ImageSource? Extract(string path)
    {
        var handle = IntPtr.Zero;
        try
        {
            handle = ShellIcons.GetFileIconHandle(path, large: false);
            if (handle == IntPtr.Zero) return null;
            var source = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                handle, System.Windows.Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();
            return source;
        }
        catch { return null; }
        finally { if (handle != IntPtr.Zero) ShellIcons.DestroyIconHandle(handle); }
    }

    /// <summary>Icon tile: real icon when available, otherwise a colored initial.</summary>
    public static FrameworkElement Tile(string pathOrExe, double size, bool preferExeLookup = false)
    {
        var source = preferExeLookup
            ? ForExe(pathOrExe) ?? ForPath(pathOrExe)
            : ForPath(pathOrExe) ?? ForExe(pathOrExe);
        if (source != null)
            return new System.Windows.Controls.Image
            {
                Source = source, Width = size, Height = size,
                VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center,
                SnapsToDevicePixels = true
            };
        var name = Path.GetFileName(pathOrExe);
        var label = name.Length > 0 ? char.ToUpperInvariant(name[0]).ToString() : "?";
        return new System.Windows.Controls.Border
        {
            Width = size, Height = size,
            CornerRadius = new CornerRadius(size * 0.28),
            Background = Theme.TileFor(pathOrExe),
            Child = new System.Windows.Controls.TextBlock
            {
                Text = label, FontSize = size * 0.5, FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
            }
        };
    }
}
