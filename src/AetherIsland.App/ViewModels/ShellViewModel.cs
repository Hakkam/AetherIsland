using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AetherIsland.Core;
using AetherIsland.Core.Models;

namespace AetherIsland.App.ViewModels;

public interface ILifecycleAware
{
    void OnShown();
    void OnHidden();
}

public partial class NavItemVm : ObservableObject
{
    public string Id { get; init; } = "";
    public string Label { get; set; } = "";
    public string Glyph { get; init; } = "";
    public bool IsAiAccent { get; init; }
    public ObservableObject ViewModel { get; init; } = null!;

    [ObservableProperty] private bool _isSelected;
}

public partial class ShellViewModel : ObservableObject
{
    private readonly DispatcherTimer _clock;
    private readonly DispatcherTimer _contextTimer;

    public ObservableCollection<NavItemVm> NavItems { get; } = new();
    public HomeViewModel Home { get; }
    public DashboardViewModel Dashboard { get; }
    public FileShelfViewModel FileShelf { get; }
    public TasksViewModel Tasks { get; }
    public NotesViewModel Notes { get; }
    public FocusViewModel Focus { get; }
    public MediaViewModel Media { get; }
    public CalendarViewModel Calendar { get; }
    public WeatherViewModel Weather { get; }
    public ProductivityViewModel Productivity { get; }
    public NetworkViewModel Network { get; }
    public SystemViewModel SystemInfo { get; }
    public WellbeingViewModel Wellbeing { get; }
    public AiCenterViewModel AiCenter { get; }
    public UtilitiesViewModel Utilities { get; }

    [ObservableProperty] private ObservableObject? _current;
    [ObservableProperty] private NavItemVm? _currentNav;
    [ObservableProperty] private string _clockText = DateTime.Now.ToString("HH:mm");
    [ObservableProperty] private string _dateText = DateTime.Now.ToString("ddd, d MMMM");
    [ObservableProperty] private string _pillStatusText = "";
    [ObservableProperty] private string _pillStatusGlyph = "";
    [ObservableProperty] private string _aiStatusText = "";
    [ObservableProperty] private double _aiUsageFraction = 0;
    [ObservableProperty] private bool _hasContextEvent;
    [ObservableProperty] private bool _focusActive;
    [ObservableProperty] private string _pinLabel = Loc.T("tray.pin");
    [ObservableProperty] private string _settingsLabel = Loc.T("common.settings");

    public ShellViewModel()
    {
        Home = new HomeViewModel(this);
        Dashboard = new DashboardViewModel(this);
        FileShelf = new FileShelfViewModel();
        Tasks = new TasksViewModel();
        Notes = new NotesViewModel();
        Focus = new FocusViewModel();
        Media = new MediaViewModel();
        Calendar = new CalendarViewModel();
        Weather = new WeatherViewModel();
        Productivity = new ProductivityViewModel();
        Network = new NetworkViewModel();
        SystemInfo = new SystemViewModel();
        Wellbeing = new WellbeingViewModel();
        AiCenter = new AiCenterViewModel();
        Utilities = new UtilitiesViewModel();

        BuildNav();
        Current = Dashboard;
        CurrentNav = NavItems.FirstOrDefault(n => n.Id == "dashboard") ?? NavItems.First();

        _clock = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clock.Tick += (_, _) => OnClockTick();
        _clock.Start();

        _contextTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(7) };
        _contextTimer.Tick += (_, _) =>
        {
            HasContextEvent = false;
            _contextTimer.Stop();
        };

        AppServices.Notify.IslandContext += e =>
        {
            Dispatcher.CurrentDispatcher.Invoke(() =>
            {
                PillStatusGlyph = e.Icon;
                PillStatusText = e.Message;
                HasContextEvent = true;
                _contextTimer.Stop();
                _contextTimer.Start();
            });
        };

        AppServices.Ai.Refreshed += () => Dispatcher.CurrentDispatcher.Invoke(UpdateAiPill);
        AppServices.Media.Updated += info => Dispatcher.CurrentDispatcher.Invoke(() =>
        {
            if (info is { IsValid: true } && info.PlaybackState == "playing" && !HasContextEvent)
            {
                PillStatusGlyph = "\uE768";
                PillStatusText = $"{info.Title} — {info.Artist}".Trim(' ', '—');
                if (PillStatusText.Length > 34) PillStatusText = PillStatusText[..33] + "…";
            }
            Media.Apply(info);
        });

