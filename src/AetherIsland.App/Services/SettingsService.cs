using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using AetherIsland.Core.Models;
using AetherIsland.Core.Storage;

namespace AetherIsland.App.Services;

public sealed class SettingsService : ISettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public string StoragePath { get; }
    private readonly object _lock = new();

    public SettingsService(string? dir = null)
    {
        var baseDir = dir ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AetherIsland");
        Directory.CreateDirectory(baseDir);
        StoragePath = Path.Combine(baseDir, "settings.json");
    }

    public AppSettings Load()
    {
        lock (_lock)
        {
            try
            {
                if (File.Exists(StoragePath))
                {
                    var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(StoragePath), Options);
                    if (settings is not null) return settings;
                }
            }
            catch { }
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        lock (_lock)
        {
            var temporary = StoragePath + ".tmp";
            try
            {
                File.WriteAllText(temporary, JsonSerializer.Serialize(settings, Options));
                File.Move(temporary, StoragePath, overwrite: true);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }
    }
}
