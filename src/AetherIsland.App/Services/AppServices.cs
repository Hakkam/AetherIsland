using AetherIsland.Core.Models;
using AetherIsland.Core.Storage;
using AetherIsland.Data;
using AetherIsland.Integrations.Calendar;
using AetherIsland.Integrations.Media;
using AetherIsland.Windows;

namespace AetherIsland.App.Services;

/// <summary>Application-wide service composition (built once at startup).</summary>
public static class AppServices
{
    public static AppSettings Settings { get; private set; } = new();
    public static SettingsService SettingsStore { get; private set; } = null!;
    public static ThemeService Theme { get; private set; } = null!;
    public static HotkeyService Hotkeys { get; private set; } = null!;
    public static TrayIconService Tray { get; private set; } = null!;
    public static NotifyService Notify { get; private set; } = null!;
    public static Db Database { get; private set; } = null!;
    public static DpapiCredentialStore CredentialStore { get; private set; } = null!;
    public static ScreenTimeService ScreenTime { get; private set; } = null!;
    public static WeatherService Weather { get; private set; } = null!;
    public static AiUsageService Ai { get; private set; } = null!;
    public static HealthService Health { get; private set; } = null!;
    public static SmtcMediaWatcher Media { get; private set; } = null!;
    public static IcsCalendarProvider CalendarProvider { get; private set; } = null!;
    public static OAuthCalendarProvider OAuthCalendar { get; private set; } = null!;
    public static LauncherStore Launcher { get; private set; } = null!;

    private static bool _built;

    public static void Build()
    {
        if (_built) return;
        _built = true;

        SettingsStore = new SettingsService();
        Settings = SettingsStore.Load();
        CredentialStore = new DpapiCredentialStore();
        Database = new Db();
        Theme = new ThemeService(Settings);
        Hotkeys = new HotkeyService();
        Tray = new TrayIconService();
        Notify = new NotifyService(Tray);
        ScreenTime = new ScreenTimeService(new ScreenTimeRepository(Database), Settings);
        Weather = new WeatherService(SettingsStore);
        Ai = new AiUsageService(SettingsStore, new CredentialProxy(CredentialStore), new PollTimer());
        // Surface verified quota warnings to the user instead of calculating alerts
        // without ever displaying them. The AI service performs deduplication.
        Ai.AlertTriggered += alert =>
        {
            var message = $"OpenAI API monthly spend: {alert.Threshold}% of the verified limit reached";
            if (Settings.General.ToastNotifications)
                Notify.ShowBalloon("Aether Island · AI Usage", message);
            Notify.ShowIslandContext("\uE7BA", message);
        };
        Health = new HealthService(new HealthLogRepository(Database), SettingsStore);
        Media = new SmtcMediaWatcher();
        CalendarProvider = new IcsCalendarProvider();
        OAuthCalendar = new OAuthCalendarProvider(CredentialStore.GetSecret, CredentialStore.SetSecret, CredentialStore.RemoveSecret);
        Launcher = new LauncherStore();
    }

    private sealed class CredentialProxy(DpapiCredentialStore store) : AiUsageService.ICredentialStoreProxy
    {
        public string? GetSecret(string providerId) => store.GetSecret(providerId);
    }

    private sealed class PollTimer : AiUsageService.DispatcherTimerAdapter
    {
        private readonly System.Windows.Threading.DispatcherTimer _timer = new()
        {
            Interval = TimeSpan.FromMinutes(15)
        };

        public event Action? Tick;

        public PollTimer()
        {
            _timer.Tick += (_, _) => Tick?.Invoke();
        }

        public void Start(int minutes)
        {
            _timer.Interval = TimeSpan.FromMinutes(Math.Clamp(minutes, 5, 240));
            _timer.Start();
        }
    }
}

