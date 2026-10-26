using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AetherIsland.Core;
using AetherIsland.Core.Logic;
using AetherIsland.Core.Models;
using AetherIsland.Integrations.Weather;
using AetherIsland.Windows;

namespace AetherIsland.App.ViewModels;

public partial class HomeViewModel : ObservableObject, ILifecycleAware
{
    private readonly ShellViewModel _shell;

    [ObservableProperty] private string _greeting = "";
    [ObservableProperty] private string _weatherSummary = "";
    [ObservableProperty] private string _weatherGlyph = "\uE9D6";
    [ObservableProperty] private string _nextEventText = "";
    [ObservableProperty] private bool _hasNextEvent;
    [ObservableProperty] private string _mediaText = "";
    [ObservableProperty] private bool _hasMedia;
    [ObservableProperty] private string _focusText = "";
    [ObservableProperty] private bool _hasFocus;
    [ObservableProperty] private string _aiSummary = "";
    [ObservableProperty] private ObservableCollection<TaskItem> recentTasks = new();
    [ObservableProperty] private ObservableCollection<HomeQuickAction> quickActions = new();

    public string WeatherCardTitle => Loc.T("dash.weather");
    public string TogglesCardTitle => Loc.T("dash.toggles");
    public string AppsCardTitle => Loc.T("dash.apps");
    public string DayProgressTitle => Loc.T("dash.dayprogress");
    public string NoLocationText => Loc.T("dash.noLocation");
    public string SetLocationLabel => Loc.T("dash.configureLocation");
    public string ElapsedLabel => Loc.T("dayprogress.elapsed");
    public string AddAppLabel => Loc.T("dash.addApp");
    public string DateShell => DateTime.Now.ToString("ddd, d MMMM");
    public string WeatherTitle => Loc.T("nav.weather");
    public string NextEventTitle => Loc.T("home.nextEvent");
    public string AiTitle => Loc.T("home.aiSummary");
    public string QuickActionsTitle => Loc.T("home.quickActions");
    public string NowPlayingTitle => Loc.T("home.nowPlaying");
    public string FocusTitle => Loc.T("home.focusActive");
    public string RecentTasksTitle => Loc.T("home.recentTasks");

    public HomeViewModel(ShellViewModel shell) => _shell = shell;

    public void OnShown() => Refresh();

    public void OnHidden() { }

    public void Refresh()
    {
        var hour = DateTime.Now.Hour;
        Greeting = Loc.T(hour switch
        {
            >= 5 and < 12 => "home.greetingMorning",
            >= 12 and < 18 => "home.greetingAfternoon",
            >= 18 and < 23 => "home.greetingEvening",
            _ => "home.greetingNight"
        }) + ", " + Environment.UserName;

        var w = AppServices.Weather.Current;
        if (w?.TempC is not null)
        {
            var units = AppServices.Settings.Weather.Units == "imperial";
            var temp = units && w.TempC is not null ? w.TempC * 9 / 5 + 32 : w.TempC;
            WeatherSummary = $"{Math.Round(temp ?? 0)}° · {WeatherCodes.Describe(w.Code, w.IsDay).Desc} · {w.LocationName}";
            WeatherGlyph = WeatherCodes.Describe(w.Code, w.IsDay).Glyph;
        }
        else
        {
            WeatherSummary = Loc.T("dash.noLocation");
            WeatherGlyph = "\uE9D6";
        }

        _ = LoadNextEventAsync();
        LoadMedia();
        LoadFocus();
        LoadTasks();
        LoadAi();
        LoadQuickActions();
    }

    private async Task LoadNextEventAsync()
    {
        try
        {
            var urls = AppServices.Settings.Calendar.IcsUrls;
            if (urls.Count == 0) { NextEventText = ""; HasNextEvent = false; return; }
            var events = await AppServices.CalendarProvider.LoadAsync(urls);
            var next = events.FirstOrDefault(e => e.End > DateTime.Now);
            if (next is null) { HasNextEvent = false; NextEventText = Loc.T("home.noEvents"); return; }
            NextEventText = next.AllDay
                ? next.Title
                : $"{next.Start:H:mm} · {next.Title}";
            HasNextEvent = true;
        }
        catch { HasNextEvent = false; }
    }

