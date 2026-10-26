using System.Windows.Threading;
using AetherIsland.Core.Models;
using AetherIsland.Data;
using AetherIsland.Integrations.AI;
using AetherIsland.Integrations.Weather;
using AetherIsland.Windows;

namespace AetherIsland.App.Services;

/// <summary>Builds AI provider adapters from settings and refreshes usage on a schedule.</summary>
public sealed class AiUsageService
{
    private readonly SettingsService _settingsStore;
    private readonly ICredentialStoreProxy _credentials;
    private readonly DispatcherTimerAdapter _timer;
    // Polling, manual refresh, and startup refresh can overlap. Only one request is allowed at a time.
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    // The only supported verified quota is currently an OpenAI monthly hard spend limit.
    // Announce each reached threshold once per calendar month, not on every poll.
    private readonly HashSet<string> _announcedThresholds = new(StringComparer.Ordinal);
    private string _alertMonth = DateTime.Now.ToString("yyyy-MM");
    public UsageAggregator Aggregator { get; } = new();
    public AgentDetector Agents { get; }

    public event Action<UsageAggregator.AlertCheck>? AlertTriggered;
    public event Action? Refreshed;
    public DateTime? LastRefresh { get; private set; }

    public interface ICredentialStoreProxy { string? GetSecret(string providerId); }
    public interface DispatcherTimerAdapter { void Start(int minutes); event Action? Tick; }

    public AiUsageService(SettingsService settingsStore, ICredentialStoreProxy credentials, DispatcherTimerAdapter timer)
    {
        _settingsStore = settingsStore;
        _credentials = credentials;
        _timer = timer;
        Agents = new AgentDetector(() => settingsStore.Load().Privacy.AgentMetadataConsent);
        Aggregator.Aggregated += () => { LastRefresh = DateTime.Now; Refreshed?.Invoke(); };
        _timer.Tick += () => _ = RefreshAsync();
        _timer.Start(settingsStore.Load().Ai.PollMinutes);
    }

    public IAiUsageProvider? BuildProvider(AiProviderSettings p)
    {
        var cfg = new ProviderConfig
        {
            ProviderId = p.ProviderId,
            AccountLabel = p.AccountLabel,
            BaseUrl = null, // Send admin credentials only to the official OpenAI endpoint.
            GetSecret = () => _credentials.GetSecret(p.ProviderId),
            ManualMonthlyBudgetUsd = p.ManualMonthlyBudgetUsd
        };
        return p.ProviderId switch
        {
            AiProviderIds.OpenAI => new OpenAiProvider(cfg),
            _ => null
        };
    }

    public async Task RefreshAsync(UsagePeriodKind period = UsagePeriodKind.BillingPeriod)
    {
        // Do not queue a second paid-provider poll while an earlier refresh is in flight.
        if (!await _refreshGate.WaitAsync(0)) return;
        try
        {
            var settings = _settingsStore.Load();
            var providers = new List<(string, string, IAiUsageProvider)>();
            foreach (var p in settings.Ai.Providers.Where(x => x.Enabled && x.ProviderId == AiProviderIds.OpenAI))
            {
                var adapter = BuildProvider(p);
                if (adapter is not null)
                    providers.Add((p.ProviderId, DisplayNameFor(p.ProviderId), adapter));
            }

            await Aggregator.RefreshAsync(providers, period);

            // The only supported verified quota is monthly. A newly started calendar
            // month must allow new alerts; repeated polls within the month must not.
            var month = DateTime.Now.ToString("yyyy-MM");
            if (!string.Equals(_alertMonth, month, StringComparison.Ordinal))
            {
                _announcedThresholds.Clear();
                _alertMonth = month;
            }

            if (settings.Ai.AlertsEnabled)
            {
                foreach (var result in Aggregator.Results)
                {
                    if (result.Snapshot is null) continue;
                    var alert = UsageAggregator.EvaluateAlerts(result.ProviderId, result.Snapshot, settings.Ai.AlertThresholdsPercent);
                    if (alert is null || settings.Ai.MutedAlerts.Contains($"{alert.ProviderId}:{alert.Threshold}"))
                        continue;

                    var alertId = $"{month}:{alert.ProviderId}:{alert.Source}:{alert.Threshold}";
                    if (_announcedThresholds.Add(alertId))
                        AlertTriggered?.Invoke(alert);
                }
            }

            // Preserve the in-memory settings object held by view models rather than
            // replacing it with a stale copy loaded before the network request.
            var live = AppServices.Settings;
            foreach (var r in Aggregator.Results)
            {
                var ps = live.Ai.Providers.FirstOrDefault(x => x.ProviderId == r.ProviderId);
                if (ps is not null) ps.LastSyncAt = r.RefreshedAt;
            }
            _settingsStore.Save(live);
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    public static string DisplayNameFor(string providerId) => providerId switch
    {
        AiProviderIds.OpenAI => "OpenAI",
        _ => providerId
    };
}

/// <summary>Weather with refresh + caching behavior around OpenMeteoService.</summary>
public sealed class WeatherService
{
    private readonly OpenMeteoService _service;
    private readonly SettingsService _settingsStore;

    public WeatherData? Current { get; private set; }
    public event Action? Updated;

    public WeatherService(SettingsService settingsStore)
    {
        _settingsStore = settingsStore;
        _service = new OpenMeteoService(imperial: settingsStore.Load().Weather.Units == "imperial");
    }

    public async Task RefreshAsync()
    {
        var w = _settingsStore.Load().Weather;
        if (w.Latitude is null || w.Longitude is null) return;
        Current = await _service.GetAsync(w.Latitude.Value, w.Longitude.Value,
            string.IsNullOrEmpty(w.LocationName) ? $"{w.Latitude:0.0#}, {w.Longitude:0.0#}" : w.LocationName);
        Updated?.Invoke();
    }
}

/// <summary>Wellbeing reminders: eye breaks, movement, hydration tracking with persistence.</summary>
public sealed class HealthService
{
    private readonly DispatcherTimer _timer;
    private readonly HealthLogRepository _health;
    private readonly SettingsService _settingsStore;
    private DateTime _lastEye = DateTime.Now;
    private DateTime _lastMove = DateTime.Now;

    public event Action<string>? ReminderDue; // "eye" | "move"
    public event Action? HydrationChanged;

    public HealthService(HealthLogRepository health, SettingsService settingsStore)
    {
        _health = health;
        _settingsStore = settingsStore;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        _timer.Tick += OnTick;
    }

    public void Start() => _timer.Start();

    private void OnTick(object? s, EventArgs e)
    {
        var st = _settingsStore.Load().Health;
        if (!st.RemindersEnabled) return;
        if ((DateTime.Now - _lastEye).TotalMinutes >= st.EyeBreakMinutes) { _lastEye = DateTime.Now; ReminderDue?.Invoke("eye"); }
        if ((DateTime.Now - _lastMove).TotalMinutes >= st.MoveMinutes) { _lastMove = DateTime.Now; ReminderDue?.Invoke("move"); }
    }

    public int HydrationToday => _health.Get("hydration");
    public void AddHydration(int ml)
    {
        _health.Set("hydration", HydrationToday + ml);
        HydrationChanged?.Invoke();
    }
    public void ResetHydration()
    {
        _health.Set("hydration", 0);
        HydrationChanged?.Invoke();
    }
}


