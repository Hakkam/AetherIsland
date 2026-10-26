using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AetherIsland.Core;
using AetherIsland.Core.Logic;
using AetherIsland.Core.Models;
using AetherIsland.Integrations.AI;

namespace AetherIsland.App.ViewModels;

public partial class AiCenterViewModel : ObservableObject, ILifecycleAware
{
    [ObservableProperty] private string _title = Loc.T("ai.title");
    [ObservableProperty] private string _connectedCountText = Loc.TF("ai.connectedCount", 0);
    [ObservableProperty] private string _lastUpdatedText = "";
    [ObservableProperty] private string _refreshLabel = Loc.T("ai.refreshAll");
    [ObservableProperty] private string _dedupNote = Loc.T("ai.dedupNote");
    [ObservableProperty] private bool _isRefreshing;

    [ObservableProperty] private ObservableCollection<ProviderCardVm> _providers = new();
    [ObservableProperty] private ObservableCollection<AgentRowVm> _agents = new();
    [ObservableProperty] private ProviderCardVm? _selectedProvider;
    [ObservableProperty] private bool _hasProviders;
    [ObservableProperty] private bool _hasAgents;
    [ObservableProperty] private string _agentsTitle = Loc.T("ai.agents");
    [ObservableProperty] private string _agentsEmptyText = Loc.T("ai.agentsNone");

    [ObservableProperty] private string _officialTotalsText = "";
    [ObservableProperty] private string _localTotalsText = "";

    [ObservableProperty] private string _periodToday = Loc.T("common.today");
    [ObservableProperty] private string _period24H = Loc.T("ai.hours24");
    [ObservableProperty] private string _period7D = Loc.T("ai.days7");
    [ObservableProperty] private string _period30D = Loc.T("ai.days30");
    [ObservableProperty] private string _periodBilling = Loc.T("ai.byPeriod");
    [ObservableProperty] private string _selectedPeriod = "billing";
    [ObservableProperty] private string _settingsTooltip = Loc.T("common.settings");
    [ObservableProperty] private string _noProvidersText = Loc.T("ai.connectPrompt");
    [ObservableProperty] private string _addProviderLabel = Loc.T("ai.addProvider");

    public bool NoProviders => !HasProviders;

    public AiCenterViewModel()
    {
        AppServices.Ai.Refreshed += () => System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(OnAggregated);
    }

    public void OnShown()
    {
        _ = RefreshAsync();
    }

    public void OnHidden() { }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsRefreshing = true;
        try { await AppServices.Ai.RefreshAsync(ResolvePeriod(SelectedPeriod)); }
        finally { IsRefreshing = false; }
    }

    [RelayCommand]
    private void SetPeriod(string period)
    {
        SelectedPeriod = period;
        _ = RefreshAsync();
    }

    private static UsagePeriodKind ResolvePeriod(string p) => p switch
    {
        "today" => UsagePeriodKind.Today,
        "h24" => UsagePeriodKind.Hours24,
        "d7" => UsagePeriodKind.Days7,
        "d30" => UsagePeriodKind.Days30,
        _ => UsagePeriodKind.BillingPeriod
    };

    [RelayCommand]
    private void SelectProvider(ProviderCardVm? card) => SelectedProvider = card;

    [RelayCommand]
    private void OpenDashboard(ProviderCardVm card)
    {
        var url = card.DashboardUrl;
        if (string.IsNullOrEmpty(url)) return;
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception ex) { AppServices.Notify.ShowBalloon("Aether Island", ex.Message); }
    }

    [RelayCommand]
    private void OpenAiSettings() => Windows.SettingsWindow.ShowOnce("ai");

    private void OnAggregated()
    {
        var results = AppServices.Ai.Aggregator.Results;
        ConnectedCountText = Loc.TF("ai.connectedCount", results.Count(r => r.Status.Connected));
        LastUpdatedText = AppServices.Ai.LastRefresh is { } t
            ? $"{Loc.T("common.lastUpdated")}: {t:H:mm:ss}"
            : "";

        var official = AppServices.Ai.Aggregator.OfficialTotals;
        var local = AppServices.Ai.Aggregator.LocalTotals;
        OfficialTotalsText = official.input is > 0 or null
            ? $"{Loc.T("ai.source.official")}: {QuotaMath.FormatTokens(official.input)} {Loc.T("ai.input").ToLowerInvariant()} · {QuotaMath.FormatTokens(official.output)} {Loc.T("ai.output").ToLowerInvariant()}" + (official.cost is > 0 ? $" · ${official.cost:0.00}" : "")
            : Loc.T("ai.notAvailableFromProvider");
        LocalTotalsText = local.input is > 0
            ? $"{Loc.T("ai.source.local")}: {QuotaMath.FormatTokens(local.input)} {Loc.T("ai.input").ToLowerInvariant()} · {QuotaMath.FormatTokens(local.output)} {Loc.T("ai.output").ToLowerInvariant()}" + (local.cost is > 0 ? $" · ${local.cost:0.00}" : "")
            : Loc.T("ai.notAvailableFromProvider");

        var old = Providers.ToDictionary(p => p.ProviderId, p => p.IsExpanded);
        Providers.Clear();
        foreach (var r in results)
            Providers.Add(new ProviderCardVm(r) { IsExpanded = old.TryGetValue(r.ProviderId, out var ex) && ex });
        HasProviders = Providers.Count > 0;
        NoProvidersText = AppServices.Settings.Ai.Providers.Any(p => p.Enabled && p.ProviderId == AiProviderIds.OpenAI)
            ? Loc.T("ai.noVerifiedLimit")
            : Loc.T("ai.connectPrompt");
        OnPropertyChanged(nameof(NoProviders));
        if (SelectedProvider is { } sel)
            SelectedProvider = Providers.FirstOrDefault(p => p.ProviderId == sel.ProviderId);

        Agents.Clear();
        foreach (var a in AppServices.Ai.Agents.Detect())
            Agents.Add(new AgentRowVm(a));
        HasAgents = Agents.Count > 0;
    }
}

