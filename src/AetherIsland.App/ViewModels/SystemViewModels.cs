using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AetherIsland.App.Services;
using AetherIsland.Core;
using AetherIsland.Data;
using AetherIsland.Windows;
using DiskStore = AetherIsland.Windows.Disks;

namespace AetherIsland.App.ViewModels;

// ============================================================================
// Productivity (screen time) — consent-gated, honest, deletable.
// ============================================================================

public partial class ProductivityViewModel : ObservableObject, ILifecycleAware
{
    private readonly DispatcherTimer _timer;
    private int _tick;
    private DateTime _lastHeavyReload = DateTime.MinValue;

    [ObservableProperty] private bool _consentNeeded;
    [ObservableProperty] private bool _hasConsent;
    [ObservableProperty] private bool _pausedVisible;
    [ObservableProperty] private string _totalTodayText = "0m";
    [ObservableProperty] private string _currentAppText = "—";
    [ObservableProperty] private string _switchesText = "";
    [ObservableProperty] private string _idleNote = "";
    [ObservableProperty] private string _noDataText = "";
    [ObservableProperty] private string _selectedRange = "today";
    [ObservableProperty] private bool _isTodaySelected = true;
    [ObservableProperty] private bool _isDays7Selected;
    [ObservableProperty] private bool _isDays30Selected;
    [ObservableProperty] private ObservableCollection<CategoryRowVm> _byCategory = new();
    [ObservableProperty] private ObservableCollection<AppUsageRowVm> _topApps = new();

    public string ConsentTitle { get; } = Loc.T("screen.consentTitle");
    public string ConsentBody { get; } = Loc.T("screen.consentBody");
    public string AcceptLabel { get; } = Loc.T("screen.consentAccept");
    public string DeclineLabel { get; } = Loc.T("screen.consentDecline");
    public string EnableLabel { get; } = Loc.T("screen.consentAccept");
    public string PausedText { get; } = Loc.T("screen.paused");
    public string TotalTodayLabel { get; } = Loc.T("screen.totalToday");
    public string ByCategoryLabel { get; } = Loc.T("screen.byCategory");
    public string TopAppsLabel { get; } = Loc.T("screen.topApps");
    public string CurrentAppLabel { get; } = Loc.T("screen.current");
    public string DeleteLabel { get; } = Loc.T("screen.deleteHistory");
    public string RangeTodayLabel { get; } = Loc.T("screen.today");
    public string Range7Label { get; } = Loc.T("screen.days7");
    public string Range30Label { get; } = Loc.T("screen.days30");

    public bool DataVisible => !ConsentNeeded;

    partial void OnConsentNeededChanged(bool value) => OnPropertyChanged(nameof(DataVisible));

