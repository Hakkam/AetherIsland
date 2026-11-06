using System.Windows.Threading;

namespace AetherIsland.Portable;

/// <summary>
/// Shared focus-timer state so the focus module, collapsed pill and tray show
/// one consistent session. The DispatcherTimer runs only while a session is
/// active — zero idle cost.
/// </summary>
public sealed class FocusService
{
    public static readonly FocusService Instance = new();

    public string Mode { get; private set; } = "pomodoro";   // pomodoro | break | custom | stopwatch
    public int SecondsLeft { get; private set; } = 25 * 60;
    public bool Running { get; private set; }
    public bool StopwatchMode { get; private set; }
    public int CompletedPomodoros { get; private set; }
    public event Action? StateChanged;

    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };

    private FocusService() => _timer.Tick += (_, _) => Tick();

    public void Configure(string mode, int minutes)
    {
        Mode = mode;
        StopwatchMode = mode == "stopwatch";
        SecondsLeft = StopwatchMode ? 0 : minutes * 60;
        Running = false;
        _timer.Stop();
        StateChanged?.Invoke();
    }

    public void StartPause()
    {
        Running = !Running;
        if (Running) _timer.Start(); else _timer.Stop();
        StateChanged?.Invoke();
    }

    public void Reset(int minutes)
    {
        Running = false;
        _timer.Stop();
        SecondsLeft = StopwatchMode ? 0 : minutes * 60;
        StateChanged?.Invoke();
    }

    private void Tick()
    {
        if (StopwatchMode) SecondsLeft++;
        else
        {
            SecondsLeft--;
            if (SecondsLeft > 0) { StateChanged?.Invoke(); return; }
            _timer.Stop();
            Running = false;
            if (Mode == "pomodoro") CompletedPomodoros++;
            App.NotifyBalloon("Aether Island Focus",
                StopwatchMode ? "Stopwatch still running." : Mode == "break" ? "Break finished — back to work." : "Pomodoro finished. Take a break.");
        }
        StateChanged?.Invoke();
    }

    public string Clock => $"{SecondsLeft / 60:00}:{SecondsLeft % 60:00}";
}
