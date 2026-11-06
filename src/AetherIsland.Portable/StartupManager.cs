using Microsoft.Win32;

namespace AetherIsland.Portable;

/// <summary>
/// Opt-in "launch at Windows startup" support for the portable exe, using the
/// standard per-user HKCU Run key. The stored command is self-healing: every
/// launch refreshes it so moving the portable folder does not leave a stale
/// entry. Nothing is written unless the user (or first-run default) enables it,
/// and disabling removes the key entirely.
/// </summary>
internal static class StartupManager
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "AetherIsland";

    public static void SyncOnLaunch()
    {
        try
        {
            var exe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exe)) return;
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            if (key is null) return;
            var existing = key.GetValue(ValueName) as string;
            var quoted = $"\"{exe}\"";
            if (PortableData.Settings.LaunchAtStartup)
            {
                if (existing != quoted) key.SetValue(ValueName, quoted);
            }
            else if (existing != null)
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }
        }
        catch (Exception ex) { PortableData.Log("STARTUP", ex.Message); }
    }

    public static void Apply(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            if (key is null) return;
            if (enabled)
            {
                var exe = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(exe)) key.SetValue(ValueName, $"\"{exe}\"");
            }
            else key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
        catch (Exception ex) { PortableData.Log("STARTUP", ex.Message); }
    }
}