    public ProductivityViewModel()
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _timer.Tick += (_, _) => Tick();
    }

    public void OnShown()
    {
        ConsentNeeded = !AppServices.Settings.Privacy.ScreenTimeConsent;
        HasConsent = !ConsentNeeded;
        PausedVisible = HasConsent && !AppServices.ScreenTime.TrackingEnabled;
        _tick = 0;
        if (HasConsent)
        {
            ReloadAggregates();
            UpdateLive();
        }
        _timer.Start();
    }

    public void OnHidden() => _timer.Stop();

    private void Tick()
    {
        _tick++;
        if (ConsentNeeded || !AppServices.ScreenTime.TrackingEnabled) return;
        UpdateLive();
        if (SelectedRange == "today" || _tick % 15 == 0) ReloadAggregates();
    }

    private void UpdateLive()
    {
        var app = AppServices.ScreenTime.CurrentApp;
        CurrentAppText = string.IsNullOrEmpty(app.display) ? "—" : app.display;
        var idle = ForegroundTracker.GetIdleSeconds();
        IdleNote = idle > 300 ? $"{Loc.T("screen.idle")} · {idle / 60}m" : "";
    }

    private void ReloadAggregates()
    {
        if (ConsentNeeded || !AppServices.ScreenTime.TrackingEnabled) return;

        var days = SelectedRange switch { "days7" => 7, "days30" => 30, _ => 1 };
        if (days > 1)
        {
            if ((DateTime.Now - _lastHeavyReload).TotalSeconds < 20) return; // throttle multi-day scans
            _lastHeavyReload = DateTime.Now;
        }

        var merged = new Dictionary<string, (string Display, string Category, long Seconds, long Switches)>(
            StringComparer.OrdinalIgnoreCase);
        try
        {
            var repo = new ScreenTimeRepository(AppServices.Database);
            for (var i = 0; i < days; i++)
            {
                var day = DateTime.Now.Date.AddDays(-i).ToString("yyyy-MM-dd");
                foreach (var row in repo.GetDay(day).Values)
                {
                    if (merged.TryGetValue(row.Exe, out var m))
                        merged[row.Exe] = (
                            string.IsNullOrEmpty(row.DisplayName) ? m.Display : row.DisplayName,
                            string.IsNullOrEmpty(row.Category) ? m.Category : row.Category,
                            m.Seconds + row.Seconds,
                            m.Switches + row.Switches);
                    else
                        merged[row.Exe] = (row.DisplayName, row.Category, row.Seconds, row.Switches);
                }
            }
        }
        catch { }

        var total = merged.Values.Sum(v => v.Seconds);
        TotalTodayText = Humanize(total);
        NoDataText = total > 0 ? "" : Loc.T("common.none");

        ByCategory.Clear();
        foreach (var g in merged
                     .GroupBy(kv => kv.Value.Category, StringComparer.OrdinalIgnoreCase)
                     .Select(g => (Category: g.Key, Seconds: g.Sum(x => x.Value.Seconds)))
                     .OrderByDescending(x => x.Seconds))
            ByCategory.Add(new CategoryRowVm(
                Capitalize(g.Category),
                total > 0 ? Math.Clamp(g.Seconds / (double)total, 0, 1) : 0,
                Humanize(g.Seconds),
                BrushFor(g.Category)));

        TopApps.Clear();
        var rank = 1;
        foreach (var kv in merged.OrderByDescending(kv => kv.Value.Seconds).Take(8))
        {
            if (kv.Value.Seconds <= 0) continue;
            TopApps.Add(new AppUsageRowVm(
                rank.ToString(),
                string.IsNullOrEmpty(kv.Value.Display) ? kv.Key : kv.Value.Display,
                Humanize(kv.Value.Seconds),
                total > 0 ? Math.Clamp(kv.Value.Seconds / (double)total, 0, 1) : 0));
            rank++;
        }

        // persisted switches are currently 0 (service counts in memory); combine both sources
        var switches = merged.Values.Sum(v => v.Switches) + AppServices.ScreenTime.SwitchesToday;
        SwitchesText = $"{Loc.T("screen.switches")} · {switches}";
    }

    [RelayCommand]
    private void AcceptConsent()
    {
        AppServices.ScreenTime.SetConsent(true);
        AppServices.SettingsStore.Save(AppServices.Settings);
        ConsentNeeded = false;
        HasConsent = true;
        PausedVisible = false;
        _tick = 0;
        ReloadAggregates();
        UpdateLive();
    }

    [RelayCommand]
    private void DeclineConsent() => PausedVisible = true; // stays off — honest paused hint

    [RelayCommand]
    private void Range(string range)
    {
        SelectedRange = range is "days7" or "days30" ? range : "today";
        IsTodaySelected = SelectedRange == "today";
        IsDays7Selected = SelectedRange == "days7";
        IsDays30Selected = SelectedRange == "days30";
        _tick = 0;
        _lastHeavyReload = DateTime.MinValue;
        ReloadAggregates();
    }

    [RelayCommand]
    private void DeleteHistory()
    {
        try
        {
            new ScreenTimeRepository(AppServices.Database).DeleteAll();
            _tick = 0;
            _lastHeavyReload = DateTime.MinValue;
            ReloadAggregates();
            AppServices.Notify.ShowIslandContext("\uE73E", Loc.T("common.done"));
        }
        catch
        {
            AppServices.Notify.ShowIslandContext("\uE783", Loc.T("common.error"));
        }
    }

    private static string Humanize(long seconds)
    {
        if (seconds <= 0) return "0m";
        var t = TimeSpan.FromSeconds(seconds);
        return t.TotalHours >= 1 ? $"{(int)t.TotalHours}h {t.Minutes}m" : $"{t.Minutes}m";
    }

    private static string Capitalize(string s) =>
        string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s[1..];

    private static readonly SolidColorBrush CreativeBrush = CreateCreativeBrush();

    private static SolidColorBrush CreateCreativeBrush()
    {
        var b = new SolidColorBrush(Color.FromRgb(0xE5, 0x8B, 0xC0));
        b.Freeze();
        return b;
    }

    private static Brush Res(string key) =>
        Application.Current?.TryFindResource(key) as Brush ?? Brushes.Gray;

    private static Brush BrushFor(string category) => category.ToLowerInvariant() switch
    {
        "browsing" => Res("AccentBrush"),
        "communication" => Res("AiBrush"),
        "development" => Res("SuccessBrush"),
        "work" => Res("WarningBrush"),
        "media" => Res("DangerBrush"),
        "creative" => CreativeBrush,
        _ => Res("MutedBrush"),
    };
}

public sealed record CategoryRowVm(string Label, double Fraction, string TimeText, Brush Brush);

public sealed record AppUsageRowVm(string Rank, string DisplayName, string TimeText, double Fraction);

// ============================================================================
// Network — Wi-Fi + Bluetooth. Statics feed the dashboard quick toggles.
// ============================================================================

public partial class NetworkViewModel : ObservableObject, ILifecycleAware
{
    private readonly DispatcherTimer _timer;
    private bool _refreshing;

    public static bool WifiConnected { get; private set; }
    public static bool BluetoothOn { get; private set; }

    [ObservableProperty] private bool _hasCurrentNetwork;
    [ObservableProperty] private string _currentSsid = Loc.T("common.loading");
    [ObservableProperty] private double _currentSignal;
    [ObservableProperty] private string _currentSignalText = "—";
    [ObservableProperty] private string _currentSecureText = "";
    [ObservableProperty] private string _ipText = "—";
    [ObservableProperty] private string _noAdapterText = AetherIsland.Core.Localization.Loc.T("net.noAdapter");
    [ObservableProperty] private string _radioOffText = AetherIsland.Core.Localization.Loc.T("net.radioOff");
    [ObservableProperty] private bool _noAdapterVisible;
    [ObservableProperty] private bool _radioOffVisible;
    [ObservableProperty] private string _availableEmptyText = "";
    [ObservableProperty] private string _bluetoothStateText = Loc.T("common.loading");
    [ObservableProperty] private string _devicesNote = "";
    [ObservableProperty] private ObservableCollection<WifiRowVm> _available = new();
    [ObservableProperty] private ObservableCollection<BluetoothDeviceVm> _bluetoothDevices = new();

