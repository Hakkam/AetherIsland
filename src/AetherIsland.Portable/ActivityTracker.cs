using System.Collections.Generic;
using System.Linq;
using System.Threading;
using AetherIsland.Windows;

namespace AetherIsland.Portable;

/// <summary>
/// Opt-in, low-frequency foreground app sampling (~10 s). Records only exe
/// names, coarse per-app seconds and app-switch counts — never titles,
/// keystrokes, URLs or window contents. Keeps a rolling 7-day local history.
/// </summary>
public sealed class ActivityTracker : IDisposable
{
    private const int HistoryDays = 7;

    private readonly object _gate = new();
    private readonly List<ActivityDay> _history = PortableData.Read<List<ActivityDay>>("screen-time.json") ?? new();
    private Dictionary<string, long> _seconds = new(StringComparer.OrdinalIgnoreCase);
    private int _switches;
    private string? _currentExe;
    private Timer? _timer;
    private int _busy;
    private int _ticks;
    private DateTime _date = DateTime.Today;

    public ActivityTracker()
    {
        lock (_gate) LoadToday();
        if (PortableData.Settings.ScreenTimeConsent) Start();
    }

    private void LoadToday()
    {
        var todayKey = DateTime.Today.ToString("yyyy-MM-dd");
        var existing = _history.FirstOrDefault(d => d.Date == todayKey);
        if (existing != null)
        {
            _seconds = new Dictionary<string, long>(existing.Seconds, StringComparer.OrdinalIgnoreCase);
            _switches = existing.Switches;
        }
        else
        {
            _history.Add(new ActivityDay { Date = todayKey });
        }
        Trim();
    }

    private void Trim()
    {
        while (_history.Count > HistoryDays)
            _history.RemoveAt(0);
    }

    public void Start()
    {
        lock (_gate)
        {
            if (_timer != null) return;
            _timer = new Timer(Sample, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10));
        }
    }

    public void Pause()
    {
        lock (_gate)
        {
            _timer?.Dispose();
            _timer = null;
            Flush();
        }
    }

    private void Sample(object? _)
    {
        if (Interlocked.Exchange(ref _busy, 1) != 0) return;
        try
        {
            if (!PortableData.Settings.ScreenTimeConsent || ForegroundTracker.GetIdleSeconds() >= 300)
                return;
            var app = ForegroundTracker.GetForegroundApp();
            if (app is null || string.IsNullOrWhiteSpace(app.Exe)) return;
            lock (_gate)
            {
                if (_date != DateTime.Today)
                {
                    Flush();
                    _seconds = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
                    _switches = 0;
                    _currentExe = app.Exe;
                    _date = DateTime.Today;
                    _history.Add(new ActivityDay { Date = _date.ToString("yyyy-MM-dd") });
                    Trim();
                }
                if (!string.Equals(_currentExe, app.Exe, StringComparison.OrdinalIgnoreCase))
                {
                    _currentExe = app.Exe;
                    _switches++;
                }
                _seconds[app.Exe] = _seconds.GetValueOrDefault(app.Exe) + 10;
                if (++_ticks % 6 == 0) Flush();
            }
        }
        catch (Exception ex) { PortableData.Log("ACTIVITY", ex.Message); }
        finally { Interlocked.Exchange(ref _busy, 0); }
    }

    /// <summary>Today's usage, sorted descending. Keys are exe names.</summary>
    public List<KeyValuePair<string, long>> Today()
    {
        lock (_gate)
            return _seconds.OrderByDescending(p => p.Value).ToList();
    }

    /// <summary>Aggregated usage across the retained history window.</summary>
    public List<KeyValuePair<string, long>> Last7Days()
    {
        lock (_gate)
        {
            var totals = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            foreach (var day in _history)
                foreach (var (exe, seconds) in day.Seconds)
                    totals[exe] = totals.GetValueOrDefault(exe) + seconds;
            return totals.OrderByDescending(p => p.Value).ToList();
        }
    }

    public (int switches, string? currentApp) SwitchInfo()
    {
        lock (_gate) return (_switches, _currentExe is null ? null : ForegroundTracker.FriendlyName(_currentExe));
    }

    /// <summary>Per-category totals in seconds for a given series.</summary>
    public static Dictionary<string, long> CategoryTotals(IEnumerable<KeyValuePair<string, long>> series)
    {
        var totals = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var (exe, seconds) in series)
        {
            var category = ForegroundTracker.Classify(exe);
            totals[category] = totals.GetValueOrDefault(category) + seconds;
        }
        return totals;
    }

    public void Clear()
    {
        lock (_gate)
        {
            _seconds.Clear();
            _switches = 0;
            _history.Clear();
            _history.Add(new ActivityDay { Date = DateTime.Today.ToString("yyyy-MM-dd") });
            Flush();
        }
    }

    private void Flush()
    {
        var todayKey = _date.ToString("yyyy-MM-dd");
        var today = _history.FirstOrDefault(d => d.Date == todayKey);
        if (today is null) { today = new ActivityDay { Date = todayKey }; _history.Add(today); Trim(); }
        today.Seconds = new Dictionary<string, long>(_seconds);
        today.Switches = _switches;
        PortableData.Save("screen-time.json", _history);
    }

    public void Dispose() => Pause();
}

public sealed class ActivityDay
{
    public string Date { get; set; } = "";
    public Dictionary<string, long> Seconds { get; set; } = new();
    public int Switches { get; set; }
}
