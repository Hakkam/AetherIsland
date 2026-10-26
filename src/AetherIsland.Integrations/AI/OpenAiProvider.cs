using System.Text.Json;
using AetherIsland.Core.Models;

namespace AetherIsland.Integrations.AI;

/// <summary>
/// OpenAI adapter using the official organization usage &amp; cost reporting endpoints:
///   GET /v1/organization/usage/completions  (requires an admin-scoped API key)
///   GET /v1/organization/costs
/// An ordinary project key validates connectivity but cannot read organization usage —
/// the adapter reports that honestly instead of estimating.
/// </summary>
public sealed class OpenAiProvider : IAiUsageProvider
{
    public string ProviderId => AiProviderIds.OpenAI;
    public string DisplayName { get; set; } = "OpenAI";

    private readonly ProviderConfig _cfg;
    public const string DashboardUrl = "https://platform.openai.com/usage";

    public OpenAiProvider(ProviderConfig cfg) => _cfg = cfg;

    private string Base => (_cfg.BaseUrl?.TrimEnd('/') is { Length: > 0 } b) ? b : "https://api.openai.com";

    private Dictionary<string, string> Headers(string? key)
    {
        var h = new Dictionary<string, string> { ["Authorization"] = $"Bearer {key}" };
        return h;
    }

    public async Task<ConnectionStatus> CheckConnectionAsync(CancellationToken ct)
    {
        var key = _cfg.GetSecret?.Invoke();
        if (string.IsNullOrWhiteSpace(key))
            return new ConnectionStatus { Connected = false, State = "disconnected", Message = "No API key configured", CheckedAt = DateTime.Now };

        // Admin keys are validated against the reporting endpoint they actually need.
        var probeEnd = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var probe = await HttpJson.GetStringAsync(
            OrgUsageUrl(probeEnd - 86400, probeEnd), Headers(key), ct);
        return new ConnectionStatus
        {
            Connected = probe.ok,
            State = probe.ok ? "connected" : probe.status is 401 or 403 ? "unauthorized" : "error",
            Message = probe.ok ? "Organization usage reporting available"
                : probe.status is 401 or 403 ? "An organization admin key is required"
                : $"HTTP {probe.status}: {Truncate(probe.body)}",
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

        var startUnix = new DateTimeOffset(period.Start.ToUniversalTime()).ToUnixTimeSeconds();
        var endUnix = new DateTimeOffset(period.End.ToUniversalTime()).ToUnixTimeSeconds();
        var url = OrgUsageUrl(startUnix, endUnix);
        var (ok, body, status) = await HttpJson.GetStringAsync(url, Headers(key), ct);
        if (!ok)
        {
            snap.ErrorState = status switch
            {
                401 or 403 => "Usage reporting requires an organization admin-scoped key (HTTP " + status + ")",
                _ => $"HTTP {status}: {Truncate(body)}"
            };
            snap.Success = false;
            return snap;
        }

        try
        {
            var root = HttpJson.ParseObject(body);
            if (root.TryGetProperty("has_more", out var more) && more.ValueKind == JsonValueKind.True)
                throw new JsonException("Usage report requires pagination");
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
                        target.InputTokens = (target.InputTokens ?? 0) + (HttpJson.GetLong(r, "input_tokens") ?? 0);
                        target.OutputTokens = (target.OutputTokens ?? 0) + (HttpJson.GetLong(r, "output_tokens") ?? 0);
                        target.CachedTokens = (target.CachedTokens ?? 0) + (HttpJson.GetLong(r, "input_cached_tokens") ?? 0);
                        target.RequestCount = (target.RequestCount ?? 0) + (HttpJson.GetLong(r, "num_model_requests") ?? 0);
                    }
                }
            }
            snap.Success = true;
            snap.CostSourceDescription = "Tokens from /v1/organization/usage/completions";
            snap.LastUpdatedAt = DateTime.Now;