    public string WifiTitle { get; } = Loc.T("net.wifi");
    public string BluetoothTitle { get; } = Loc.T("net.bluetooth");
    public string CurrentLabel { get; } = Loc.T("net.currentNetwork");
    public string AvailableLabel { get; } = Loc.T("net.available");
    public string SignalLabel { get; } = Loc.T("net.signal");
    public string ConnectLabel { get; } = Loc.T("net.connect");
    public string DisconnectLabel { get; } = Loc.T("net.disconnect");
    public string OpenSettingsLabel { get; } = Loc.T("net.openSettings");
    public string IpLabel { get; } = Loc.T("net.ip");
    public string RefreshLabel { get; } = Loc.T("common.refresh");
    public string DevicesLabel { get; } = Loc.T("net.knownDevices");

    public NetworkViewModel()
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(20) };
        _timer.Tick += (_, _) => _ = RefreshAsync();
    }

    public void OnShown()
    {
        _ = RefreshAsync();
        _timer.Start();
    }

    public void OnHidden() => _timer.Stop();

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (_refreshing) return;
        _refreshing = true;
        try
        {
            var nets = await Task.Run(() => WlanApi.GetAvailableNetworks());
            var interfaces = await Task.Run(() => WlanApi.EnumerateInterfaces());

            var connected = nets.FirstOrDefault(n => n.Connected)
                            ?? (interfaces.Count > 0 ? interfaces[0].Current : null);
            WifiConnected = connected is not null || interfaces.Any(i => i.State == "connected");

            NoAdapterVisible = interfaces.Count == 0 && nets.Count == 0;
            RadioOffVisible = !NoAdapterVisible && interfaces.Count > 0 && nets.Count == 0 && connected is null;

            if (connected is { } cur)
            {
                HasCurrentNetwork = true;
                CurrentSsid = cur.Ssid;
                CurrentSignal = Math.Clamp(cur.SignalPercent / 100.0, 0, 1);
                CurrentSignalText = $"{cur.SignalPercent}%";
                CurrentSecureText = cur.Secured ? Loc.T("net.secured") : Loc.T("net.open");
            }
            else
            {
                HasCurrentNetwork = false;
                CurrentSsid = NoAdapterVisible ? Loc.T("net.noAdapter")
                    : RadioOffVisible ? Loc.T("net.radioOff")
                    : Loc.T("common.disconnected");
                CurrentSignal = 0;
                CurrentSignalText = "—";
                CurrentSecureText = "";
            }

            Available.Clear();
            foreach (var n in nets.Where(n => !string.IsNullOrWhiteSpace(n.Ssid)))
                Available.Add(new WifiRowVm(n.Ssid, n.SignalPercent, "\uE701", n.Secured, n.Connected, !n.Secured));
            AvailableEmptyText = Available.Count == 0 && !NoAdapterVisible && !RadioOffVisible
                ? Loc.T("common.none") : "";

            IpText = ReadIpv4();
            await RefreshBluetoothAsync();
        }
        catch
        {
            AvailableEmptyText = Loc.T("common.error");
        }
        finally
        {
            _refreshing = false;
        }
    }

    [RelayCommand]
    private async Task ConnectAsync(WifiRowVm? row)
    {
        if (row is null || row.IsConnected) return;
        if (row.IsSecured)
        {
            // WPS/profile entry needs the OS — never fake a password prompt
            AppServices.Notify.ShowBalloon(Loc.T("net.connect"), Loc.T("net.openSettings"));
            OpenWifiSettings();
            return;
        }
        var err = await Task.Run(() => WlanApi.ConnectOpen(row.Ssid));
        if (err is not null) AppServices.Notify.ShowBalloon(Loc.T("common.error"), err);
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task DisconnectAsync()
    {
        await Task.Run(() => WlanApi.Disconnect());
        await RefreshAsync();
    }

    [RelayCommand]
    private void OpenSettings() => OpenWifiSettings();

    [RelayCommand]
    private void OpenBluetoothSettings() => OpenUri("ms-settings:bluetooth");

    private static void OpenWifiSettings() => OpenUri("ms-settings:network-wifi");

    private static void OpenUri(string uri)
    {
        try { Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true }); }
        catch { }
    }

    private async Task RefreshBluetoothAsync()
    {
        try
        {
            var radios = await global::Windows.Devices.Radios.Radio.GetRadiosAsync();
            BluetoothOn = radios.Any(r => r.State == global::Windows.Devices.Radios.RadioState.On);
            BluetoothStateText = radios.Count == 0 ? Loc.T("net.noAdapter")
                : BluetoothOn ? Loc.T("common.enabled")
                : Loc.T("net.radioOff");

            BluetoothDevices.Clear();
            if (BluetoothOn)
            {
                var devices = await global::Windows.Devices.Enumeration.DeviceInformation.FindAllAsync();
                foreach (var d in devices)
                {
                    try
                    {
                        var state = "";
                        if (d.Properties.TryGetValue("System.Devices.Aep.IsConnected", out var c) && c is bool b)
                            state = b ? Loc.T("common.connected") : Loc.T("common.disconnected");
                        var battery = "";
                        if (d.Properties.TryGetValue("System.DeviceInterface.Bluetooth.Battery", out var v) &&
                            byte.TryParse(v?.ToString(), out var pct))
                            battery = $"{pct}%";
                        BluetoothDevices.Add(new BluetoothDeviceVm(d.Name, state, battery));
                    }
                    catch { }
                }
            }
            DevicesNote = BluetoothDevices.Count == 0 && BluetoothOn ? Loc.T("common.none") : "";
        }
        catch
        {
            BluetoothOn = false;
            BluetoothStateText = Loc.T("net.noAdapter");
            BluetoothDevices.Clear();
            DevicesNote = "";
        }
    }

    private static string ReadIpv4()
    {
        try
        {
            string? first = null;
            foreach (var nic in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Loopback) continue;
                if (nic.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up) continue;
                var props = nic.GetIPProperties();
                var hasGateway = props.GatewayAddresses.Any(
                    g => g.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
                foreach (var addr in props.UnicastAddresses)
                {
                    if (addr.Address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork) continue;
                    if (hasGateway) return addr.Address.ToString();
                    first ??= addr.Address.ToString();
                }
            }
            return first ?? "—";
        }
        catch
        {
            return "—";
        }
    }
}

