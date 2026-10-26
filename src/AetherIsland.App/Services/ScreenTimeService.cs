using System.Windows.Threading;
using AetherIsland.Core.Logic;
using AetherIsland.Core.Models;
using AetherIsland.Data;
using AetherIsland.Windows;

namespace AetherIsland.App.Services;

/// <summary>
/// Measures foreground application time. Only process names are recorded — never
/// window titles, keystrokes or content. Opt-in, pausable, deletable, retention-pruned.
/// </summary>
public sealed class ScreenTimeService
{
    private readonly DispatcherTimer _timer;
    private readonly ScreenTimeRepository _repo;
    private readonly AppSettings _settings;
    private ForegroundApp? _current;
    private DateTime _currentSince;
    private int _switchesToday;
    private string? _lastDay;

    public bool TrackingEnabled { get; private set; }
    public event Action? Ticked;

    public ScreenTimeService(ScreenTimeRepository repo, AppSettings settings)
    {
        _repo = repo;
        _settings = settings;
        TrackingEnabled = settings.Privacy.ScreenTimeConsent;
        _lastDay = DateTime.Now.Date.ToString("yyyy-MM-dd");
        _timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += OnTick;
    }

    public void Start()
    {
        if (TrackingEnabled) { _current = null; _timer.Start(); }
    }

    public void SetConsent(bool enabled)
    {
        TrackingEnabled = enabled;
        _settings.Privacy.ScreenTimeConsent = enabled;
        if (enabled) { _current = null; _timer.Start(); }
        else { FlushCurrent(); _current = null; _timer.Stop(); }
    }

    public void Pause() { FlushCurrent(); _current = null; _timer.Stop(); }
    public void Resume()
    {
        if (TrackingEnabled) _timer.Start();
    }

    public (string exe, string display) CurrentApp => _current is null ? ("", "") : (_current.Exe, _current.DisplayName);
    public int SwitchesToday => _switchesToday;

    private void OnTick(object? s, EventArgs e)
    {
        var day = DateTime.Now.Date.ToString("yyyy-MM-dd");
        if (day != _lastDay)
        {
            _lastDay = day;
            _switchesToday = 0;
            _repo.PruneOlderThan(_settings.Privacy.RetentionDays);
        }

        // idle detection — idle time is excluded from active usage
        var idle = ForegroundTracker.GetIdleSeconds();
        if (idle > 90)
        {
            FlushCurrent();
            _current = null;
            Ticked?.Invoke();
            return;
        }

        var app = ForegroundTracker.GetForegroundApp();
        if (app is null)
        {
            FlushCurrent();
            _current = null;
            Ticked?.Invoke();
            return;
        }

        var now = DateTime.Now;
        if (_current is null)
        {
            _current = app;
            _currentSince = now;
            _switchesToday++;
        }
        else if (!string.Equals(_current.Exe, app.Exe, StringComparison.OrdinalIgnoreCase))
        {
            FlushCurrent();
            _current = app;
            _currentSince = now;
            _switchesToday++;
        }
        Ticked?.Invoke();
    }

    private void FlushCurrent()
    {
        if (_current is null) return;
        var seconds = (int)(DateTime.Now - _currentSince).TotalSeconds;
        if (seconds > 0)
        {
            _repo.AddSeconds(
                DateTime.Now.Date.ToString("yyyy-MM-dd"),
                _current.Exe,
                _current.DisplayName,
                _current.Category,
                seconds, 0);
        }
        _currentSince = DateTime.Now;
    }

    public void FlushNow() => FlushCurrent();
}

public sealed record DayProgressInfo(double DayFraction, double? WorkFraction, TimeSpan UntilBed, string ClockText);

public static class DayProgressService
{
    public static DayProgressInfo Compute(AppSettings settings)
    {
        var now = DateTime.Now;
        var wake = DayProgressCalculator.ParseTime(settings.Day.WakeTime, new TimeSpan(7, 0, 0));
        var bed = DayProgressCalculator.ParseTime(settings.Day.BedTime, new TimeSpan(23, 30, 0));
        var workStart = DayProgressCalculator.ParseTime(settings.Day.WorkStart, new TimeSpan(9, 0, 0));
        var workEnd = DayProgressCalculator.ParseTime(settings.Day.WorkEnd, new TimeSpan(17, 0, 0));
        var clock = settings.Day.Use24Hour
            ? now.ToString("HH:mm")
            : now.ToString("h:mm tt");
        return new DayProgressInfo(
            DayProgressCalculator.DayFraction(now, wake, bed),
            DayProgressCalculator.WorkFraction(now, workStart, workEnd),
            DayProgressCalculator.RemainingUntil(now, settings.Day.BedTime),
            clock);
    }
}
