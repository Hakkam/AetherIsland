using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using AetherIsland.Windows;

namespace AetherIsland.Portable;

/// <summary>
/// Opt-in, low-frequency foreground app sampling. Never collects titles,
/// keystrokes, browser URLs, or window contents. Stores local aggregates only.
/// </summary>
public sealed class ActivityTracker : IDisposable
{
    private readonly object _gate = new();
    private readonly ActivityDay _persisted = PortableData.Read<ActivityDay>("screen-time.json")
        ?? new ActivityDay();
    private Dictionary<string, long> _seconds = new(StringComparer.OrdinalIgnoreCase);
    private Timer? _timer;
    private int _busy;
    private int _ticks;
    private DateTime _date = DateTime.Today;

    public ActivityTracker()
    {
        if (_persisted.Date == DateTime.Today.ToString("yyyy-MM-dd"))
            _seconds = new Dictionary<string, long>(_persisted.Seconds, StringComparer.OrdinalIgnoreCase);
        if (PortableData.Settings.ScreenTimeConsent) Start();
    }

    public void Start()
    {
        lock (_gate)
        {
            if (_timer != null) return;
            _timer = new Timer(Sample, null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
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
            if (app is null || string.IsNullOrWhiteSpace(app.DisplayName)) return;
            lock (_gate)
            {
                if (_date != DateTime.Today)
                {
                    Flush();
                    _seconds = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
                    _date = DateTime.Today;
                }
                _seconds[app.DisplayName] = _seconds.GetValueOrDefault(app.DisplayName) + 10;
                if (++_ticks % 6 == 0) Flush();
            }
        }
        catch (Exception ex) { PortableData.Log("ACTIVITY", ex.Message); }
        finally { Interlocked.Exchange(ref _busy, 0); }
    }

    public List<KeyValuePair<string, long>> Snapshot()
    {
        lock (_gate)
            return _seconds.OrderByDescending(p => p.Value).ToList();
    }
    public void Clear()
    {
        lock (_gate) { _seconds.Clear(); Flush(); }
    }
    private void Flush() => PortableData.Save("screen-time.json", new ActivityDay
    {
        Date = _date.ToString("yyyy-MM-dd"),
        Seconds = new Dictionary<string, long>(_seconds)
    });
    public void Dispose() => Pause();
}

public sealed class ActivityDay
{
    public string Date { get; set; } = "";
    public Dictionary<string, long> Seconds { get; set; } = new();
}