public sealed record WifiRowVm(
    string Ssid, int SignalPercent, string SignalGlyph,
    bool IsSecured, bool IsConnected, bool IsOpen)
{
    public string SecureText => IsSecured ? AetherIsland.Core.Localization.Loc.T("net.secured") : AetherIsland.Core.Localization.Loc.T("net.open");
}

public sealed record BluetoothDeviceVm(string Name, string StateText, string BatteryText);

// ============================================================================
// System — live metrics. Expensive sampling pauses with the panel.
// ============================================================================

public partial class SystemViewModel : ObservableObject, ILifecycleAware
{
    private readonly SystemMetricsSampler _sampler = new();
    private readonly DispatcherTimer _timer;
    private int _tick;
    private DateTime _lastProcSample = DateTime.Now;
    private readonly Dictionary<int, (string Name, TimeSpan Cpu)> _lastProcCpu = new();

    [ObservableProperty] private double _cpuPercent;
    [ObservableProperty] private string _cpuText = "—";
    [ObservableProperty] private double _memPercent;
    [ObservableProperty] private string _memText = "—";
    [ObservableProperty] private string _memDetailText = "";
    [ObservableProperty] private string _upText = "—";
    [ObservableProperty] private string _downText = "—";
    [ObservableProperty] private string _batteryText = "";
    [ObservableProperty] private double _batteryFraction;
    [ObservableProperty] private string _batteryDetailText = "";
    [ObservableProperty] private bool _isCharging;
    [ObservableProperty] private bool _batteryVisible;
    [ObservableProperty] private bool _noBattery = true;
    [ObservableProperty] private string _tempText = Loc.T("sys.unavailable");
    [ObservableProperty] private string _processesNote = Loc.T("common.loading");
    [ObservableProperty] private ObservableCollection<DiskRowVm> _disks = new();
    [ObservableProperty] private ObservableCollection<ProcessRowVm> _topProcesses = new();

    public string CpuLabel { get; } = Loc.T("sys.cpu");
    public string MemLabel { get; } = Loc.T("sys.memory");
    public string BatteryLabel { get; } = Loc.T("sys.battery");
    public string NetLabel { get; } = Loc.T("sys.network");
    public string UpLabel { get; } = Loc.T("sys.up");
    public string DownLabel { get; } = Loc.T("sys.down");
    public string DiskLabel { get; } = Loc.T("sys.disk");
    public string TopProcessesLabel { get; } = Loc.T("sys.topProcesses");
    public string TempLabel { get; } = Loc.T("sys.temperature");
    public string ChargingText { get; } = Loc.T("sys.charging");

