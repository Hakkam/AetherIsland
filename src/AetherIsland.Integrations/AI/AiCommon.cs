using System.Net.Http.Json;
using System.Text.Json;

namespace AetherIsland.Integrations.AI;

public static class AiProviderIds
{
    public const string OpenAI = "openai";
    public const string Anthropic = "anthropic";
    public const string Zai = "zai";
    public const string OpenCode = "opencode";
    public const string OpenAiCodex = "codex-cli";
    public const string ClaudeCode = "claude-code";
}

/// <summary>Settings bag handed to adapters. Secrets are resolved through a delegate, never stored here.</summary>
public sealed class ProviderConfig
{
    public string ProviderId { get; init; } = "";
    public string? AccountLabel { get; init; }
    public string? BaseUrl { get; init; }
    public Func<string?>? GetSecret { get; init; }
    public double? ManualMonthlyBudgetUsd { get; init; }
}

internal static class HttpJson
{
    internal static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };

    internal static JsonElement ParseObject(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    internal static double? GetDouble(JsonElement e, string prop) =>
        e.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : null;

    internal static long? GetLong(JsonElement e, string prop) =>
        e.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt64() : null;

    internal static string? GetString(JsonElement e, string prop) =>
        e.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    internal static async Task<(bool ok, string body, int status)> GetStringAsync(string url, IDictionary<string, string> headers, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        foreach (var (k, v) in headers) req.Headers.TryAddWithoutValidation(k, v);
        try
        {
            using var resp = await Http.SendAsync(req, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            return (resp.IsSuccessStatusCode, body, (int)resp.StatusCode);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return (false, ex is TaskCanceledException ? "Request timed out" : "Network error", 0);
        }
    }
}