public partial class ProviderCardVm : ObservableObject
{
    public string ProviderId { get; }
    public string DisplayName { get; }
    public bool Connected { get; }
    public string StatusText { get; }
    public string StatusBrush { get; }
    public string? AccountLabel { get; }
    public string? SubscriptionCategory { get; }
    public string TokensText { get; }
    public string RequestsText { get; }
    public string CostText { get; }
    public string CostSourceLabel { get; }
    public string CostSourceBrush { get; }
    public string LastSyncText { get; }
    public string? DashboardUrl { get; }
    public string OpenDashboardLabel { get; } = Loc.T("ai.openDashboard");
    public bool HasDashboard => !string.IsNullOrEmpty(DashboardUrl);
    public string NotAvailableText { get; } = Loc.T("ai.notAvailableFromProvider");
    public string ByModelTitle { get; } = Loc.T("ai.byModel");
    public string TokensTitle { get; } = Loc.T("ai.tokens");
    public string RequestsTitle { get; } = Loc.T("ai.requests");
    public string CostSourceTitle { get; } = Loc.T("ai.cost");

    public ObservableCollection<QuotaLineVm> QuotaLines { get; } = new();
    public ObservableCollection<ModelRowVm> ModelRows { get; } = new();
    public bool HasModelRows => ModelRows.Count > 0;
    public bool HasQuotaLines => QuotaLines.Any(q => !q.IsUnavailable);
    public bool ShowNotAvailable => QuotaLines.Count > 0 && QuotaLines.All(q => q.IsUnavailable);

    [ObservableProperty] private bool _isExpanded;

    public ProviderCardVm(UsageAggregator.ProviderResult result)
    {
        ProviderId = result.ProviderId;
        DisplayName = result.DisplayName;
        Connected = result.Status.Connected;
        StatusText = result.Status.State switch
        {
            "connected" => Loc.T("common.connected"),
            "unauthorized" => Loc.T("common.error") + " · " + result.Status.Message,
            "notfound" => Loc.T("common.disconnected") + " · " + result.Status.Message,
            "disconnected" => Loc.T("common.disconnected"),
            _ => Loc.T("common.error") + (result.Error is null ? "" : $" · {result.Error.Message}")
        };
        if (result.Status.State == "disconnected" && result.Status.Message.Length > 0)
            StatusText += " · " + result.Status.Message;
        StatusBrush = Connected ? "SuccessBrush" : result.Status.State is "unauthorized" or "error" ? "DangerBrush" : "MutedBrush";
        AccountLabel = result.Status.AccountLabel;
        SubscriptionCategory = result.Snapshot?.SubscriptionCategory;
        DashboardUrl = result.Snapshot?.UsageDashboardUrl;
        LastSyncText = $"{Loc.T("ai.lastSync")}: {result.RefreshedAt:H:mm}";

        var snap = result.Snapshot;
        if (snap is not null)
        {
            TokensText = $"{QuotaMath.FormatTokens(snap.TotalInputTokens)} / {QuotaMath.FormatTokens(snap.TotalOutputTokens)}";
            RequestsText = QuotaMath.FormatTokens(snap.TotalRequests);
            var cost = snap.TotalReportedCost ?? snap.TotalEstimatedCost;
            CostText = cost is null ? "—" : $"${cost:0.0000}";
            CostSourceLabel = Loc.T(snap.TotalReportedCost is not null ? "ai.source.official"
                : snap.TotalEstimatedCost is not null ? "ai.source.estimated" : "ai.source.unavailable");
            CostSourceBrush = snap.TotalReportedCost is not null ? "SuccessBrush" : "WarningBrush";

            foreach (var qw in snap.QuotaWindows)
                QuotaLines.Add(new QuotaLineVm(qw, snap));
            foreach (var m in snap.Models.Where(m => m.ModelId != "(all models)"))
                ModelRows.Add(new ModelRowVm(m));
            if (snap.ErrorState is not null && !Connected)
                StatusText = Loc.T("common.error") + " · " + snap.ErrorState;
        }
        else
        {
            TokensText = "— / —";
            RequestsText = "—";
            CostText = "—";
            CostSourceLabel = Loc.T("ai.source.unavailable");
            CostSourceBrush = "MutedBrush";
        }
    }
}