    public SystemViewModel()
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => Tick();
    }

    public void OnShown()
    {
        _tick = 0;
        Tick();
        _timer.Start();
    }

    public void OnHidden() => _timer.Stop();

    private void Tick()
    {
        _tick++;
        try
        {
            var s = _sampler.Sample();
            CpuPercent = Math.Clamp(s.CpuPercent, 0, 100);
            CpuText = $"{Math.Round(CpuPercent)}%";
            MemPercent = Math.Clamp(s.MemoryPercent, 0, 100);
            MemText = $"{Math.Round(MemPercent)}%";
            MemDetailText = $"{Gb(s.MemoryUsedBytes)} / {Gb(s.MemoryTotalBytes)} GB";
            UpText = FormatSpeed(s.NetUpBps);
            DownText = FormatSpeed(s.NetDownBps);

            BatteryVisible = s.HasBattery;
            NoBattery = !s.HasBattery;
            if (s.HasBattery)
            {
                BatteryFraction = Math.Clamp(s.BatteryPercent / 100.0, 0, 1);
                BatteryText = $"{s.BatteryPercent}%";
                IsCharging = s.Charging;
                BatteryDetailText = s.Charging
                    ? Loc.T("sys.charging")
                    : s.BatteryLifeSeconds != uint.MaxValue
                        ? Loc.TF("sys.remaining", $"{s.BatteryLifeSeconds / 3600}h {s.BatteryLifeSeconds % 3600 / 60}m")
                        : "";
            }

            TempText = s.CpuTemperatureCelsius is null
                ? Loc.T("sys.unavailable")
                : $"{s.CpuTemperatureCelsius} °C";

            if (_tick % 10 == 1) LoadDisks();
            if (_tick % 3 == 0) SampleProcesses();
        }
        catch { }
    }

    private void LoadDisks()
    {
        Disks.Clear();
        try
        {
            foreach (var d in DiskStore.GetDisks())
            {
                if (d.TotalBytes <= 0) continue;
                var used = d.TotalBytes - d.FreeBytes;
                Disks.Add(new DiskRowVm(
                    d.Drive,
                    Math.Clamp(used / (double)d.TotalBytes, 0, 1),
                    $"{Loc.T("sys.free")} {Gb(d.FreeBytes)} GB"));
            }
        }
        catch { }
    }

    private void SampleProcesses()
    {
        var now = DateTime.Now;
        var elapsed = (now - _lastProcSample).TotalSeconds;
        var current = new Dictionary<int, (string Name, TimeSpan Cpu)>();
        try
        {
            foreach (var p in Process.GetProcesses())
            {
                try
                {
                    int id;
                    string name;
                    TimeSpan cpu;
                    using (p)
                    {
                        cpu = p.TotalProcessorTime;
                        name = p.ProcessName;
                        id = p.Id;
                    }
                    current[id] = (name, cpu);
                }
                catch { /* access denied / exited */ }
            }
        }
        catch { }

        if (_lastProcCpu.Count > 0 && elapsed >= 1.0)
        {
            var cores = Math.Max(1, Environment.ProcessorCount);
            var rows = new List<ProcessRowVm>();
            foreach (var (id, (name, cpu)) in current)
            {
                if (!_lastProcCpu.TryGetValue(id, out var prev)) continue;
                var pct = (cpu - prev.Cpu).TotalSeconds / elapsed / cores * 100.0;
                if (pct > 0.05)
                    rows.Add(new ProcessRowVm(name, Math.Clamp(pct, 0, 100), $"{Math.Clamp(pct, 0, 100):0.0}%"));
            }
            TopProcesses.Clear();
            foreach (var r in rows.OrderByDescending(r => r.CpuPercent).Take(5))
                TopProcesses.Add(r);
            ProcessesNote = TopProcesses.Count == 0 ? Loc.T("common.none") : "";
        }

        _lastProcSample = now;
        _lastProcCpu.Clear();
        foreach (var kv in current) _lastProcCpu[kv.Key] = kv.Value;
    }

    private static string Gb(ulong bytes) =>
        (bytes / 1073741824.0).ToString("0.#", CultureInfo.InvariantCulture);

    private static string FormatSpeed(double bps) => bps switch
    {
        >= 1_000_000 => $"{bps / 1_000_000.0:0.#} MB/s",
        >= 1_000 => $"{bps / 1_000.0:0.#} KB/s",
        _ => $"{bps:0} B/s",
    };
}

public sealed record DiskRowVm(string Drive, double UsedFraction, string FreeText);

public sealed record ProcessRowVm(string Name, double CpuPercent, string CpuText);

// ============================================================================
// Wellbeing — hydration, reminder countdowns, breathing.
// ============================================================================

public partial class WellbeingViewModel : ObservableObject, ILifecycleAware
{
    private readonly DispatcherTimer _clock;
    private readonly DispatcherTimer _breath;
    private DateTime _lastEye = DateTime.Now;
    private DateTime _lastMove = DateTime.Now;
    private double _breathElapsed;

    [ObservableProperty] private int _hydrationTodayMl;
    [ObservableProperty] private string _hydrationText = "0 ml";
    [ObservableProperty] private string _goalText = "";
    [ObservableProperty] private double _hydrationFraction;
    [ObservableProperty] private string _eyeEveryText = "";
    [ObservableProperty] private string _nextEyeText = "";
    [ObservableProperty] private string _moveEveryText = "";
    [ObservableProperty] private string _nextMoveText = "";
    [ObservableProperty] private bool _isBreathing;
    [ObservableProperty] private string _breathPhase = Loc.T("well.breathIn");
    [ObservableProperty] private double _breathFraction;
    [ObservableProperty] private string _streakText = "";

    public string HydrationTitle { get; } = Loc.T("well.hydration");
    public string AddCupLabel { get; } = Loc.T("well.addCup");
    public string ResetLabel { get; } = Loc.T("focus.reset");
    public string EyeTitle { get; } = Loc.T("well.eyeBreak");
    public string MoveTitle { get; } = Loc.T("well.move");
    public string BreathingTitle { get; } = Loc.T("well.breathing");
    public string StartBreathingLabel { get; } = Loc.T("well.startBreathing");
    public string RemindersTitle { get; } = Loc.T("nav.wellbeing");
    public string BreathButtonLabel => IsBreathing ? Loc.T("common.close") : StartBreathingLabel;

    partial void OnIsBreathingChanged(bool value) => OnPropertyChanged(nameof(BreathButtonLabel));

    public WellbeingViewModel()
    {
        _clock = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _clock.Tick += (_, _) => UpdateCountdowns();
        _breath = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _breath.Tick += (_, _) => BreathTick();
        RefreshHydration();
        UpdateCountdowns();
    }

    public void OnShown()
    {
        RefreshHydration();
        UpdateCountdowns();
        LoadStreak();
        AppServices.Health.HydrationChanged += OnHydrationChanged;
        AppServices.Health.ReminderDue += OnReminder;
        _clock.Start();
    }

