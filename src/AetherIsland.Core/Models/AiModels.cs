namespace AetherIsland.Core.Models;

/// <summary>AI usage data shared model. Every metric carries its source/provenance category.</summary>
public enum UsageSource
{
    VerifiedOfficial, // from a documented provider API
    VerifiedLocal,    // from approved local client records on this machine
    Estimated,        // derived from records or published pricing
    ManualBudget,     // user-defined limit
    Unavailable,
    Stale,
    Error
}

public enum QuotaDimension
{
    None,
    Tokens,
    Requests,
    Dollars,
    Messages,
    Credits,
    ToolCalls
}

public enum UsagePeriodKind
{
    Today,
    Hours24,
    Days7,
    Days30,
    BillingPeriod,
    Custom
}

public sealed class UsagePeriod
{
    public UsagePeriodKind Kind { get; set; } = UsagePeriodKind.Today;
    public DateTime Start { get; set; }
    public DateTime End { get; set; }

    public static UsagePeriod Resolve(UsagePeriodKind kind, DateTime nowLocal) => kind switch
    {
        UsagePeriodKind.Today => new UsagePeriod { Kind = kind, Start = nowLocal.Date, End = nowLocal },
        UsagePeriodKind.Hours24 => new UsagePeriod { Kind = kind, Start = nowLocal.AddHours(-24), End = nowLocal },
        UsagePeriodKind.Days7 => new UsagePeriod { Kind = kind, Start = nowLocal.Date.AddDays(-6), End = nowLocal },
        UsagePeriodKind.Days30 => new UsagePeriod { Kind = kind, Start = nowLocal.Date.AddDays(-29), End = nowLocal },
        UsagePeriodKind.BillingPeriod => new UsagePeriod { Kind = kind, Start = new DateTime(nowLocal.Year, nowLocal.Month, 1), End = nowLocal },
        _ => new UsagePeriod { Kind = kind, Start = nowLocal.Date, End = nowLocal }
    };
}

public sealed class ModelUsage
{
    public string ModelId { get; set; } = "";
    public long? InputTokens { get; set; }
    public long? OutputTokens { get; set; }
    public long? CachedTokens { get; set; }
    public long? RequestCount { get; set; }
    public double? ReportedCost { get; set; }
    public double? EstimatedCost { get; set; }
    public string? Currency { get; set; }
}

public sealed class QuotaWindow
{
    /// <summary>Human label, e.g. "5-hour window", "Weekly", "Monthly plan".</summary>
    public string Label { get; set; } = "";
    public QuotaDimension Dimension { get; set; } = QuotaDimension.None;
    public double? Limit { get; set; }
    public double? Used { get; set; }
    public double? Remaining { get; set; }
    public DateTime? WindowStart { get; set; }
    public DateTime? WindowEnd { get; set; }
    public DateTime? ResetAt { get; set; }
    public UsageSource Source { get; set; } = UsageSource.Unavailable;
    public string SourceDescription { get; set; } = "";
    public DateTime? LastUpdatedAt { get; set; }
    public string? ErrorState { get; set; }
}

public sealed class UsageSnapshot
{
    public string ProviderId { get; set; } = "";
    public string ConnectionId { get; set; } = "";
    public string? AccountLabel { get; set; }
    public string? SubscriptionCategory { get; set; } // e.g. "API organization", "Coding plan"
    public UsagePeriod Period { get; set; } = new();
    public List<ModelUsage> Models { get; set; } = new();
    public List<QuotaWindow> QuotaWindows { get; set; } = new();

    public long? TotalInputTokens => Sum(Models.Select(m => m.InputTokens));
    public long? TotalOutputTokens => Sum(Models.Select(m => m.OutputTokens));
    public long? TotalCachedTokens => Sum(Models.Select(m => m.CachedTokens));
    public long? TotalRequests => Sum(Models.Select(m => m.RequestCount));
    public double? TotalReportedCost => Models.Any(m => m.ReportedCost is not null) ? Models.Sum(m => m.ReportedCost) : null;
    public double? TotalEstimatedCost => Models.Any(m => m.EstimatedCost is not null) ? Models.Sum(m => m.EstimatedCost) : null;
    public string Currency { get; set; } = "USD";

    public UsageSource CostSource { get; set; } = UsageSource.Unavailable;
    public string CostSourceDescription { get; set; } = "";
    public string? UsageDashboardUrl { get; set; }
    public DateTime? LastUpdatedAt { get; set; }
    public string? ErrorState { get; set; }
    public bool Success { get; set; }

    private static long? Sum(IEnumerable<long?> values)
    {
        var any = false; long total = 0;
        foreach (var v in values) { if (v is not null) { any = true; total += v.Value; } }
        return any ? total : null;
    }
}

public sealed class ConnectionStatus
{
    public bool Connected { get; set; }
    public string State { get; set; } = "unknown"; // connected | disconnected | error | unauthorized | notfound
    public string Message { get; set; } = "";
    public DateTime? CheckedAt { get; set; }
    public string? AccountLabel { get; set; }
}

public sealed class AgentInfo
{
    public string AgentName { get; set; } = "";
    public string State { get; set; } = "idle"; // running | idle | waiting | completed | error | offline
    public string? Model { get; set; }
    public string? Provider { get; set; }
    public string? ProjectLabel { get; set; }
    public DateTime? StartedAt { get; set; }
    public TimeSpan? Duration => StartedAt is null ? null : DateTime.Now - StartedAt;
    public long? TokensUsed { get; set; }
    public double? EstimatedCost { get; set; }
    public DateTime? LastActivity { get; set; }
    public UsageSource Source { get; set; } = UsageSource.Unavailable;
}

/// <summary>Interface every AI usage adapter implements (spec §9).</summary>
public interface IAiUsageProvider
{
    string ProviderId { get; }
    string DisplayName { get; set; }
    Task<ConnectionStatus> CheckConnectionAsync(CancellationToken cancellationToken);
    Task<UsageSnapshot> GetUsageAsync(UsagePeriod period, CancellationToken cancellationToken);
    Task<IReadOnlyList<QuotaWindow>> GetQuotaWindowsAsync(CancellationToken cancellationToken);
}
