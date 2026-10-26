using System.Text.Json;
using AetherIsland.Core.Models;

namespace AetherIsland.Integrations.AI;

/// <summary>
/// Anthropic adapter using the official Usage &amp; Cost Admin API:
///   GET /v1/organizations/usage_report/messages  (admin-key, org scope)
///   GET /v1/organizations/cost_report
/// Personal Claude subscription usage is deliberately NOT supported here — the public
/// API does not expose it and browser-session scraping is out of scope by policy.
/// </summary>
public sealed class AnthropicProvider : IAiUsageProvider
{
    public string ProviderId => AiProviderIds.Anthropic;
    public string DisplayName { get; set; } = "Anthropic";

    private readonly ProviderConfig _cfg;
    public const string DashboardUrl = "https://console.anthropic.com/settings/usage";

    public AnthropicProvider(ProviderConfig cfg) => _cfg = cfg;

    private string Base => (_cfg.BaseUrl?.TrimEnd('/') is { Length: > 0 } b) ? b : "https://api.anthropic.com";

    private Dictionary<string, string> Headers(string? key) => new()
    {
        ["x-api-key"] = key ?? "",
        ["anthropic-version"] = "2023-06-01"
    };

    public async Task<ConnectionStatus> CheckConnectionAsync(CancellationToken ct)
    {
        var key = _cfg.GetSecret?.Invoke();
        if (string.IsNullOrWhiteSpace(key))
            return new ConnectionStatus { Connected = false, State = "disconnected", Message = "No API key configured", CheckedAt = DateTime.Now };

        var (ok, body, status) = await HttpJson.GetStringAsync($"{Base}/v1/models", Headers(key), ct);
        if (!ok)
            return new ConnectionStatus
            {
                Connected = false,
                State = status == 401 ? "unauthorized" : "error",
                Message = status == 401 ? "Invalid API key" : $"HTTP {status}: {OpenAiProvider.Truncate(body)}",
                CheckedAt = DateTime.Now
            };

        var start = DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-ddTHH:mm:ssZ");
        var probe = await HttpJson.GetStringAsync(UsageUrl(start, DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")), Headers(key), ct);
        return new ConnectionStatus
        {
            Connected = true,
            State = "connected",
            Message = probe.ok ? "Usage & cost admin reporting available"
                               : "Key is valid; usage reports need an organization admin key",
            AccountLabel = _cfg.AccountLabel,
            CheckedAt = DateTime.Now
        };
    }

    public async Task<UsageSnapshot> GetUsageAsync(UsagePeriod period, CancellationToken ct)
    {
        var key = _cfg.GetSecret?.Invoke();
        var snap = new UsageSnapshot
        {
            ProviderId = ProviderId,
            ConnectionId = "default",
            AccountLabel = _cfg.AccountLabel,
            Period = period,
            UsageDashboardUrl = DashboardUrl,
            SubscriptionCategory = "API (organization reporting)"
        };

        if (string.IsNullOrWhiteSpace(key))
        {
            snap.ErrorState = "No API key configured";
            return snap;
        }

        var (ok, body, status) = await HttpJson.GetStringAsync(
            UsageUrl(period.Start.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ"),
                     period.End.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")),
            Headers(key), ct);
        if (!ok)
        {
            snap.ErrorState = status switch
            {
                401 or 403 => "Usage reports require an organization admin key with usage:read scope (HTTP " + status + ")",
                _ => $"HTTP {status}: {OpenAiProvider.Truncate(body)}"
            };
            return snap;
        }

        try
        {
            var root = HttpJson.ParseObject(body);
            if (root.TryGetProperty("data", out var buckets))
            {
                foreach (var bucket in buckets.EnumerateArray())
                {
                    if (!bucket.TryGetProperty("results", out var results)) continue;
                    foreach (var r in results.EnumerateArray())
                    {
                        var model = HttpJson.GetString(r, "model") ?? "unknown";
                        var target = snap.Models.FirstOrDefault(m => m.ModelId == model);
                        if (target is null) { target = new ModelUsage { ModelId = model }; snap.Models.Add(target); }
                        if (r.TryGetProperty("usage", out var u))
                        {
                            target.InputTokens = (target.InputTokens ?? 0) + (HttpJson.GetLong(u, "input_tokens") ?? 0);
                            target.OutputTokens = (target.OutputTokens ?? 0) + (HttpJson.GetLong(u, "output_tokens") ?? 0);
                            target.CachedTokens = (target.CachedTokens ?? 0)
                                + (HttpJson.GetLong(u, "cache_creation_input_tokens") ?? 0)
                                + (HttpJson.GetLong(u, "cache_read_input_tokens") ?? 0);
                        }
                    }
                }
            }
            snap.Success = true;
            snap.CostSource = UsageSource.VerifiedOfficial;
            snap.CostSourceDescription = "Tokens from /v1/organizations/usage_report/messages";
            snap.LastUpdatedAt = DateTime.Now;

            var (costOk, costBody, _) = await HttpJson.GetStringAsync(
                CostsUrl(period.Start.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ"),
                         period.End.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")),
                Headers(key), ct);
            if (costOk)
            {
                var costRoot = HttpJson.ParseObject(costBody);
                double total = 0;
                if (costRoot.TryGetProperty("data", out var rows))
                    foreach (var row in rows.EnumerateArray())
                        if (row.TryGetProperty("results", out var results))
                            foreach (var r in results.EnumerateArray())
                                total += HttpJson.GetDouble(r, "cost_usd") ?? 0;
                if (total > 0)
                    snap.Models.Add(new ModelUsage { ModelId = "(all models)", ReportedCost = Math.Round(total, 4) });
            }
        }
        catch (Exception ex)
        {
            snap.ErrorState = "Parse error: " + ex.Message;
        }
        return snap;
    }

    public Task<IReadOnlyList<QuotaWindow>> GetQuotaWindowsAsync(CancellationToken ct)
    {
        IReadOnlyList<QuotaWindow> windows = new List<QuotaWindow>
        {
            new()
            {
                Label = "Claude subscription quota",
                Dimension = QuotaDimension.None,
                Source = UsageSource.Unavailable,
                SourceDescription = "Claude Pro/Max subscription limits are not exposed by any public API and are not read from the browser.",
            },
            new()
            {
                Label = "Rate limits",
                Dimension = QuotaDimension.Requests,
                Source = UsageSource.Unavailable,
                SourceDescription = "Organization rate limits are returned per-request via response headers, not through the reporting API."
            }
        };
        return Task.FromResult(windows);
    }

    private string UsageUrl(string startIso, string endIso) =>
        $"{Base}/v1/organizations/usage_report/messages?starting_at={Uri.EscapeDataString(startIso)}&ending_at={Uri.EscapeDataString(endIso)}&bucket_width=1d&limit=31";

    private string CostsUrl(string startIso, string endIso) =>
        $"{Base}/v1/organizations/cost_report?starting_at={Uri.EscapeDataString(startIso)}&ending_at={Uri.EscapeDataString(endIso)}&limit=31";
}
