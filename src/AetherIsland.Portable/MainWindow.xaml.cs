using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using AetherIsland.Integrations.Media;

namespace AetherIsland.Portable;

/// <summary>
/// Explicit, no-focus-loss-auto-collapse state machine. A click expands;
/// a move past the OS drag threshold drags, not expands. No fullscreen polling.
/// </summary>
public partial class MainWindow : Window
{
    public const double PillWidth = 196, PillHeight = 46, PanelWidth = 945, PanelHeight = 248;

    private bool _expanded;
    private bool _pinned;
    private bool _pillPressed;
    private Point _pressPosition;
    private string _current = "dashboard";

    /// <summary>Static module renderers reach the live window through this.</summary>
    internal static MainWindow Win => (MainWindow)System.Windows.Application.Current.MainWindow;

    private readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromMinutes(1) };
    private readonly Dictionary<string, Button> _buttons = new();
    private readonly System.Windows.Forms.NotifyIcon _tray;
    private readonly SmtcMediaWatcher _media = new();
    private bool _mediaStarted;
    private readonly ActivityTracker _activity = new();

    public record Module(string Id, string Icon, string Title, Func<UIElement> Render, string Tooltip);

    private static readonly Module[] Sections =
    {
        new("home",      "home",      "Home",       RenderHome,      "Home"),
        new("dashboard", "dashboard", "Dashboard",  RenderDashboard, "Dashboard"),
        new("shelf",     "shelf",     "File shelf", RenderShelf,     "File shelf"),
        new("tasks",     "tasks",     "Tasks",      RenderTasks,     "Tasks"),
        new("notes",     "notes",     "Notes",      RenderNotes,     "Notes"),
        new("focus",     "focus",     "Focus",      RenderFocus,     "Focus timer"),
        new("media",     "media",     "Media",      RenderMedia,     "Now playing and calendar"),
        new("wellbeing", "wellbeing", "Wellbeing",  RenderWellbeing, "Wellbeing"),
        new("screen",    "screen",    "Screen time",RenderScreen,    "Screen time"),
        new("weather",   "weather",   "Weather",    RenderWeather,   "Weather"),
        new("network",   "network",   "Network",    RenderNetwork,   "Wi-Fi and Bluetooth"),
        new("ai",        "ai",        "AI quota",   RenderAi,        "Verified AI quota"),
        new("utilities", "utilities", "Utilities",  RenderUtilities, "Utilities"),
    };

    public MainWindow()
    {
        InitializeComponent();
        Theme.Apply(PortableData.Settings.Theme);
        _pinned = PortableData.Settings.Pinned;

        var wa = SystemParameters.WorkArea;
        Left = PortableData.Settings.Left ?? wa.Left + (wa.Width - PillWidth) / 2;
        Top = PortableData.Settings.Top ?? wa.Top + 6;
        ClampToVisibleMonitor();

        BuildNavigation();
        BuildHeaderButtons();
        ApplyThemeChrome();
        KeyDown += WindowKeyDown;
        Deactivated += (_, _) =>
        {
            // Focus moved anywhere else while expanded → minimize to the
            // pill. Collapse only swaps internal visuals and resizes, so
            // this can never make the app disappear. Pin overrides.
            if (_expanded && !_pinned)
                Collapse();
        };
        _tray = BuildTray();
        _clock.Tick += (_, _) => UpdateClock();
        _clock.Start();
        UpdateClock();
        Navigate("dashboard");  // Prepare panel before first expansion.

        if (PortableData.Settings.HotkeyEnabled) RegisterGlobalHotkey();
        StartupManager.SyncOnLaunch();
        Scheduler.Instance.Start();
        Scheduler.Instance.MinuteTick += UpdatePillStatus;
        FocusService.Instance.StateChanged += UpdatePillStatus;
        _media.Updated += _ => Dispatcher.BeginInvoke(UpdatePillStatus);

        _ = WeatherLocation.EnsureLocatedAsync();

        Closed += (_, _) =>
        {
            RemoveOutsideClickHook();
            KeyboardBlocker.Set(false); // fail-safe: typing returns when the island exits
            StopPillGlow();
            _clock.Stop();
            _activity.Dispose();
            _media.Dispose();
            _tray.Visible = false;
            _tray.Dispose();
            UnregisterGlobalHotkey();
        };
    }

    // ------------------------------------------------------------------ nav

    private void BuildNavigation()
    {
        foreach (var section in Sections)
        {
            var id = section.Id;
            var button = Iconography.IconButton(section.Icon, section.Tooltip, 29, () => Navigate(id), iconSize: 16, corner: 9);
            button.Tag = id;
            Navigation.Children.Add(button);
            _buttons[id] = button;
        }
        StyleActiveNav();
    }

    private void BuildHeaderButtons()
    {
        // Same geometry as the nav rail: transparent, radius 9, hover overlay.
        Ui.SetCorner(RefreshButton, 9);
        Ui.SetCorner(AwakeButton, 9);
        Ui.SetCorner(SettingsButton, 9);
        RefreshButton.Background = Brushes.Transparent;
        AwakeButton.Background = Brushes.Transparent;
        SettingsButton.Background = Brushes.Transparent;
        Iconography.SetIcon(RefreshButton, "refresh", 16, Theme.Dim);
        Iconography.SetIcon(AwakeButton, "coffee", 16, Theme.Dim);
        Iconography.SetIcon(SettingsButton, "settings", 16, Theme.Dim);
        RefreshAwakeButton();
        RefreshKeyboardButton();
    }

    private void StyleActiveNav()
    {
        var accent = Theme.AccentColor;
        var tint = new SolidColorBrush(Color.FromArgb(64, accent.R, accent.G, accent.B));
        var accentBrush = Theme.Accent;
        foreach (var (id, button) in _buttons)
        {
            var active = id == _current;
            button.Background = active ? tint : Brushes.Transparent;
            button.Content = Iconography.Draw(
                Sections.First(s => s.Id == id).Icon, 16, 1.9, active ? accentBrush : Theme.Dim);
        }
    }

    private int _navSeq;

    private UIElement BuildModule(string name) => name switch
    {
        "home"      => RenderHome(),
        "dashboard" => RenderDashboard(),
        "shelf"     => RenderShelf(),
        "tasks"     => RenderTasks(),
        "notes"     => RenderNotes(),
        "focus"     => RenderFocus(),
        "media"     => RenderMedia(),
        "wellbeing" => RenderWellbeing(),
        "screen"    => RenderScreen(),
        "weather"   => RenderWeather(),
        "network"   => RenderNetwork(),
        "ai"        => RenderAi(),
        "utilities" => RenderUtilities(),
        "settings"  => RenderSettings(),
        _ => RenderDashboard()
    };

    private void Navigate(string name)
    {
        _current = name;
        StyleActiveNav();
        // Content is reconstructed lazily; other tabs keep no polling behind them.
        var content = BuildModule(name);
        if (PortableData.Settings.ReducedMotion)
        {
            ContentHost.Content = content;
            return;
        }
        // Crossfade module switches the way the reference video does:
        // the outgoing view fades quickly, the incoming view eases in.
        var seq = ++_navSeq;
        var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150))
        { EasingFunction = MotionEase, FillBehavior = FillBehavior.Stop };
        if (ContentHost.Content is UIElement && ContentHost.Opacity > 0.95)
        {
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(90))
            { FillBehavior = FillBehavior.Stop };
            fadeOut.Completed += (_, _) =>
            {
                if (seq != _navSeq) return; // superseded by a faster click
                ContentHost.Content = content;
                ContentHost.BeginAnimation(UIElement.OpacityProperty, fadeIn);
            };
            ContentHost.BeginAnimation(UIElement.OpacityProperty, fadeOut);
        }
        else
        {
            ContentHost.Content = content;
            ContentHost.BeginAnimation(UIElement.OpacityProperty, fadeIn);
        }
    }

    private void UpdateClock()
    {
        var now = DateTime.Now;
        PillClock.Text = now.ToString("HH:mm");
        HeaderClock.Text = now.ToString("HH:mm");
        HeaderDate.Text = FormatDate(now);
        // Keep dashboard/weather honest across the minute boundary without polling.
        if (_expanded && (_current == "dashboard" || _current == "home")) Navigate(_current);
    }

    internal static string FormatDate(DateTime now)
    {
        var culture = System.Globalization.CultureInfo.CurrentCulture;
        var name = culture.Name.ToLowerInvariant();
        if (name.StartsWith("zh") || name.StartsWith("ja"))
            return now.ToString("M", culture) + now.ToString("ddd", culture);
        return now.ToString("ddd, MMM d", culture);
    }

    // ------------------------------------------------------- expand/collapse

    private void Toggle() { if (_expanded) Collapse(); else Expand(); }

    private static readonly CubicEase MotionEase = new() { EasingMode = EasingMode.EaseOut };
    private static readonly TimeSpan MotionDuration = TimeSpan.FromMilliseconds(240);

    private void Expand()
    {
        if (_expanded) { Activate(); return; }
        _expanded = true;
        var x = Left - (PanelWidth - PillWidth) / 2;
        var y = Top;
        Pill.Visibility = Visibility.Collapsed;
        Panel.Visibility = Visibility.Visible;
        Panel.Opacity = 1;
        StopPillGlow();
        EnsureMediaStarted();
        SnapWindow(PanelWidth, PanelHeight, x, y);
        ScheduleGeometryGuard(PanelWidth, PanelHeight);
        Activate();
        InstallOutsideClickHook();
    }

    private void Collapse()
    {
        if (!_expanded) return;
        _expanded = false;
        RemoveOutsideClickHook();
        var x = Left + (Width - PillWidth) / 2;
        var y = Top;
        Panel.Visibility = Visibility.Collapsed;
        Panel.Opacity = 1;
        Pill.Visibility = Visibility.Visible;
        Pill.Opacity = 1;
        SnapWindow(PillWidth, PillHeight, x, y);
        StartPillGlow();
        // Self-healing: whatever races with the swap (rapid clicks, focus
        // loss, DPI changes), the resting pill must end up exactly
        // pill-sized and topmost.
        ScheduleGeometryGuard(PillWidth, PillHeight);
        UpdatePillStatus();
    }

    private void ScheduleGeometryGuard(double width, double height)
    {
        var delay = TimeSpan.FromMilliseconds(30);
        _ = System.Threading.Tasks.Task.Delay(delay).ContinueWith(_ =>
            Dispatcher.BeginInvoke(() => ForceGeometry(width, height)));
    }

    // ---------------- minimized pill border glow ----------------

    private RotateTransform? _pillGlowTransform;
    private System.Windows.Media.Effects.DropShadowEffect? _pillGlowShadow;
    private System.Windows.Threading.DispatcherTimer? _glowBreath;

    /// <summary>A soft accent streak travels around the pill border with a
    /// breathing outer glow — active only while minimized, subtle by design,
    /// and skipped entirely under reduced motion.</summary>
    private void StartPillGlow()
    {
        StopPillGlow();
        if (PortableData.Settings.ReducedMotion)
        {
            Pill.BorderBrush = Theme.PanelEdge;
            return;
        }
        var accent = Theme.AccentColor;
        var baseColor = (Color)System.Windows.Media.ColorConverter.ConvertFromString(Theme.IsDark ? "#262C36" : "#C7CEDA");
        var brush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 1),
            GradientStops =
            {
                new GradientStop(accent, 0.0),
                new GradientStop(baseColor, 0.22),
                new GradientStop(baseColor, 0.78),
                new GradientStop(Color.FromArgb(160, accent.R, accent.G, accent.B), 1.0),
            }
        };
        _pillGlowTransform = new RotateTransform(0, 0.5, 0.5);
        brush.RelativeTransform = _pillGlowTransform;
        Pill.BorderBrush = brush;
        _pillGlowTransform.BeginAnimation(RotateTransform.AngleProperty,
            new DoubleAnimation(0, 360, TimeSpan.FromMilliseconds(4200)) { RepeatBehavior = RepeatBehavior.Forever });

        _pillGlowShadow = new System.Windows.Media.Effects.DropShadowEffect
        {
            Color = accent,
            BlurRadius = 16,
            ShadowDepth = 0,
            Opacity = 0.35
        };
        Pill.Effect = _pillGlowShadow;
        _glowBreath = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(60) };
        var phase = 0.0;
        _glowBreath.Tick += (_, _) =>
        {
            phase += 0.06;
            if (_pillGlowShadow != null)
                _pillGlowShadow.Opacity = 0.3 + 0.18 * (0.5 + 0.5 * Math.Sin(phase));
        };
        _glowBreath.Start();
    }

    private void StopPillGlow()
    {
        _glowBreath?.Stop();
        _glowBreath = null;
        _pillGlowTransform?.BeginAnimation(RotateTransform.AngleProperty, null);
        _pillGlowTransform = null;
        _pillGlowShadow = null;
        Pill.Effect = null;
        Pill.BorderBrush = Theme.PanelEdge;
    }

    /// <summary>Final word on window geometry for the current state; also
    /// re-asserts always-on-top after fullscreen apps steal it.</summary>
    private void ForceGeometry(double width, double height)
    {
        BeginAnimation(WidthProperty, null);
        BeginAnimation(HeightProperty, null);
        BeginAnimation(LeftProperty, null);
        BeginAnimation(TopProperty, null);
        Width = width;
        Height = height;
        ClampToVisibleMonitor();
        Topmost = true;
    }

    // ---------------- click outside collapses (popup behavior) ----------------

    private IntPtr _outsideHook;
    private Native.HookProc? _outsideHookProc;
    private bool _outsideLoggedClick;

    private static readonly HashSet<string> ShellTrayClasses = new(StringComparer.Ordinal)
    {
        "Shell_TrayWnd", "NotifyIconOverflowWindow", "TopLevelWindowForOverflowXamlIsland", "#32768"
    };

    private void InstallOutsideClickHook()
    {
        if (!PortableData.Settings.CollapseOnOutsideClick || _outsideHook != IntPtr.Zero) return;
        _outsideHookProc = OutsideClickHook;
        _outsideLoggedClick = false;
        _outsideHook = Native.SetWindowsHookEx(Native.WH_MOUSE_LL, _outsideHookProc,
            Native.GetModuleHandle(null), 0);
        PortableData.Log("HOOK", _outsideHook == IntPtr.Zero ? "install failed" : "armed");
    }

    private void RemoveOutsideClickHook()
    {
        if (_outsideHook == IntPtr.Zero) return;
        Native.UnhookWindowsHookEx(_outsideHook);
        _outsideHook = IntPtr.Zero;
        _outsideHookProc = null;
    }

    private IntPtr OutsideClickHook(int code, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            if (code >= 0 && _expanded && !_pinned
                && PortableData.Settings.CollapseOnOutsideClick
                && (wParam.ToInt64() is Native.WM_LBUTTONDOWN_LL or Native.WM_RBUTTONDOWN_LL))
            {
                var info = Marshal.PtrToStructure<Native.MSLLHOOKSTRUCT>(lParam);
                var at = new Native.POINTI { X = info.pt.X, Y = info.pt.Y };
                var hitWindow = Native.WindowFromPoint(at);
                var cls = new System.Text.StringBuilder(64);
                Native.GetClassName(hitWindow, cls, 64);
                if (!_outsideLoggedClick)
                {
                    _outsideLoggedClick = true;
                    PortableData.Log("HOOK", $"click at {at.X},{at.Y} cls={cls} expanded={_expanded}");
                }
                if (!ShellTrayClasses.Contains(cls.ToString()))
                {
                    var self = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                    var rect = new Native.RECTL();
                    if (self != IntPtr.Zero && Native.GetWindowRectIntPtr(self, out rect)
                        && (at.X < rect.L || at.X > rect.R || at.Y < rect.T || at.Y > rect.B))
                    {
                        // Non-eating: the click continues to the app below.
                        Dispatcher.BeginInvoke((Action)(() =>
                        {
                            if (_expanded && !_pinned && PortableData.Settings.CollapseOnOutsideClick)
                                Collapse();
                        }));
                    }
                }
            }
        }
        catch { /* the hook must never break mouse input */ }
        return Native.CallNextHookEx(_outsideHook, code, wParam, lParam);
    }

    private void SnapWindow(double width, double height, double x, double y)
    {
        // Instant transitions per user preference — no window animation.
        BeginAnimation(WidthProperty, null);
        BeginAnimation(HeightProperty, null);
        BeginAnimation(LeftProperty, null);
        BeginAnimation(TopProperty, null);
        var area = SystemParameters.WorkArea;
        x = Math.Clamp(x, area.Left, Math.Max(area.Left, area.Right - width));
        y = Math.Clamp(y, area.Top, Math.Max(area.Top, area.Bottom - height));
        Width = width; Height = height; Left = x; Top = y;
        Topmost = true;
        UpdateLayout();
    }

    private void ClampToVisibleMonitor()
    {
        var area = SystemParameters.WorkArea;
        if (Left < area.Left - 30 || Left > area.Right - 30)
            Left = area.Left + (area.Width - PillWidth) / 2;
        if (Top < area.Top || Top > area.Bottom - PillHeight)
            Top = area.Top + 6;
    }

    private void SavePosition()
    {
        // Store the equivalent collapsed pill position, not the expanded x.
        PortableData.Settings.Left = _expanded ? Left + (Width - PillWidth) / 2 : Left;
        PortableData.Settings.Top = Top;
        PortableData.SaveSettings();
    }

    private void DragPanel()
    {
        var x = Left; var y = Top;
        BeginAnimation(LeftProperty, null); BeginAnimation(TopProperty, null);
        Left = x; Top = y;
        try { DragMove(); }
        catch (InvalidOperationException) { return; }
        SavePosition();
    }

    private static bool IsInsideButton(DependencyObject? source)
    {
        while (source != null)
        {
            if (source is System.Windows.Controls.Primitives.ButtonBase) return true;
            source = System.Windows.Media.VisualTreeHelper.GetParent(source);
        }
        return false;
    }

    private void PillDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        _pillPressed = true;
        _pressPosition = e.GetPosition(this);
        Pill.CaptureMouse();
        e.Handled = true;
    }

    private void PillMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_pillPressed || e.LeftButton != MouseButtonState.Pressed) return;
        var p = e.GetPosition(this);
        if (Math.Abs(p.X - _pressPosition.X) <= SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(p.Y - _pressPosition.Y) <= SystemParameters.MinimumVerticalDragDistance) return;
        _pillPressed = false;
        Pill.ReleaseMouseCapture();
        DragPanel();
        e.Handled = true;
    }

    private void PillUp(object sender, MouseButtonEventArgs e)
    {
        var click = _pillPressed;
        _pillPressed = false;
        if (Pill.IsMouseCaptured) Pill.ReleaseMouseCapture();
        if (click) Expand();
        e.Handled = true;
    }

    private void HeaderDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        // Clicking header background/gaps drags; clicks that began on a button
        // never reach here so tab clicks are never swallowed.
        if (IsInsideButton(e.OriginalSource as DependencyObject)) return;
        DragPanel();
        e.Handled = true;
    }

    // -------------------------------------------------------- pill status

    private void UpdatePillStatus()
    {
        if (PillDot is null) return;
        var focus = FocusService.Instance;
        string status;
        Brush dot = Theme.Accent; // follows the chosen accent color
        if (focus.Running)
        {
            status = (focus.StopwatchMode ? "Stopwatch " : "Focus ") + focus.Clock;
            dot = Theme.Good;
        }
        else if (_media.LastSession is { IsValid: true } session && session.PlaybackState == "playing")
        {
            status = session.Title;
        }
        else if (Toggles.KeepAwakeOn) { status = "Keeping awake"; dot = Theme.Orange; }
        else if (KeyboardBlocker.IsOn) { status = "Keyboard off"; dot = Theme.Orange; }
        else
        {
            var title = PortableData.Settings.PillTitle;
            status = string.IsNullOrWhiteSpace(title) ? "Aether Island" : title.Trim();
        }
        PillStatus.Text = status;
        PillDot.Fill = dot;
        PillStatus.Foreground = Theme.Dim;
    }

    internal void UpdatePillStatusPublic() => UpdatePillStatus();

    internal string CurrentModule => _current;
    internal bool IsExpandedPublic => _expanded;

    internal void PinStateChanged()
    {
        _pinned = PortableData.Settings.Pinned;
        PortableData.SaveSettings();
    }

    internal void ResetPositionPublic() => ResetPosition();

    private void WindowKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _expanded && !_pinned)
        {
            Collapse();
            e.Handled = true;
        }
    }

    private void EnsureMediaStarted()
    {
        if (_mediaStarted) return;
        _mediaStarted = true;
        _ = _media.StartAsync();
    }

    // -------------------------------------------------------- header actions

    private void RefreshClick(object sender, RoutedEventArgs e)
    {
        // A genuine refresh of the visible module; weather and network fetch
        // only when their module is the one visible.
        WeatherLocation.ResetCache();
        Navigate(_current);
    }

    private void AwakeClick(object sender, RoutedEventArgs e) => ToggleKeepAwake();

    internal void ToggleKeepAwake()
    {
        var on = !Toggles.KeepAwakeOn;
        Toggles.SetKeepAwake(on);
        RefreshAwakeButton();
        UpdatePillStatus();
    }

    internal void RefreshAwakeButton()
        => Iconography.SetIcon(AwakeButton, "coffee", 16, Toggles.KeepAwakeOn ? Theme.Orange : Theme.Dim);

    private void KeyboardButtonClick(object sender, RoutedEventArgs e) => ToggleKeyboardBlock();

    internal void ToggleKeyboardBlock()
    {
        var on = !KeyboardBlocker.IsOn;
        KeyboardBlocker.Set(on);
        RefreshKeyboardButton();
        UpdatePillStatus();
        App.NotifyBalloon("Keyboard", on
            ? "Laptop keyboard is OFF. Click the keyboard icon to restore it."
            : "Laptop keyboard is back on.");
    }

    internal void RefreshKeyboardButton()
    {
        KeyboardButton.ToolTip = KeyboardBlocker.IsOn
            ? "Laptop keyboard is OFF — click to restore"
            : "Turn off the laptop keyboard";
        Iconography.SetIcon(KeyboardButton, "keyboard", 16, KeyboardBlocker.IsOn ? Theme.Orange : Theme.Dim);
    }

    private void SettingsClick(object sender, RoutedEventArgs e) => Navigate("settings");

    // ---------------------------------------------------------------- theme

    internal void ApplyThemeChrome()
    {
        Panel.Background = Theme.Bg;
        Panel.BorderBrush = Theme.PanelEdge;
        Pill.Background = Theme.Bg;
        Pill.BorderBrush = Theme.PanelEdge;
        ContentCard.Background = Theme.Card;
        ContentCard.BorderBrush = Theme.CardEdge;
        PillClock.Foreground = Theme.Text;
        PillStatus.Foreground = Theme.Dim;
        HeaderClock.Foreground = Theme.Text;
        HeaderDate.Foreground = Theme.Dim;
        StyleActiveNav();
        BuildHeaderButtons();
        UpdatePillStatus();
    }

    internal void ThemeChanged()
    {
        Theme.Apply(PortableData.Settings.Theme);
        ApplyThemeChrome();
        if (!_expanded) StartPillGlow(); // rebuild glow with the new theme/accent
        Navigate(_current);
    }

    // ------------------------------------------------------------- hotkeys

    private const int HotkeyId = 0xA17C;

    private void RegisterGlobalHotkey()
    {
        var source = System.Windows.Interop.HwndSource.FromHwnd(
            new System.Windows.Interop.WindowInteropHelper(this).EnsureHandle());
        source?.AddHook(WndProc);
        Native.RegisterHotKey(source!.Handle, HotkeyId, Native.MOD_CONTROL | Native.MOD_ALT, 0x20 /* Space */);
    }

    private void UnregisterGlobalHotkey()
    {
        try
        {
            var helper = new System.Windows.Interop.WindowInteropHelper(this);
            if (helper.Handle != IntPtr.Zero) Native.UnregisterHotKey(helper.Handle, HotkeyId);
        }
        catch { /* window already gone */ }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == Native.WM_HOTKEY && wParam.ToInt32() == HotkeyId)
        {
            Toggle();
            handled = true;
        }
        return IntPtr.Zero;
    }

    // ---------------------------------------------------------------- tray

    private System.Windows.Forms.NotifyIcon BuildTray()
    {
        var tray = new System.Windows.Forms.NotifyIcon
        {
            Text = "Aether Island",
            Visible = true,
            Icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath
                ?? Process.GetCurrentProcess().MainModule?.FileName ?? "")
        };
        tray.MouseUp += (_, e) =>
        {
            if (e.Button == System.Windows.Forms.MouseButtons.Left)
                Dispatcher.BeginInvoke((Action)Toggle);
        };
        var menu = new System.Windows.Forms.ContextMenuStrip();

        var startup = new System.Windows.Forms.ToolStripMenuItem("Launch at Windows startup")
        {
            Checked = PortableData.Settings.LaunchAtStartup,
            CheckOnClick = true
        };
        startup.Click += (_, _) =>
        {
            PortableData.Settings.LaunchAtStartup = startup.Checked;
            PortableData.SaveSettings();
            StartupManager.Apply(startup.Checked);
        };

        menu.Items.Add("Open / collapse", null, (_, _) => Dispatcher.BeginInvoke((Action)Toggle));
        menu.Items.Add("Refresh", null, (_, _) => Dispatcher.BeginInvoke(() =>
        {
            WeatherLocation.ResetCache();
            Navigate(_current);
        }));
        menu.Items.Add("Settings", null, (_, _) => Dispatcher.BeginInvoke(() => { Expand(); Navigate("settings"); }));
        menu.Items.Add(startup);
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add("Reset position", null, (_, _) => Dispatcher.BeginInvoke(ResetPosition));
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add("Exit Aether Island", null, (_, _) => System.Windows.Application.Current.Shutdown());
        tray.ContextMenuStrip = menu;
        return tray;
    }

    private void ResetPosition()
    {
        PortableData.Settings.Left = null;
        PortableData.Settings.Top = null;
        var wa = SystemParameters.WorkArea;
        var x = wa.Left + (wa.Width - PillWidth) / 2;
        var y = wa.Top + 6;
        if (_expanded) x -= (PanelWidth - PillWidth) / 2;
        Left = x; Top = y;
        PortableData.SaveSettings();
    }

    internal ActivityTracker Activity => _activity;
    internal SmtcMediaWatcher Media => _media;

    public void ShowBalloon(string title, string message)
        => _tray.ShowBalloonTip(4000, title, message, System.Windows.Forms.ToolTipIcon.Info);

    // --------------------------------------------------- CI shell selfcheck

    /// <summary>
    /// Renders the pill and every expanded module to PNG files for visual
    /// acceptance. Used by the local preview and CI evidence steps.
    /// </summary>
    public async Task CaptureAllViews(string dir)
    {
        Directory.CreateDirectory(dir);
        static async Task Settle() { for (var i = 0; i < 6; i++) { await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); await Task.Delay(60); } }

        Expand();
        await Settle();
        Capture("00-expanded-" + _current, dir);
        foreach (var section in Sections)
        {
            Navigate(section.Id);
            await Settle();
            Capture(section.Id, dir);
        }
        Navigate("settings");
        await Settle();
        Capture("settings", dir);
        Collapse();
        await Settle();
        Capture("pill-collapsed", dir);
        await Settle();
    }

    private void Capture(string name, string dir)
    {
        var zoom = 1.5;
        var rtb = new RenderTargetBitmap(
            (int)(ActualWidth * zoom), (int)(ActualHeight * zoom), 96 * zoom, 96 * zoom, System.Windows.Media.PixelFormats.Pbgra32);
        rtb.Render(this);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(rtb));
        using var stream = File.Create(Path.Combine(dir, name + ".png"));
        encoder.Save(stream);
    }

    /// <summary>Verifies real WPF layout and routed tab actions on demand.</summary>
    public void VerifyShellInteractions()
    {
        var motionWasReduced = PortableData.Settings.ReducedMotion;
        PortableData.Settings.ReducedMotion = true; // geometry checks need final bounds
        try
        {
        Expand();
        UpdateLayout();
        if (!_expanded || Panel.Visibility != Visibility.Visible || Pill.Visibility != Visibility.Collapsed)
            throw new InvalidOperationException("Expand failed to switch the island state.");
        if (Width < 900 || Height < 200 || ContentHost.ActualHeight < 80)
            throw new InvalidOperationException($"Expanded panel clipped: {Width}x{Height}, content {ContentHost.ActualHeight}.");
        if (Navigation.Children.Count != Sections.Length)
            throw new InvalidOperationException("Navigation button count differs from expected modules.");
        foreach (var section in Sections)
        {
            var button = _buttons[section.Id];
            if (button.Content is not Viewbox)
                throw new InvalidOperationException($"The {section.Id} icon is not a vector control.");
            button.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent, button));
            UpdateLayout();
            if (_current != section.Id || ContentHost.Content is null)
                throw new InvalidOperationException($"Navigation failed for {section.Id}.");
        }
        Collapse();
        UpdateLayout();
        if (_expanded || Pill.Visibility != Visibility.Visible || Panel.Visibility != Visibility.Collapsed || Width > 220)
            throw new InvalidOperationException("Collapse did not restore the compact pill.");
        }
        finally { PortableData.Settings.ReducedMotion = motionWasReduced; }
    }
}

