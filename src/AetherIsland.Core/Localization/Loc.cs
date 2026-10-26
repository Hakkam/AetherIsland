using System.Reflection;
using System.Text.Json;

namespace AetherIsland.Core.Localization;

/// <summary>
/// Lightweight localization over embedded JSON dictionaries (en/ar/tr).
/// Falls back to English, then to the raw key. Arabic sets RTL.
/// </summary>
public static class Loc
{
    private static Dictionary<string, string> _strings = new();
    private static Dictionary<string, string> _fallback = new();

    public static string Language { get; private set; } = "en";
    public static bool IsRightToLeft => Language is "ar" or "he" or "fa" or "ur";

    public static event Action? LanguageChanged;

    public static void SetLanguage(string lang)
    {
        Language = lang switch { "ar" => "ar", "tr" => "tr", _ => "en" };
        _strings = Load(Language);
        if (Language != "en") _fallback = Load("en");
        LanguageChanged?.Invoke();
    }

    public static string T(string key)
    {
        if (_strings.TryGetValue(key, out var v) && !string.IsNullOrEmpty(v)) return v;
        if (_fallback.TryGetValue(key, out var f)) return f;
        return key;
    }

    public static string TF(string key, params object[] args)
    {
        var fmt = T(key);
        try { return string.Format(fmt, args); }
        catch { return fmt; }
    }

    private static Dictionary<string, string> Load(string lang)
    {
        try
        {
            var asm = Assembly.GetExecutingAssembly();
            var name = $"AetherIsland.Core.Localization.strings.{lang}.json";
            using var stream = asm.GetManifestResourceStream(name);
            if (stream is null) return new Dictionary<string, string>();
            var doc = JsonDocument.Parse(stream);
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            Flatten(doc.RootElement, null, result);
            return result;
        }
        catch
        {
            return new Dictionary<string, string>();
        }
    }

    private static void Flatten(JsonElement element, string? prefix, Dictionary<string, string> target)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var p in element.EnumerateObject())
                    Flatten(p.Value, prefix is null ? p.Name : $"{prefix}.{p.Name}", target);
                break;
            case JsonValueKind.String:
                if (prefix is not null) target[prefix] = element.GetString() ?? "";
                break;
        }
    }
}