public partial class QuotaLineVm : ObservableObject
{
    public string Label { get; }
    public bool IsUnavailable { get; }
    public string UnavailableText { get; } = Loc.T("ai.notAvailableFromProvider");
    public string UsedText { get; }
    public string LimitText { get; }
    public string RemainingText { get; }
    public double? Percent { get; }
    public string PercentText { get; }
    public string SourceLabel { get; }
    public string SourceBrush { get; }
    public string ResetText { get; }
    public string ResetLabel { get; } = "";

    public QuotaLineVm(QuotaWindow qw, UsageSnapshot snap)
    {
        Label = qw.Label;
        IsUnavailable = qw.Source == UsageSource.Unavailable;
        SourceLabel = Loc.T(qw.Source switch
        {
            UsageSource.VerifiedOfficial => "ai.source.official",
            UsageSource.VerifiedLocal => "ai.source.local",
            UsageSource.Estimated => "ai.source.estimated",
            UsageSource.ManualBudget => "ai.source.manual",
            UsageSource.Unavailable => "ai.source.unavailable",
            UsageSource.Stale => "ai.source.stale",
            _ => "ai.source.error"
        });
        SourceBrush = qw.Source switch
        {
            UsageSource.VerifiedOfficial => "SuccessBrush",
            UsageSource.VerifiedLocal => "SuccessBrush",
            UsageSource.ManualBudget => "AccentBrush",
            UsageSource.Estimated => "WarningBrush",
            _ => "MutedBrush"
        };

        if (IsUnavailable)
        {
            UsedText = LimitText = RemainingText = PercentText = "—";
            Percent = null;
            ResetText = qw.SourceDescription.Length > 0 ? qw.SourceDescription : "—";
            return;
        }

        // Resolve "used": direct value, or dollars from the snapshot for manual budgets
        double? used = qw.Used;
        if (used is null && qw.Dimension == QuotaDimension.Dollars)
            used = snap.TotalReportedCost ?? snap.TotalEstimatedCost;
        var pct = QuotaMath.PercentUsed(used, qw.Limit);
        Percent = pct is null ? null : Math.Clamp(pct.Value / 100.0, 0, 1);
        PercentText = pct is null ? "—" : $"{Math.Round(pct.Value)}%";

        UsedText = Format(qw.Dimension, used);
        LimitText = Format(qw.Dimension, qw.Limit);
        RemainingText = Format(qw.Dimension, QuotaMath.Remaining(qw.Limit, used));
        UsedLimitText = $"{UsedText} / {LimitText}";
        ResetText = qw.ResetAt is { } r
            ? Loc.TF("ai.resetAt", r < DateTime.Now ? Loc.T("common.today") : r.ToString("ddd H:mm"))
            : qw.SourceDescription;
    }

    public string UsedLimitText { get; private set; } = "—";

    private static string Format(QuotaDimension dim, double? v)
    {
        if (v is null) return "—";
        return dim switch
        {
            QuotaDimension.Dollars => $"${v:0.00}",
            QuotaDimension.Tokens => QuotaMath.FormatTokens((long)v),
            QuotaDimension.Requests => ((long)v).ToString("N0"),
            QuotaDimension.Credits => $"{v:0.##}",
            _ => $"{v:0.##}"
        };
    }
}

public sealed record ModelRowVm(string ModelId, string InputText, string OutputText, string CachedText, string RequestsText, string CostText)
{
    public ModelRowVm(ModelUsage m) : this(
        m.ModelId,
        QuotaMath.FormatTokens(m.InputTokens),
        QuotaMath.FormatTokens(m.OutputTokens),
        QuotaMath.FormatTokens(m.CachedTokens),
        QuotaMath.FormatTokens(m.RequestCount),
        (m.ReportedCost ?? m.EstimatedCost) is { } c ? $"${c:0.0000}" : "—")
    { }
}

public partial class AgentRowVm : ObservableObject
{
    public string Name { get; }
    public string State { get; }
    public string StateText { get; }
    public string StateBrush { get; }
    public string? Project { get; }
    public string DurationText { get; }
    public string LastActivityText { get; }
    public string Provider { get; }

    public AgentRowVm(AgentInfo a)
    {
        Name = a.AgentName;
        State = a.State;
        Provider = a.Provider ?? "";
        StateText = a.State switch
        {
            "running" => Loc.T("ai.agentRunning"),
            "waiting" => Loc.T("ai.agentWaiting"),
            "completed" => Loc.T("ai.agentCompleted"),
            "error" => Loc.T("common.error"),
            _ => Loc.T("nav.home")
        };
        StateBrush = a.State switch
        {
            "running" => "SuccessBrush",
            "waiting" => "WarningBrush",
            "error" => "DangerBrush",
            _ => "MutedBrush"
        };
        Project = a.ProjectLabel;
        DurationText = a.Duration is { } d ? $"{(int)d.TotalMinutes}m {d.Seconds}s" : "—";
        LastActivityText = a.LastActivity is { } la
            ? (DateTime.Now - la).TotalMinutes < 1 ? Loc.T("common.today") : la.ToString("H:mm")
            : "—";
    }
}