    private void LoadMedia()
    {
        var info = AppServices.Media.LastSession;
        if (info is { IsValid: true })
        {
            MediaText = $"{info.Title} — {info.Artist}";
            HasMedia = true;
        }
        else
        {
            MediaText = Loc.T("home.noMedia");
            HasMedia = false;
        }
    }

    private void LoadFocus()
    {
        if (_shell.Focus.IsRunning)
        {
            FocusText = $"{Loc.T("home.focusActive")} · {_shell.Focus.RemainingText}";
            HasFocus = true;
        }
        else HasFocus = false;
    }

    private void LoadTasks()
    {
        try
        {
            var repo = new AetherIsland.Data.TaskRepository(AppServices.Database);
            var tasks = repo.GetAll().Where(t => !t.Completed).Take(5).ToList();
            RecentTasks.Clear();
            foreach (var t in tasks) RecentTasks.Add(t);
        }
        catch { }
    }

    private void LoadAi()
    {
        var totals = AppServices.Ai.Aggregator.OfficialTotals;
        var local = AppServices.Ai.Aggregator.LocalTotals;
        var connected = AppServices.Ai.Aggregator.Results.Count(r => r.Status.Connected);
        if (connected == 0)
        {
            AiSummary = Loc.T("home.noAi");
        }
        else
        {
            var official = AppServices.Ai.Aggregator.OfficialTotals;
            var lT = AppServices.Ai.Aggregator.LocalTotals;
            var parts = new List<string> { Loc.TF("ai.connectedCount", connected) };
            if (official.input is > 0) parts.Add($"{QuotaMath.FormatTokens(official.input)} {Loc.T("ai.input").ToLowerInvariant()}");
            if (lT.input is > 0) parts.Add($"{QuotaMath.FormatTokens(lT.input)} {Loc.T("ai.source.local").ToLowerInvariant()}");
            AiSummary = string.Join(" · ", parts);
        }
    }

    private void LoadQuickActions()
    {
        QuickActions.Clear();
        QuickActions.Add(new HomeQuickAction(Loc.T("util.screenshot"), "\uE7A8", () => _ = TakeScreenshotAsync()));
        QuickActions.Add(new HomeQuickAction(Loc.T("util.lock"), "\uE72E", () => PowerHelper.LockWorkstation()));
        QuickActions.Add(new HomeQuickAction(Loc.T("util.taskManager"), "\uE9D5", () =>
            Process.Start(new ProcessStartInfo("taskmgr.exe") { UseShellExecute = true })));
        QuickActions.Add(new HomeQuickAction(Loc.T("common.settings"), "\uE713", () => Windows.SettingsWindow.ShowOnce()));
    }

    private static async Task TakeScreenshotAsync()
    {
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            $"AetherIsland_{DateTime.Now:yyyyMMdd_HHmmss}.png");
        await System.Threading.Tasks.Task.Run(() => UtilitiesViewModel.CaptureScreen(path));
        AppServices.Notify.ShowIslandContext("\uE7A8", Loc.T("util.screenshotSaved"));
    }
}

public partial class HomeQuickAction : ObservableObject
{
    public string Label { get; }
    public string Glyph { get; }
    private readonly Action _run;

    public HomeQuickAction(string label, string glyph, Action run)
    {
        Label = label; Glyph = glyph; _run = run;
        RunCommand = new RelayCommand(_run);
    }

    public RelayCommand RunCommand { get; }
}

public partial class DashboardViewModel : ObservableObject, ILifecycleAware
{
    private readonly ShellViewModel _shell;
    private readonly DispatcherTimer _timer;