/// <summary>Weather location resolution with IP auto-detection fallback.</summary>
internal static class WeatherLocation
{
    private static DateTime _lastAutoAttempt = DateTime.MinValue;

    public static void ResetCache() { /* per-launch state only */ }

    public static async Task EnsureLocatedAsync()
    {
        var s = PortableData.Settings;
        if (s.WeatherLatitude is not null || !WeatherLocationService.IpLookupAvailable) return;
        if (DateTime.UtcNow - _lastAutoAttempt < TimeSpan.FromMinutes(30)) return;
        _lastAutoAttempt = DateTime.UtcNow;
        var located = await WeatherLocationService.LocateByIpAsync();
        if (located is null) return;
        s.WeatherLatitude = located.Value.latitude;
        s.WeatherLongitude = located.Value.longitude;
        s.WeatherLocation = located.Value.label;
        s.WeatherAutoLocated = true;
        PortableData.SaveSettings();
        MainWindow.ReRenderIfVisible("dashboard");
        MainWindow.ReRenderIfVisible("weather");
        MainWindow.ReRenderIfVisible("home");
    }
}

/// <summary>IP geolocation via ipapi.co (free tier, HTTPS, no key). Failure is honest: stays unlocated.</summary>
internal static class WeatherLocationService
{
    public static bool IpLookupAvailable => true;