    public void OnHidden()
    {
        AppServices.Health.HydrationChanged -= OnHydrationChanged;
        AppServices.Health.ReminderDue -= OnReminder;
        _clock.Stop();
        if (IsBreathing) StopBreathing();
    }

    private void OnHydrationChanged() => RefreshHydration();

    private void OnReminder(string kind)
    {
        if (kind == "eye") _lastEye = DateTime.Now;
        else if (kind == "move") _lastMove = DateTime.Now;
        UpdateCountdowns();
    }

    private void RefreshHydration()
    {
        HydrationTodayMl = AppServices.Health.HydrationToday;
        var goal = Math.Max(1, AppServices.Settings.Health.HydrationGoalMl);
        HydrationText = $"{HydrationTodayMl} ml";
        HydrationFraction = Math.Clamp(HydrationTodayMl / (double)goal, 0, 1);
        GoalText = $"{Loc.T("well.goal")} {goal} ml";
    }

    private void UpdateCountdowns()
    {
        var s = AppServices.Settings.Health;
        EyeEveryText = Loc.TF("well.everyMinutes", s.EyeBreakMinutes);
        MoveEveryText = Loc.TF("well.everyMinutes", s.MoveMinutes);
        var eyeIn = Math.Max(0, s.EyeBreakMinutes - (int)(DateTime.Now - _lastEye).TotalMinutes);
        var moveIn = Math.Max(0, s.MoveMinutes - (int)(DateTime.Now - _lastMove).TotalMinutes);
        NextEyeText = Loc.TF("calendar.in", $"{eyeIn}m");
        NextMoveText = Loc.TF("calendar.in", $"{moveIn}m");
    }

    private void LoadStreak()
    {
        try
        {
            var streak = new HealthLogRepository(AppServices.Database).Get("breakstreak");
            StreakText = $"{Loc.T("well.streak")} · {streak}";
        }
        catch
        {
            StreakText = Loc.T("well.streak");
        }
    }

    [RelayCommand]
    private void AddCup() => AppServices.Health.AddHydration(AppServices.Settings.Health.HydrationCupMl);

    [RelayCommand]
    private void Reset() => AppServices.Health.ResetHydration();

    [RelayCommand]
    private void StartBreathing()
    {
        if (IsBreathing) { StopBreathing(); return; }
        IsBreathing = true;
        _breathElapsed = 0;
        _breath.Start();
    }

    private void StopBreathing()
    {
        IsBreathing = false;
        _breath.Stop();
        _breathElapsed = 0;
        BreathFraction = 0;
        BreathPhase = Loc.T("well.breathIn");
    }

    private void BreathTick()
    {
        _breathElapsed += 0.1;
        var cycle = _breathElapsed % 14.0; // 4s in, 4s hold, 6s out — repeats until stopped
        if (cycle < 4.0)
        {
            BreathPhase = Loc.T("well.breathIn");
            BreathFraction = Math.Clamp(cycle / 4.0, 0, 1);
        }
        else if (cycle < 8.0)
        {
            BreathPhase = Loc.T("well.hold");
            BreathFraction = Math.Clamp((cycle - 4.0) / 4.0, 0, 1);
        }
        else
        {
            BreathPhase = Loc.T("well.breathOut");
            BreathFraction = Math.Clamp((cycle - 8.0) / 6.0, 0, 1);
        }
    }
}

// ============================================================================
// Utilities — quick actions, calculator, favorites. Statics feed the dashboard.
// ============================================================================

public partial class UtilitiesViewModel : ObservableObject, ILifecycleAware
{
    private const uint SHERB_NOCONFIRMATION = 0x1;
    private const uint SHERB_NOPROGRESSUI = 0x2;
    private const uint SHERB_NOSOUND = 0x4;

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHEmptyRecycleBin(IntPtr hwnd, string? root, uint flags);

    // ---- static surface used by Home/Dashboard quick actions ----

    public static bool KeepAwakeActive { get; private set; }

    public static void SetKeepAwake(bool on)
    {
        if (on) PowerHelper.KeepAwake(enableDisplay: true);
        else PowerHelper.AllowSleep();
        KeepAwakeActive = on;
    }

    public static bool MicMuted { get; private set; } = ReadMicMuted();
    public static bool SpeakerMuted { get; private set; } = ReadSpeakerMuted();

    public static void ToggleMic()
    {
        try { MicMuted = AudioEndpoints.ToggleMicrophoneMute(); }
        catch { }
    }

    public static void ToggleSpeaker()
    {
        try { SpeakerMuted = AudioEndpoints.ToggleSpeakerMute(); }
        catch { }
    }