    [ObservableProperty] private string _weatherTemp = "--°";
    [ObservableProperty] private string _weatherDesc = "";
    [ObservableProperty] private string _weatherGlyph = "\uE9D6";
    [ObservableProperty] private string _weatherHilo = "";
    private bool _weatherLoaded;
    public bool WeatherLoaded
    {
        get => _weatherLoaded;
        set { _weatherLoaded = value; OnPropertyChanged(nameof(WeatherLoaded)); OnPropertyChanged(nameof(WeatherNotLoaded)); }
    }
    public bool WeatherNotLoaded => !WeatherLoaded;
    [ObservableProperty] private string _locationName = "";
    [ObservableProperty] private double _dayFraction;
    [ObservableProperty] private string _dayPercentText = "0%";
    [ObservableProperty] private string _bedtimeText = "";
    [ObservableProperty] private string _workdayText = "";
    [ObservableProperty] private ObservableCollection<ToggleTileVm> toggles = new();
    [ObservableProperty] private ObservableCollection<AppTileVm> apps = new();
    [ObservableProperty] private bool _hasApps = true;

    public DashboardViewModel(ShellViewModel shell)
    {
        _shell = shell;
        Instance = this;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _timer.Tick += (_, _) => RefreshWeather();
        BuildToggles();
    }

    public void OnShown()
    {
        RefreshWeather();
        RefreshDayProgress();
        RefreshApps();
        _timer.Start();
    }

    public void OnHidden() => _timer.Stop();

    private void BuildToggles()
    {
        Toggles.Clear();
        Toggles.Add(new ToggleTileVm(Loc.T("toggle.wifi"), "\uE701", true,
            get: () => NetworkViewModel.WifiConnected, set: _ => { OpenNetworkSettings(); }));
        Toggles.Add(new ToggleTileVm(Loc.T("toggle.bluetooth"), "\uE702", true,
            get: () => NetworkViewModel.BluetoothOn, set: _ => { OpenBluetoothSettings(); }));
        Toggles.Add(new ToggleTileVm(Loc.T("toggle.awake"), "\uE708", true,
            get: () => UtilitiesViewModel.KeepAwakeActive, set: v => UtilitiesViewModel.SetKeepAwake(v)));
        Toggles.Add(new ToggleTileVm(Loc.T("toggle.theme"), "\uE793", false,
            get: () => !AppServices.Theme.IsDark, set: _ => ToggleTheme()));
        Toggles.Add(new ToggleTileVm(Loc.T("toggle.mic"), "\uE720", true,
            get: () => UtilitiesViewModel.MicMuted, set: _ => UtilitiesViewModel.ToggleMic()));
        Toggles.Add(new ToggleTileVm(Loc.T("toggle.speaker"), "\uE767", true,
            get: () => UtilitiesViewModel.SpeakerMuted, set: _ => UtilitiesViewModel.ToggleSpeaker()));
        Toggles.Add(new ToggleTileVm(Loc.T("toggle.screenshot"), "\uE7A8", false,
            get: () => false, set: ignored => { _ = UtilitiesViewModel.TakeScreenshotAsync(); }));
        Toggles.Add(new ToggleTileVm(Loc.T("toggle.lock"), "\uE72E", false,
            get: () => false, set: _ => PowerHelper.LockWorkstation()));
    }

    private static void OpenNetworkSettings() =>
        Process.Start(new ProcessStartInfo("ms-settings:network-wifi") { UseShellExecute = true });

    private static void OpenBluetoothSettings() =>
        Process.Start(new ProcessStartInfo("ms-settings:bluetooth") { UseShellExecute = true });

    private static void ToggleTheme()
    {
        var s = AppServices.Settings;
        s.Appearance.Theme = AppServices.Theme.IsDark ? "light" : "dark";
        AppServices.SettingsStore.Save(s);
        AppServices.Theme.Apply();
        foreach (var t in Instance?.Toggles ?? new ObservableCollection<ToggleTileVm>()) t.RaiseGet();
    }

    internal static DashboardViewModel? Instance { get; private set; }
    public event Action? LayoutChanged;
    public void NotifyLayoutChanged() => LayoutChanged?.Invoke();

