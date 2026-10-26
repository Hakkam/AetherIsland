using System.Text.Json;
using AetherIsland.Core.Models;

namespace AetherIsland.Integrations.AI;

/// <summary>
/// OpenCode adapter. Two observation channels, both local and permission-gated:
///  1. A user-configured local OpenCode server (e.g. http://localhost:4096 from `opencode serve`).
///  2. OpenCode's on-disk storage under ~/.local/share/opencode.
/// Local client statistics are labeled VerifiedLocal and never presented as provider
/// billing totals. API keys are never read or imported.
/// </summary>
public sealed class OpenCodeLocalProvider : IAiUsageProvider
{
    public string ProviderId => AiProviderIds.OpenCode;
    public string DisplayName { get; set; } = "OpenCode";

    private readonly ProviderConfig _cfg;
    private readonly bool _metadataConsent;

    public OpenCodeLocalProvider(ProviderConfig cfg, bool metadataConsent)
    {
        _cfg = cfg;
        _metadataConsent = metadataConsent;
    }

    private string ServerUrl => (_cfg.BaseUrl?.TrimEnd('/') is { Length: > 0 } b) ? b : "http://localhost:4096";

    private string StorageDir
    {
        get
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var candidates = new[]
            {
                Path.Combine(userProfile, ".local", "share", "opencode", "storage", "message"),
                Path.Combine(localAppData, "opencode", "storage", "message"),
                Path.Combine(userProfile, ".local", "share", "opencode", "project")
            };
            return candidates.FirstOrDefault(Directory.Exists) ?? candidates[0];
        }
    }

    public async Task<ConnectionStatus> CheckConnectionAsync(CancellationToken ct)
    {
        if (!_metadataConsent)
            return new ConnectionStatus { Connected = false, State = "disconnected", Message = "Local agent metadata permission is off (Settings → Privacy)", CheckedAt = DateTime.Now };

        if (Directory.Exists(StorageDir))
        {
            return new ConnectionStatus
            {
                Connected = true,
                State = "connected",
                Message = "Local OpenCode storage found; " + (await ProbeServerAsync(ct) is null ? "no local server running" : "local server reachable"),
                AccountLabel = "this machine",
                CheckedAt = DateTime.Now
            };
        }
        var server = await ProbeServerAsync(ct);
        return new ConnectionStatus
        {
            Connected = server is not null,
            State = server is not null ? "connected" : "notfound",
            Message = server is not null ? "Local OpenCode server reachable" : "OpenCode storage not found on this machine",
            CheckedAt = DateTime.Now
        };
    }

    private async Task<string?> ProbeServerAsync(CancellationToken ct)
    {
        var (ok, _, _) = await HttpJson.GetStringAsync($"{ServerUrl}/app", new Dictionary<string, string>(), ct);
        return ok ? ServerUrl : null;
    }

    public async Task<UsageSnapshot> GetUsageAsync(UsagePeriod period, CancellationToken ct)
    {
        var snap = new UsageSnapshot
        {
            ProviderId = ProviderId,
            AccountLabel = "this machine",
            Period = period,
            SubscriptionCategory = "Local client observations",
            CostSource = UsageSource.VerifiedLocal,
            CostSourceDescription = "Token/cost fields recorded by the local OpenCode client"
        };

        if (!_metadataConsent)
        {
            snap.ErrorState = "Local agent metadata permission is off";
            return snap;
        }

        var storageUsage = ReadStorageUsage(period);
        var serverUsage = await ReadServerUsageAsync(period, ct);
        if (serverUsage is not null)
        {
            // Prefer the live server session data; it is more current than disk.
            snap.Models = serverUsage;
            snap.Success = true;
        }
        else if (storageUsage.Count > 0)
        {
            snap.Models = storageUsage;
            snap.Success = true;
        }
        else
        {
            snap.Success = false;
            snap.ErrorState = "No OpenCode sessions with token data found in the period";
        }
        snap.LastUpdatedAt = DateTime.Now;
        return snap;
    }

    private List<ModelUsage> ReadStorageUsage(UsagePeriod period)
    {
        var byModel = new Dictionary<string, ModelUsage>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var dir = StorageDir;
            if (!Directory.Exists(dir)) return new List<ModelUsage>();
            foreach (var file in Directory.EnumerateFiles(dir, "*.json", SearchOption.AllDirectories))
            {
                try
                {
                    var fi = new FileInfo(file);
                    if (fi.LastWriteTime < period.Start || fi.LastWriteTime > period.End.AddHours(1)) continue;
                    using var doc = JsonDocument.Parse(File.ReadAllText(file));
                    if (!TryExtractTokens(doc.RootElement, out var input, out var output, out var cached, out var cost, out var model)) continue;
                    var key = string.IsNullOrWhiteSpace(model) ? "unknown" : model;
                    if (!byModel.TryGetValue(key, out var mu)) { mu = new ModelUsage { ModelId = key }; byModel[key] = mu; }
                    mu.InputTokens = (mu.InputTokens ?? 0) + input;
                    mu.OutputTokens = (mu.OutputTokens ?? 0) + output;
                    mu.CachedTokens = (mu.CachedTokens ?? 0) + cached;
                    mu.RequestCount = (mu.RequestCount ?? 0) + 1;
                    if (cost is > 0) mu.EstimatedCost = (mu.EstimatedCost ?? 0) + cost;
                }
                catch { }
            }
        }
        catch { }
        return byModel.Values.ToList();
    }

    private async Task<List<ModelUsage>?> ReadServerUsageAsync(UsagePeriod period, CancellationToken ct)
    {
        var (ok, body, _) = await HttpJson.GetStringAsync($"{ServerUrl}/session", new Dictionary<string, string>(), ct);
        if (!ok) return null;
        try
        {
            var root = HttpJson.ParseObject(body);
            var sessions = root.ValueKind == JsonValueKind.Array
                ? root
                : root.TryGetProperty("data", out var d) ? d : default;
            if (sessions.ValueKind != JsonValueKind.Array) return null;

            var byModel = new Dictionary<string, ModelUsage>(StringComparer.OrdinalIgnoreCase);
            foreach (var session in sessions.EnumerateArray())
            {
                var id = HttpJson.GetString(session, "id");
                if (string.IsNullOrEmpty(id)) continue;
                var updated = HttpJson.GetString(session, "updated") ?? HttpJson.GetString(session, "time");
                if (DateTime.TryParse(updated, out var t) && (t < period.Start || t > period.End.AddHours(1))) continue;

                var (mok, mbody, _) = await HttpJson.GetStringAsync($"{ServerUrl}/session/{id}/message", new Dictionary<string, string>(), ct);
                if (!mok) continue;
                var msgs = HttpJson.ParseObject(mbody);
                var arr = msgs.ValueKind == JsonValueKind.Array ? msgs
                    : msgs.TryGetProperty("data", out var d2) ? d2 : default;
                if (arr.ValueKind != JsonValueKind.Array) continue;
                foreach (var msg in arr.EnumerateArray())
                {
                    var inner = msg.ValueKind == JsonValueKind.Object && msg.TryGetProperty("info", out var inf) ? inf : msg;
                    if (!TryExtractTokens(inner, out var input, out var output, out var cached, out var cost, out var model)) continue;
                    var key = string.IsNullOrWhiteSpace(model) ? "unknown" : model;
                    if (!byModel.TryGetValue(key, out var mu)) { mu = new ModelUsage { ModelId = key }; byModel[key] = mu; }
                    mu.InputTokens = (mu.InputTokens ?? 0) + input;
                    mu.OutputTokens = (mu.OutputTokens ?? 0) + output;
                    mu.CachedTokens = (mu.CachedTokens ?? 0) + cached;
                    mu.RequestCount = (mu.RequestCount ?? 0) + 1;
                    if (cost is > 0) mu.EstimatedCost = (mu.EstimatedCost ?? 0) + cost;
                }
            }
            return byModel.Values.ToList();
        }
        catch { return null; }
    }

    internal static bool TryExtractTokens(JsonElement e, out long input, out long output, out long cached, out double? cost, out string? model)
    {
        input = 0; output = 0; cached = 0; cost = null; model = null;
        if (e.ValueKind != JsonValueKind.Object) return false;

        // tokens may live at: e.tokens, e.assistant.tokens, e.message.tokens …
        JsonElement tokens = default;
        foreach (var path in new[] { "tokens", "assistant", "message" })
        {
            if (e.TryGetProperty(path, out var v))
            {
                if (path == "tokens") tokens = v;
                else if (v.ValueKind == JsonValueKind.Object && v.TryGetProperty("tokens", out var tv)) tokens = tv;
            }
        }
        if (tokens.ValueKind != JsonValueKind.Object) return false;

        long inTok = tokens.TryGetProperty("input", out var i1) && i1.ValueKind == JsonValueKind.Number ? i1.GetInt64()
                   : HttpJson.GetLong(tokens, "input_tokens") ?? 0;
        long outTok = tokens.TryGetProperty("output", out var o1) && o1.ValueKind == JsonValueKind.Number ? o1.GetInt64()
                    : HttpJson.GetLong(tokens, "output_tokens") ?? 0;
        long cache = 0;
        if (tokens.TryGetProperty("cache", out var cacheEl) && cacheEl.ValueKind == JsonValueKind.Object)
        {
            cache += HttpJson.GetLong(cacheEl, "read") ?? 0;
            cache += HttpJson.GetLong(cacheEl, "creation") ?? 0;
        }
        cache += HttpJson.GetLong(tokens, "cache_read") ?? 0;
        cache += HttpJson.GetLong(tokens, "cache_write") ?? 0;

        if (inTok == 0 && outTok == 0) return false;
        input = inTok; output = outTok; cached = cache;
        cost = tokens.TryGetProperty("cost", out var c) && c.ValueKind == JsonValueKind.Number ? c.GetDouble() : null;
        model = HttpJson.GetString(tokens, "modelID") ?? HttpJson.GetString(e, "modelID") ?? HttpJson.GetString(e, "model");
        return true;
    }

    public Task<IReadOnlyList<QuotaWindow>> GetQuotaWindowsAsync(CancellationToken ct)
    {
        IReadOnlyList<QuotaWindow> windows = new List<QuotaWindow>
        {
            new()
            {
                Label = "OpenCode plan allowance",
                Dimension = QuotaDimension.Credits,
                Source = UsageSource.Unavailable,
                SourceDescription = "OpenCode Console/Go plan allowances are visible in the official console; the local client does not expose remaining quota."
            }
        };
        return Task.FromResult(windows);
    }
}
