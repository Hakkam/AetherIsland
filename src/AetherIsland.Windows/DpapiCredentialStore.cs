using System.Security.Cryptography;
using System.Text;
using AetherIsland.Core.Storage;

namespace AetherIsland.Windows;

/// <summary>
/// Secret storage using Windows DPAPI (CryptProtectData, per-user scope).
/// Secrets never appear in logs, settings files, or exports.
/// </summary>
public sealed class DpapiCredentialStore : ICredentialStore
{
    private readonly string _filePath;
    private readonly object _lock = new();

    public DpapiCredentialStore(string? appDataDir = null)
    {
        var dir = appDataDir ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AetherIsland");
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "credentials.bin");
    }

    public void SetSecret(string providerId, string secret)
    {
        lock (_lock)
        {
            var all = ReadAll();
            all[providerId] = Protect(secret);
            WriteAll(all);
        }
    }

    public string? GetSecret(string providerId)
    {
        lock (_lock)
        {
            if (!ReadAll().TryGetValue(providerId, out var blob)) return null;
            try { return Unprotect(blob); }
            catch { return null; }
        }
    }

    public void RemoveSecret(string providerId)
    {
        lock (_lock)
        {
            var all = ReadAll();
            if (all.Remove(providerId)) WriteAll(all);
        }
    }

    private Dictionary<string, string> ReadAll()
    {
        try
        {
            if (!File.Exists(_filePath)) return new Dictionary<string, string>();
            var json = File.ReadAllText(_filePath);
            return System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                   ?? new Dictionary<string, string>();
        }
        catch { return new Dictionary<string, string>(); }
    }

    private void WriteAll(Dictionary<string, string> all)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(all);
        File.WriteAllText(_filePath, json);
        try { File.SetAttributes(_filePath, FileAttributes.Hidden); } catch { }
    }

    private static string Protect(string secret)
    {
        var bytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(secret), null, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(bytes);
    }

    private static string Unprotect(string blob) =>
        Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(blob), null, DataProtectionScope.CurrentUser));
}
