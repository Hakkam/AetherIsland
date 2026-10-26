using AetherIsland.Core.Models;

namespace AetherIsland.Integrations.AI;

/// <summary>
/// Z.AI / GLM adapter. Authentication is via the Z.AI OpenAI-compatible API.
/// Quota windows (coding-plan 5h/weekly allowances, prepaid balance) are only marked
/// VERIFIED when an endpoint the user configures actually returns them; anything else
/// is reported as Unavailable with a dashboard link. Authenticated pages are never scraped.
/// </summary>
public sealed class ZaiProvider : IAiUsageProvider
{
    public string ProviderId => AiProviderIds.Zai;
    public string DisplayName { get; set; } = "Z.AI / GLM";

    private readonly ProviderConfig _cfg;
    public const string DashboardUrl = "https://z.ai/manage-apikey/usage";

    public ZaiProvider(ProviderConfig cfg) => _cfg = cfg;

    private string Base => (_cfg.BaseUrl?.TrimEnd('/') is { Length: > 0 } b) ? b : "https://api.z.ai";

    public async Task<ConnectionStatus> CheckConnectionAsync(CancellationToken ct)
    {
        var key = _cfg.GetSecret?.Invoke();
        if (string.IsNullOrWhiteSpace(key))
            return new ConnectionStatus { Connected = false, State = "disconnected", Message = "No API key configured", CheckedAt = DateTime.Now };

        var (ok, body, status) = await HttpJson.GetStringAsync($"{Base}/api/paas/v4/models",
            new Dictionary<string, string> { ["Authorization"] = $"Bearer {key}" }, ct);
        return new ConnectionStatus
        {
            Connected = ok,
            State = ok ? "connected" : status == 401 ? "unauthorized" : "error",
            Message = ok ? "Key accepted (OpenAI-compatible endpoint)"
                         : $"HTTP {status}: {OpenAiProvider.Truncate(body)}",
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
            AccountLabel = _cfg.AccountLabel,
            Period = period,
            UsageDashboardUrl = DashboardUrl,
            SubscriptionCategory = "Z.AI API"
        };

        if (string.IsNullOrWhiteSpace(key))
        {
            snap.ErrorState = "No API key configured";
            return snap;
        }

        // Token-level billing data is available per-request from Z.AI but there is no
        // documented aggregate usage endpoint; do not fabricate one.
        snap.Success = false;
        snap.ErrorState = "Aggregate usage is not available from a documented public endpoint. Token billing lives in the Z.AI console; set a manual budget to track locally.";
        snap.CostSource = UsageSource.Unavailable;
        snap.CostSourceDescription = "No documented aggregate usage endpoint";
        snap.LastUpdatedAt = DateTime.Now;
        await Task.CompletedTask;
        return snap;
    }

    public Task<IReadOnlyList<QuotaWindow>> GetQuotaWindowsAsync(CancellationToken ct)
    {
        var windows = new List<QuotaWindow>
        {
            new()
            {
                Label = "Coding plan (5h window)",
                Dimension = QuotaDimension.Credits,
                Source = UsageSource.Unavailable,
                SourceDescription = "Coding-plan usage windows are only visible inside the official Z.AI console; no authorized external endpoint is documented."
            },
            new()
            {
                Label = "Prepaid balance",
                Dimension = QuotaDimension.Dollars,
                Source = UsageSource.Unavailable,
                SourceDescription = "Balance is visible in the Z.AI console."
            }
        };
        if (_cfg.ManualMonthlyBudgetUsd is > 0)
        {
            windows.Add(new QuotaWindow
            {
                Label = "Manual monthly budget",
                Dimension = QuotaDimension.Dollars,
                Limit = _cfg.ManualMonthlyBudgetUsd,
                Source = UsageSource.ManualBudget,
                SourceDescription = "User-defined budget; usage tracked from local records only."
            });
        }
        return Task.FromResult<IReadOnlyList<QuotaWindow>>(windows);
    }
}
