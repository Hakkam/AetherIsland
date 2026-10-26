using AetherIsland.Core.Models;

namespace AetherIsland.Core.Storage;

public interface ISettingsStore
{
    AppSettings Load();
    void Save(AppSettings settings);
    string StoragePath { get; }
}

public interface ICredentialStore
{
    /// <summary>Stores a secret encrypted for the current Windows user (DPAPI).</summary>
    void SetSecret(string providerId, string secret);
    string? GetSecret(string providerId);
    void RemoveSecret(string providerId);
}