    public static async Task<(double latitude, double longitude, string label)?> LocateByIpAsync()
    {
        try
        {
            // Primary: ipapi.co; fallback: ipinfo.io. Both free, HTTPS, keyless.
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(7) };
            double lat; double lon; string city; string? country = null;
            try
            {
                using var response = await http.GetAsync("https://ipapi.co/json/");
                if (!response.IsSuccessStatusCode) throw new HttpRequestException("ipapi " + (int)response.StatusCode);
                using var doc = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                var root = doc.RootElement;
                lat = root.GetProperty("latitude").GetDouble();
                lon = root.GetProperty("longitude").GetDouble();
                city = root.TryGetProperty("city", out var cityEl) ? cityEl.GetString() ?? "" : "";
                country = root.TryGetProperty("country_name", out var countryEl) ? countryEl.GetString() : null;
                if (city.Length == 0) throw new KeyNotFoundException("city");
            }
            catch
            {
                using var response = await http.GetAsync("https://ipinfo.io/json");
                if (!response.IsSuccessStatusCode) return null;
                using var doc = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                var root = doc.RootElement;
                var loc = (root.TryGetProperty("loc", out var locEl) ? locEl.GetString() : null)?.Split(',') ?? Array.Empty<string>();
                if (loc.Length != 2 || !double.TryParse(loc[0], System.Globalization.CultureInfo.InvariantCulture, out lat)
                    || !double.TryParse(loc[1], System.Globalization.CultureInfo.InvariantCulture, out lon)) return null;
                city = root.TryGetProperty("city", out var cityEl) ? cityEl.GetString() ?? "" : "";
                country = root.TryGetProperty("country", out var cEl) ? cEl.GetString() : null;
                if (city.Length == 0) city = "Your area";
            }
            return (lat, lon, string.IsNullOrEmpty(country) ? city : $"{city}, {country}");
        }
        catch (Exception ex)
        {
            PortableData.Log("WEATHER", "auto-locate: " + ex.Message);
            return null;
        }
    }
}