    public static async Task TakeScreenshotAsync()
    {
        var dir = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        try { if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir); } catch { }
        var path = Path.Combine(
            string.IsNullOrEmpty(dir) ? "." : dir,
            $"AetherIsland_{DateTime.Now:yyyyMMdd_HHmmss}.png");
        await Task.Run(() => CaptureScreen(path));
        AppServices.Notify.ShowIslandContext("\uE7A8", Loc.T("util.screenshotSaved"));
    }

    /// <summary>Captures the whole virtual screen to a PNG (no WinForms).</summary>
    public static void CaptureScreen(string path)
    {
        try
        {
            var w = (int)SystemParameters.VirtualScreenWidth;
            var h = (int)SystemParameters.VirtualScreenHeight;
            var x = (int)SystemParameters.VirtualScreenLeft;
            var y = (int)SystemParameters.VirtualScreenTop;
            if (w <= 0 || h <= 0) return;
            using var bmp = new System.Drawing.Bitmap(w, h);
            using var g = System.Drawing.Graphics.FromImage(bmp);
            g.CopyFromScreen(x, y, 0, 0, new System.Drawing.Size(w, h));
            bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
        }
        catch { }
    }

    private static bool ReadMicMuted()
    {
        try { return AudioEndpoints.GetMicrophone().Muted; }
        catch { return false; }
    }

    private static bool ReadSpeakerMuted()
    {
        try { return AudioEndpoints.GetSpeaker().Muted; }
        catch { return false; }
    }

    // ---- instance state ----

    [ObservableProperty] private string _calcExpression = "";
    [ObservableProperty] private string _calcResult = "";
    [ObservableProperty] private string _keepAwakeText = "";
    [ObservableProperty] private ObservableCollection<FolderRowVm> _folders = new();
    [ObservableProperty] private ObservableCollection<LinkRowVm> _websites = new();
    [ObservableProperty] private string _foldersNote = "";
    [ObservableProperty] private string _websitesNote = "";

    public string ScreenshotLabel { get; } = Loc.T("util.screenshot");
    public string LockLabel { get; } = Loc.T("util.lock");
    public string TaskManagerLabel { get; } = Loc.T("util.taskManager");
    public string ExplorerLabel { get; } = Loc.T("util.explorer");
    public string SettingsLabel { get; } = Loc.T("util.winSettings");
    public string EmptyBinLabel { get; } = Loc.T("util.emptyBin");
    public string KeepAwakeLabel { get; } = Loc.T("util.awake");
    public string CalculatorLabel { get; } = Loc.T("util.calculator");
    public string FoldersLabel { get; } = Loc.T("util.folders");
    public string WebsitesLabel { get; } = Loc.T("util.websites");
    public string AddFolderLabel { get; } = Loc.T("common.add");
    public string EvalLabel { get; } = Loc.T("util.eval");

    private static string StorePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AetherIsland", "utilities.json");

    public UtilitiesViewModel()
    {
        KeepAwakeText = DescribeKeepAwake();
        LoadFavorites();
    }

    public void OnShown()
    {
        MicMuted = ReadMicMuted();
        SpeakerMuted = ReadSpeakerMuted();
        OnPropertyChanged(nameof(KeepAwakeOn));
        KeepAwakeText = DescribeKeepAwake();
    }

    public void OnHidden() { }

    public bool KeepAwakeOn
    {
        get => KeepAwakeActive;
        set
        {
            SetKeepAwake(value);
            OnPropertyChanged();
            KeepAwakeText = DescribeKeepAwake();
        }
    }

    private static string DescribeKeepAwake() =>
        Loc.T("util.awake") + (KeepAwakeActive
            ? $" · {Loc.T("common.enabled")}"
            : $" · {Loc.T("common.disabled")}");

    [RelayCommand]
    private void Screenshot() => _ = TakeScreenshotAsync();

    [RelayCommand]
    private void Lock()
    {
        try { PowerHelper.LockWorkstation(); }
        catch { }
    }

    [RelayCommand]
    private void TaskManager() => Start("taskmgr.exe");

    [RelayCommand]
    private void Explorer() => Start("explorer.exe");

    [RelayCommand]
    private void Settings() => Start("ms-settings:");

    private static void Start(string target)
    {
        try { Process.Start(new ProcessStartInfo(target) { UseShellExecute = true }); }
        catch (Exception ex) { AppServices.Notify.ShowBalloon(Loc.T("common.error"), ex.Message); }
    }

    [RelayCommand]
    private void EmptyBin()
    {
        if (MessageBox.Show(Loc.T("util.confirmEmptyBin"), Loc.T("app.name"),
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        try
        {
            SHEmptyRecycleBin(IntPtr.Zero, null, SHERB_NOCONFIRMATION | SHERB_NOPROGRESSUI | SHERB_NOSOUND);
            AppServices.Notify.ShowIslandContext("\uE73E", Loc.T("common.done"));
        }
        catch
        {
            AppServices.Notify.ShowIslandContext("\uE783", Loc.T("common.error"));
        }
    }

    [RelayCommand]
    private void KeepAwake()
    {
        SetKeepAwake(!KeepAwakeActive);
        OnPropertyChanged(nameof(KeepAwakeOn));
        KeepAwakeText = DescribeKeepAwake();
    }

    // ---- calculator (small recursive-descent parser; no external libs) ----

    [RelayCommand]
    private void Eval()
    {
        var value = TryEvaluate(CalcExpression, out var ok);
        CalcResult = ok ? value : Loc.T("common.error");
    }

    private static string TryEvaluate(string expr, out bool ok)
    {
        ok = false;
        try
        {
            var s = (expr ?? "").Replace(" ", "").Replace(",", ".");
            if (s.Length == 0 || s.Length > 64) return "0";
            var pos = 0;
            var v = ParseExpr(s, ref pos);
            if (pos != s.Length || !double.IsFinite(v)) return "0";
            ok = true;
            return Math.Abs(v) < 1e15 && v == Math.Floor(v)
                ? v.ToString("N0", CultureInfo.InvariantCulture)
                : v.ToString("0.####", CultureInfo.InvariantCulture);
        }
        catch
        {
            return "0";
        }
    }

    private static double ParseExpr(string s, ref int pos)
    {
        var v = ParseTerm(s, ref pos);
        while (pos < s.Length && (s[pos] == '+' || s[pos] == '-'))
        {
            var op = s[pos++];
            var r = ParseTerm(s, ref pos);
            v = op == '+' ? v + r : v - r;
        }
        return v;
    }

    private static double ParseTerm(string s, ref int pos)
    {
        var v = ParseFactor(s, ref pos);
        while (pos < s.Length && (s[pos] == '*' || s[pos] == '/'))
        {
            var op = s[pos++];
            var r = ParseFactor(s, ref pos);
            if (op == '/' && r == 0) throw new DivideByZeroException();
            v = op == '*' ? v * r : v / r;
        }
        return v;
    }

    private static double ParseFactor(string s, ref int pos)
    {
        if (pos >= s.Length) throw new FormatException();
        if (s[pos] == '-') { pos++; return -ParseFactor(s, ref pos); }
        if (s[pos] == '+') { pos++; return ParseFactor(s, ref pos); }
        if (s[pos] == '(')
        {
            pos++;
            var v = ParseExpr(s, ref pos);
            if (pos >= s.Length || s[pos] != ')') throw new FormatException();
            pos++;
            return v;
        }
        var start = pos;
        while (pos < s.Length && (char.IsDigit(s[pos]) || s[pos] == '.')) pos++;
        if (start == pos) throw new FormatException();
        return double.Parse(s[start..pos], CultureInfo.InvariantCulture);
    }

    // ---- favorites: folders + websites, file-backed (%LOCALAPPDATA%\AetherIsland\utilities.json) ----

    private sealed class NamePath
    {
        public string Name { get; set; } = "";
        public string Path { get; set; } = "";
        public string Url { get; set; } = "";
    }

    private void LoadFavorites()
    {
        List<NamePath> store;
        try
        {
            store = File.Exists(StorePath)
                ? JsonSerializer.Deserialize<List<NamePath>>(File.ReadAllText(StorePath)) ?? SeedFolders()
                : SeedFolders();
        }
        catch
        {
            store = SeedFolders();
        }

        Folders.Clear();
        foreach (var f in store.Where(i => i.Url.Length == 0))
            Folders.Add(new FolderRowVm(f.Name, f.Path));
        Websites.Clear();
        foreach (var w in store.Where(i => i.Url.Length > 0))
            Websites.Add(new LinkRowVm(w.Name, w.Url));
        FoldersNote = Folders.Count == 0 ? Loc.T("common.none") : "";
        WebsitesNote = Websites.Count == 0 ? Loc.T("common.none") : "";
    }

    private static List<NamePath> SeedFolders()
    {
        var list = new List<NamePath>();
        void TryAdd(string name, string path)
        {
            if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                list.Add(new NamePath { Name = name, Path = path });
        }
        TryAdd("Desktop", Environment.GetFolderPath(Environment.SpecialFolder.Desktop));
        TryAdd("Documents", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
        TryAdd("Downloads", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"));
        TryAdd("Pictures", Environment.GetFolderPath(Environment.SpecialFolder.MyPictures));
        TryAdd("Videos", Environment.GetFolderPath(Environment.SpecialFolder.MyVideos));
        TryAdd("Music", Environment.GetFolderPath(Environment.SpecialFolder.MyMusic));
        return list;
    }

    private void SaveFavorites()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(StorePath)!);
            var list = Folders.Select(f => new NamePath { Name = f.Name, Path = f.Path }).ToList();
            list.AddRange(Websites.Select(w => new NamePath { Name = w.Name, Url = w.Url }));
            File.WriteAllText(StorePath, JsonSerializer.Serialize(list));
            FoldersNote = Folders.Count == 0 ? Loc.T("common.none") : "";
            WebsitesNote = Websites.Count == 0 ? Loc.T("common.none") : "";
        }
        catch { }
    }

    [RelayCommand]
    private void AddFolder()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog { Title = Loc.T("util.folders"), CheckFileExists = true };
        if (dlg.ShowDialog() != true) return;
        var dir = Path.GetDirectoryName(dlg.FileName);
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return;
        if (Folders.Any(f => string.Equals(f.Path, dir, StringComparison.OrdinalIgnoreCase))) return;
        var name = Path.GetFileName(dir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        Folders.Add(new FolderRowVm(string.IsNullOrEmpty(name) ? dir : name, dir));
        SaveFavorites();
    }

    [RelayCommand]
    private void OpenItem(FolderRowVm? f)
    {
        if (f is null) return;
        try { Process.Start(new ProcessStartInfo(f.Path) { UseShellExecute = true }); }
        catch (Exception ex) { AppServices.Notify.ShowBalloon(Loc.T("common.error"), ex.Message); }
    }

    [RelayCommand]
    private void OpenLink(LinkRowVm? l)
    {
        if (l is null || string.IsNullOrWhiteSpace(l.Url)) return;
        try { Process.Start(new ProcessStartInfo(l.Url) { UseShellExecute = true }); }
        catch (Exception ex) { AppServices.Notify.ShowBalloon(Loc.T("common.error"), ex.Message); }
    }
}

public sealed record FolderRowVm(string Name, string Path);

public sealed record LinkRowVm(string Name, string Url);
