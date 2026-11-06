using System.Windows.Threading;

namespace AetherIsland.Portable;

/// <summary>
/// One shared 60-second scheduler for everything that needs minute-level
/// awareness: task reminders, wellbeing breaks and day rollovers. A single
/// cheap timer replaces several per-feature loops.
/// </summary>
public sealed class Scheduler
{
    public static readonly Scheduler Instance = new();
    public event Action? MinuteTick;

    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMinutes(1) };
    private readonly HashSet<string> _firedReminders = new(StringComparer.Ordinal);
    private DateTime _lastEyeBreak = DateTime.Now;
    private DateTime _lastStand = DateTime.Now;

    private Scheduler() => _timer.Tick += (_, _) => OnMinute();

    public void Start()
    {
        _lastEyeBreak = _lastStand = DateTime.Now;
        _timer.Start();
    }

    private void OnMinute()
    {
        var now = DateTime.Now;

        // Hydration day rollover
        var s = PortableData.Settings;
        if (s.HydrationDay != DateTime.Today)
        {
            s.HydrationDay = DateTime.Today;
            s.HydrationCups = 0;
            PortableData.SaveSettings();
        }

        // Task reminders: title may end with "15m", "2h", "14:30".
        var tasks = PortableData.Read<List<PortableTask>>("tasks.json") ?? new();
        foreach (var task in tasks)
        {
            if (task.Done || task.Trashed || task.DueAt is null) continue;
            var key = task.Id + "|" + task.DueAt.Value.Ticks;
            if (task.DueAt <= now && _firedReminders.Add(key))
                App.NotifyBalloon("Task due", task.Title);
        }

        // Wellbeing reminders (opt-in)
        if (s.WellbeingEnabled)
        {
            if ((now - _lastEyeBreak).TotalMinutes >= Math.Max(5, s.EyeBreakMinutes))
            {
                _lastEyeBreak = now;
                App.NotifyBalloon("Eye break", "Look 20 feet away for 20 seconds.");
            }
            if ((now - _lastStand).TotalMinutes >= Math.Max(15, s.StandMinutes))
            {
                _lastStand = now;
                App.NotifyBalloon("Movement", "Time to stand up and stretch.");
            }
        }

        MinuteTick?.Invoke();
    }
}