            // Costs from /v1/organization/costs — requires admin scope; failures are non-fatal.
            var (costOk, costBody, _) = await HttpJson.GetStringAsync(OrgCostsUrl(startUnix, endUnix), Headers(key), ct);
            if (costOk)
            {
                var total = ParseCostUsd(costBody);
                snap.Models.Add(new ModelUsage { ModelId = "(all models)", ReportedCost = Math.Round(total, 4) });
                snap.CostSource = UsageSource.VerifiedOfficial;
            }
        }
        catch (Exception ex)
        {
            snap.ErrorState = "Parse error: " + ex.Message;
        }
        return snap;
    }

    public async Task<IReadOnlyList<QuotaWindow>> GetQuotaWindowsAsync(CancellationToken ct)
    {
        var key = _cfg.GetSecret?.Invoke();
        if (string.IsNullOrWhiteSpace(key)) return Array.Empty<QuotaWindow>();

        // A hard organization spend limit is distinct from subscription allowances.
        var (limitOk, limitBody, _) = await HttpJson.GetStringAsync(
            $"{Base}/v1/organization/spend_limit", Headers(key), ct);
        if (!limitOk) return Array.Empty<QuotaWindow>();

        double? threshold;
        try
        {
            var limit = HttpJson.ParseObject(limitBody);
            var enforcing = limit.TryGetProperty("enforcement", out var enforcement)
                && HttpJson.GetString(enforcement, "status") == "enforcing";
            if (!enforcing || HttpJson.GetString(limit, "interval") != "month"
                || HttpJson.GetString(limit, "currency") != "USD")
                return Array.Empty<QuotaWindow>();
            threshold = HttpJson.GetDouble(limit, "threshold_amount");
        }
        catch (JsonException) { return Array.Empty<QuotaWindow>(); }
        if (threshold is not > 0) return Array.Empty<QuotaWindow>();

        var now = DateTime.Now;
        var start = new DateTime(now.Year, now.Month, 1);
        var startUnix = new DateTimeOffset(start.ToUniversalTime()).ToUnixTimeSeconds();
        var endUnix = new DateTimeOffset(now.ToUniversalTime()).ToUnixTimeSeconds();
        var (costOk, costBody, _) = await HttpJson.GetStringAsync(
            OrgCostsUrl(startUnix, endUnix), Headers(key), ct);
        if (!costOk) return Array.Empty<QuotaWindow>();

        double spent;
        try { spent = ParseCostUsd(costBody); }
        catch (JsonException) { return Array.Empty<QuotaWindow>(); }

        return new[] { new QuotaWindow
        {
            Label = "Organization monthly hard spend limit",
            Dimension = QuotaDimension.Dollars,
            Limit = threshold.Value / 100.0,
            Used = spent,
            Remaining = Math.Max(0, threshold.Value / 100.0 - spent),
            WindowStart = start,
            WindowEnd = start.AddMonths(1),
            ResetAt = start.AddMonths(1),
            Source = UsageSource.VerifiedOfficial,
            SourceDescription = "Enforced organization limit and monthly cost from OpenAI Admin API",
            LastUpdatedAt = now
        } };
    }

    internal static double ParseCostUsd(string body)
    {
        var root = HttpJson.ParseObject(body);
        double total = 0;
        if (root.TryGetProperty("has_more", out var more) && more.ValueKind == JsonValueKind.True)
            throw new JsonException("Cost report requires pagination");
        if (!root.TryGetProperty("data", out var buckets) || buckets.ValueKind != JsonValueKind.Array)
            throw new JsonException("Cost report has no data array");
        foreach (var bucket in buckets.EnumerateArray())
                if (bucket.TryGetProperty("results", out var results))
                    foreach (var result in results.EnumerateArray())
                        if (result.TryGetProperty("amount", out var amount))
                            total += HttpJson.GetDouble(amount, "value") ?? 0;
        return total;
    }

    private string OrgUsageUrl(object startUnix, object endUnix) =>
        $"{Base}/v1/organization/usage/completions?start_time={startUnix}&end_time={endUnix}&bucket_width=1d&limit=31&group_by=model";

    private string OrgCostsUrl(object startUnix, object endUnix) =>
        $"{Base}/v1/organization/costs?start_time={startUnix}&end_time={endUnix}&bucket_width=1d&limit=31";

    internal static string Truncate(string s) => s.Length <= 160 ? s : s[..160] + "…";
}