    public void RefreshWeather()
    {
        var w = AppServices.Weather.Current;
        LocationName = AppServices.Settings.Weather.LocationName;
        if (w is null) { WeatherLoaded = false; _ = AppServices.Weather.RefreshAsync(); return; }
        var units = AppServices.Settings.Weather.Units == "imperial";
        double? Temp(double? c) => units && c is not null ? c * 9 / 5 + 32 : c;
        var suffix = units ? "°F" : "°C";
        WeatherTemp = w.TempC is null ? "--" : $"{Math.Round(Temp(w.TempC) ?? 0)}{suffix}";
        WeatherDesc = WeatherCodes.Describe(w.Code, w.IsDay).Desc;
        WeatherGlyph = WeatherCodes.Describe(w.Code, w.IsDay).Glyph;
        var today = w.Daily.FirstOrDefault();
        WeatherHilo = today is null ? "" : $"H {Math.Round(Temp(today.MaxC) ?? 0)}°  L {Math.Round(Temp(today.MinC) ?? 0)}°";
        WeatherLoaded = true;
    }

    public void RefreshDayProgress()
    {
        var info = DayProgressService.Compute(AppServices.Settings);
        DayFraction = Math.Clamp(info.DayFraction, 0, 1);
        DayPercentText = $"{Math.Round(DayFraction * 100)}%";
        var bed = info.UntilBed;
        BedtimeText = Loc.TF("dayprogress.remaining", $"{(int)bed.TotalHours}h {bed.Minutes}m");
        WorkdayText = info.WorkFraction is { } wf
            ? $"{Loc.T("dayprogress.workday")} · {Math.Round(wf * 100)}%"
            : Loc.T("dayprogress.workday");
    }

    public void RefreshApps()
    {
        Apps.Clear();
        foreach (var app in AppServices.Launcher.Load().Take(8))
            Apps.Add(new AppTileVm(app.Name, app.Path));
        HasApps = Apps.Count > 0;
    }

    [RelayCommand]
    private void LaunchApp(AppTileVm app) => app.Launch();

    [RelayCommand]
    private void OpenWeatherModule() => _shell.NavigateTo("weather");

    [RelayCommand]
    private void AddApp()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = Loc.T("dash.addApp"),
            Filter = "Applications (*.exe;*.lnk)|*.exe;*.lnk|All files (*.*)|*.*"
        };
        if (dlg.ShowDialog() == true)
        {
            var list = AppServices.Launcher.Load();
            list.Add(new AppShortcut { Name = Path.GetFileNameWithoutExtension(dlg.FileName), Path = dlg.FileName });
            AppServices.Launcher.Save(list);
            RefreshApps();
        }
    }
}

public partial class ToggleTileVm : ObservableObject
{
    private readonly Func<bool> _get;
    private readonly Action<bool> _set;

    public string Label { get; }
    public string Glyph { get; }
    public bool IsToggle { get; }

    public ToggleTileVm(string label, string glyph, bool isToggle, Func<bool> get, Action<bool> set)
    {
        Label = label; Glyph = glyph; IsToggle = isToggle; _get = get; _set = set;
    }

    public bool Value => _get();

    [RelayCommand]
    private void Toggle() => _set(!Value);

    public void RaiseGet() => OnPropertyChanged(nameof(Value));
}

public partial class AppTileVm : ObservableObject
{
    public string Name { get; }
    public string Path { get; }
    public string Initial => string.IsNullOrEmpty(Name) ? "?" : Name.Substring(0, 1).ToUpperInvariant();

    public AppTileVm(string name, string path) { Name = name; Path = path; }

    public void Launch()
    {
        try
        {
            if (Path.EndsWith("://") || Path.EndsWith(":"))
                Process.Start(new ProcessStartInfo(Path) { UseShellExecute = true });
            else
                Process.Start(new ProcessStartInfo(Path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            AppServices.Notify.ShowBalloon("Aether Island", ex.Message);
        }
    }
}