        UpdateAiPill();
    }

    public void RebuildNavVisibility()
    {
        BuildNav();
        var current = CurrentNav?.Id ?? "dashboard";
        CurrentNav = NavItems.FirstOrDefault(n => n.Id == current) ?? NavItems.FirstOrDefault();
        if (CurrentNav is not null) NavigateTo(CurrentNav.Id);
    }

    private void BuildNav()
    {
        var hidden = AppServices.Settings.General.HiddenModules ?? new();
        var order = AppServices.Settings.General.ModuleOrder ?? new();
        var all = new List<NavItemVm>
        {
            new() { Id = "home", Label = Loc.T("nav.home"), Glyph = "\uE80F", ViewModel = Home },
            new() { Id = "dashboard", Label = Loc.T("nav.dashboard"), Glyph = "\uE9D9", ViewModel = Dashboard },
            new() { Id = "fileshelf", Label = Loc.T("nav.fileshelf"), Glyph = "\uE8B7", ViewModel = FileShelf },
            new() { Id = "tasks", Label = Loc.T("nav.tasks"), Glyph = "\uE700", ViewModel = Tasks },
            new() { Id = "notes", Label = Loc.T("nav.notes"), Glyph = "\uE70B", ViewModel = Notes },
            new() { Id = "focus", Label = Loc.T("nav.focus"), Glyph = "\uE9D5", ViewModel = Focus },
            new() { Id = "media", Label = Loc.T("nav.media"), Glyph = "\uE768", ViewModel = Media },
            new() { Id = "calendar", Label = Loc.T("nav.calendar"), Glyph = "\uE787", ViewModel = Calendar },
            new() { Id = "weather", Label = Loc.T("nav.weather"), Glyph = "\uE9D6", ViewModel = Weather },
            new() { Id = "productivity", Label = Loc.T("nav.productivity"), Glyph = "\uE9D2", ViewModel = Productivity },
            new() { Id = "network", Label = Loc.T("nav.network"), Glyph = "\uE701", ViewModel = Network },
            new() { Id = "system", Label = Loc.T("nav.system"), Glyph = "\uE7F8", ViewModel = SystemInfo },
            new() { Id = "wellbeing", Label = Loc.T("nav.wellbeing"), Glyph = "\uE95E", ViewModel = Wellbeing },
            new() { Id = "ai", Label = Loc.T("nav.ai"), Glyph = "\uE653", IsAiAccent = true, ViewModel = AiCenter },
            new() { Id = "utilities", Label = Loc.T("nav.utilities"), Glyph = "\uE90F", ViewModel = Utilities },
        };

        var hiddenSet = hidden.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var items = all.Where(n => !hiddenSet.Contains(n.Id)).ToList();
        if (order.Count > 0)
        {
            var idx = 0;
            items = items.OrderBy(n =>
            {
                var i = order.IndexOf(n.Id);
                return i >= 0 ? i : 100 + idx++;
            }).ToList();
        }

        // A user can hide every module in Settings; keep the shell recoverable
        // rather than throwing on NavItems.First() at the next application start.
        if (items.Count == 0)
            items.Add(all.First(n => n.Id == "dashboard"));

        NavItems.Clear();
        foreach (var item in items) NavItems.Add(item);
    }

    [RelayCommand]
    public void NavigateTo(string id)
    {
        var item = NavItems.FirstOrDefault(n => n.Id == id);
        if (item is null) return;
        if (Current is ILifecycleAware la) la.OnHidden();
        foreach (var n in NavItems) n.IsSelected = n == item;
        CurrentNav = item;
        Current = item.ViewModel;
        if (item.ViewModel is ILifecycleAware la2) la2.OnShown();
    }

    private void OnClockTick()
    {
        var now = DateTime.Now;
        ClockText = AppServices.Settings.Day.Use24Hour ? now.ToString("HH:mm") : now.ToString("h:mm tt");
        DateText = now.ToString("ddd, d MMMM");

        FocusActive = Focus.IsRunning;
        if (Focus.IsRunning)
        {
            PillStatusGlyph = "\uE9D5";
            PillStatusText = Loc.TF("focus.timeLeft", Focus.RemainingText);
        }
        else if (!HasContextEvent)
        {
            var info = AppServices.Media.LastSession;
            if (info is { IsValid: true, PlaybackState: "playing" })
            {
                PillStatusGlyph = "\uE768";
                PillStatusText = $"{info.Title} — {info.Artist}".Trim(' ', '—');
                if (PillStatusText.Length > 34) PillStatusText = PillStatusText[..33] + "…";
            }
            else
            {
                PillStatusGlyph = "";
                PillStatusText = "";
            }
        }
    }

    private void UpdateAiPill()
    {
        var quota = AppServices.Ai.Aggregator.Results
            .SelectMany(r => r.Snapshot?.QuotaWindows ?? new List<QuotaWindow>())
            .FirstOrDefault(q => q.Source == UsageSource.VerifiedOfficial && q.Limit is > 0 && q.Used is >= 0);
        if (quota is not null)
        {
            AiStatusText = Loc.T("ai.title");
            AiUsageFraction = Math.Clamp(quota.Used!.Value / quota.Limit!.Value, 0, 1);
        }
        else
        {
            AiStatusText = "";
            AiUsageFraction = 0;
        }
    }
}
