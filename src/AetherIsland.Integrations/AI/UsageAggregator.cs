using AetherIsland.Core.Logic;
using AetherIsland.Core.Models;

namespace AetherIsland.Integrations.AI;

/// <summary>
/// Coordinates all configured providers: parallel refresh with per-provider error
/// isolation, provenance-preserving aggregation (official vs local totals are NEVER
/// summed into one number), and threshold alert computation.
/// </summary>
public sealed class UsageAggregator
{
    public sealed class ProviderResult
    {
        public string ProviderId { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public ConnectionStatus Status { get; set; } = new();
        public UsageSnapshot? Snapshot { get; set; }
        public Exception? Error { get; set; }
        public DateTime RefreshedAt { get; set; }
    }

    public List<ProviderResult> Results { get; private set; } = new();

    public event Action? Aggregated;

    public async Task RefreshAsync(IReadOnlyList<(string Id, string Name, IAiUsageProvider Provider)> providers, UsagePeriodKind period, CancellationToken ct = default)
    {
        var tasks = providers.Select(async p =>
        {
            var result = new ProviderResult { ProviderId = p.Id, DisplayName = p.Name };
            try
            {
                result.Status = await p.Provider.CheckConnectionAsync(ct);
                if (result.Status.Connected)
                {
                    var snap = await p.Provider.GetUsageAsync(UsagePeriod.Resolve(period, DateTime.Now), ct);
                    var quota = await p.Provider.GetQuotaWindowsAsync(ct);
                    snap.QuotaWindows = quota.ToList();
                    result.Snapshot = snap;
                }
            }
            catch (Exception ex)
            {
                result.Error = ex;
            }
            result.RefreshedAt = DateTime.Now;
            return result;
        }).ToList();

        var done = await Task.WhenAll(tasks);
        Results = done.Where(r => r.Snapshot?.QuotaWindows.Any(q => q.Source == UsageSource.VerifiedOfficial && q.Limit is > 0 && q.Used is >= 0) == true)
            .OrderBy(r => r.DisplayName).ToList();
        Aggregated?.Invoke();
    }

    /// <summary>Totals of OFFICIAL provider reporting only.</summary>
    public (long? input, long? output, double? cost) OfficialTotals => Aggregate(r =>
        r.Snapshot?.CostSource == UsageSource.VerifiedOfficial || r.Snapshot?.SubscriptionCategory?.Contains("API") == true);

    /// <summary>Totals of LOCAL client observations only (kept separate to avoid double counting).</summary>
    public (long? input, long? output, double? cost) LocalTotals => Aggregate(r =>
        r.Snapshot?.CostSource == UsageSource.VerifiedLocal);

    private (long? input, long? output, double? cost) Aggregate(Func<ProviderResult, bool> filter)
    {
        long? input = null, output = null;
        double? cost = null;
        foreach (var r in Results.Where(filter))
        {
            var s = r.Snapshot;
            if (s is null) continue;
            if (s.TotalInputTokens is not null) input = (input ?? 0) + s.TotalInputTokens;
            if (s.TotalOutputTokens is not null) output = (output ?? 0) + s.TotalOutputTokens;
            var c = s.TotalReportedCost ?? s.TotalEstimatedCost;
            if (c is not null) cost = (cost ?? 0) + c;
        }
        return (input, output, cost);
    }

    /// <summary>
    /// Computes quota alerts for a provider. Only VERIFIED sources and MANUAL budgets can
    /// trigger percentage alerts — estimated-only data never pretends to be a quota.
    /// </summary>
    public static AlertCheck? EvaluateAlerts(string providerId, UsageSnapshot snapshot, IEnumerable<int> thresholds)
    {
        if (snapshot is null) return null;
        foreach (var qw in snapshot.QuotaWindows)
        {
            if (qw.Source is not (UsageSource.VerifiedOfficial or UsageSource.ManualBudget)) continue;
            var used = qw.Used;
            if (used is null && qw.Dimension == QuotaDimension.Dollars && snapshot.TotalReportedCost is not null)
                used = snapshot.TotalReportedCost;
            if (used is null && qw.Dimension == QuotaDimension.Dollars && snapshot.TotalEstimatedCost is not null)
                used = snapshot.TotalEstimatedCost;
            var pct = QuotaMath.PercentUsed(used, qw.Limit);
            var crossed = QuotaMath.CrossedThreshold(pct, thresholds);
            if (crossed is not null)
            {
                return new AlertCheck
                {
                    ProviderId = providerId,
                    Percent = pct,
                    Threshold = crossed.Value,
                    Source = qw.Source
                };
            }
        }
        return null;
    }

    public sealed class AlertCheck
    {
        public string ProviderId { get; set; } = "";
        public double? Percent { get; set; }
        public int Threshold { get; set; }
        public UsageSource Source { get; set; }
    }
}
