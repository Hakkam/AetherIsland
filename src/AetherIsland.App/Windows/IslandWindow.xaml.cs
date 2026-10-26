using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using AetherIsland.App.Services;
using AetherIsland.App.ViewModels;
using AetherIsland.Core;
using AetherIsland.Windows.Interop;

namespace AetherIsland.App.Windows;

public partial class IslandWindow : Window
{
    private readonly ShellViewModel _vm = new();
    private bool _expanded;
    private bool _pinned;
    private readonly DispatcherTimer _fullscreenTimer;
    private readonly DispatcherTimer _contextTimer;
    private bool _hiddenForFullscreen;
    private string _lastLanguage = Loc.Language;

    private const double PillWidth = 176;
    private const double PillHeight = 50;   // includes shadow margin
    private double ExpandedWidth => AppServices.Settings.Appearance.ExpandedWidth;
    private double ExpandedHeight => AppServices.Settings.Appearance.ExpandedHeight;

    public IslandWindow()
    {
        InitializeComponent();
        DataContext = _vm;
        ApplyLanguageDirection();
        AppServices.Theme.ThemeChanged += ApplyLanguageDirection;
        Closed += (_, _) => AppServices.Theme.ThemeChanged -= ApplyLanguageDirection;

        _pinned = AppServices.Settings.Behavior.Pinned;
        PinButton.Content = _pinned ? "\uE77A" : "\uE718";

        _fullscreenTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _fullscreenTimer.Tick += (_, _) => CheckFullscreen();

        _contextTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(6) };
        _contextTimer.Tick += (_, _) => ClearContext();

        AppServices.Notify.IslandContext += e => Dispatcher.Invoke(() => ShowContext(e.Icon, e.Message));

        Deactivated += (_, _) =>
        {
            if (_expanded && !_pinned && AppServices.Settings.Behavior.CollapseOnDeactivate
                && !IsContextMenuOpen()) Collapse();
        };
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape && _expanded) { Collapse(); e.Handled = true; }
        };

        Width = PillWidth;
        Height = PillHeight;
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        Closed += (_, _) => Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        ((App)Application.Current).BuildTrayMenu(this);
        _fullscreenTimer.Start();
    }

    private bool IsContextMenuOpen() => ContextMenu?.IsOpen == true;

    private void ApplyLanguageDirection()
    {
        FlowDirection = ThemeService.FlowDirectionForCurrentLanguage();
        if (_lastLanguage != Loc.Language)
        {
            _lastLanguage = Loc.Language;
            _vm.RebuildNavVisibility();
        }
    }

    // ---------- state transitions ----------

    public void Toggle()
    {
        if (_expanded && !_hiddenForFullscreen) Collapse();
        else Expand();
    }

    public void Expand()
    {
        _hiddenForFullscreen = false;
        Visibility = Visibility.Visible;
        if (_expanded) return;
        _expanded = true;
        PillRoot.Visibility = Visibility.Collapsed;
        ExpandedRoot.Visibility = Visibility.Visible;
        RootBorder.CornerRadius = new CornerRadius(AppServices.Settings.Appearance.CornerRadius);
        AnimateTo(ExpandedWidth, ExpandedHeight);
        Activate();
        _vm.NavigateTo(_vm.CurrentNav?.Id ?? "dashboard");
    }

    public void Collapse()
    {
        if (!_expanded) { PositionPill(instant: false); return; }
        _expanded = false;
        ExpandedRoot.Visibility = Visibility.Collapsed;
        PillRoot.Visibility = Visibility.Visible;
        RootBorder.CornerRadius = new CornerRadius(20);
        AnimateTo(PillWidth, PillHeight);
    }

    private void ShowContext(string glyph, string message)
    {
        _vm.PillStatusGlyph = glyph;
        _vm.PillStatusText = message;
        _vm.HasContextEvent = true;
        _contextTimer.Stop();
        _contextTimer.Start();
        if (!_expanded) PositionPill(instant: false);
    }

    private void ClearContext()
    {
        _contextTimer.Stop();
        _vm.HasContextEvent = false;
    }

    private void AnimateTo(double width, double height)
    {
        var (targetLeft, targetTop) = TargetPosition(width);

        var duration = AppServices.Settings.Behavior.ReducedMotion
            ? TimeSpan.Zero
            : TimeSpan.FromMilliseconds(210);

        var wa = new DoubleAnimation(width, duration) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
        var ha = new DoubleAnimation(height, duration) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
        var la = new DoubleAnimation(targetLeft, duration) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
        var ta = new DoubleAnimation(targetTop, duration) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };

        BeginAnimation(WidthProperty, wa);
        BeginAnimation(HeightProperty, ha);
        BeginAnimation(LeftProperty, la);
        BeginAnimation(TopProperty, ta);

        if (duration == TimeSpan.Zero)
        {
            Width = width; Height = height; Left = targetLeft; Top = targetTop;
        }
    }

    private void PositionPill(bool instant)
    {
        if (instant)
        {
            Width = PillWidth;
            Height = PillHeight;
            (Left, Top) = TargetPosition(PillWidth);
        }
        else AnimateTo(PillWidth, PillHeight);
    }

    private (double left, double top) TargetPosition(double width)
    {
        var work = MonitorService.SelectedWorkArea(AppServices.Settings.Behavior.MonitorDeviceName);
        var source = PresentationSource.FromVisual(this);
        var transform = source?.CompositionTarget?.TransformFromDevice ?? System.Windows.Media.Matrix.Identity;
        var topLeft = transform.Transform(new System.Windows.Point(work.Left, work.Top));
        var bottomRight = transform.Transform(new System.Windows.Point(work.Right, work.Bottom));
        return (topLeft.X + Math.Max(0, bottomRight.X - topLeft.X - width) / 2, topLeft.Y + 6);
    }

    public void Reposition() => AnimateTo(_expanded ? ExpandedWidth : PillWidth,
        _expanded ? ExpandedHeight : PillHeight);

    private void OnDisplaySettingsChanged(object? sender, EventArgs e) =>
        Dispatcher.BeginInvoke((Action)Reposition);

    // ---------- input ----------

    private void Pill_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 1) Expand();
    }

    private void Rail_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject d && FindAncestor<RadioButton>(d) is not null) return;
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            try { DragMove(); } catch { }
        }
    }

    private static T? FindAncestor<T>(DependencyObject d) where T : class
    {
        while (d is not null)
        {
            if (d is T t) return t;
            d = System.Windows.Media.VisualTreeHelper.GetParent(d);
        }
        return null;
    }

    private void Pin_Click(object sender, RoutedEventArgs e) => SetPinned(!_pinned);

    public void SetPinned(bool pinned)
    {
        _pinned = pinned;
        AppServices.Settings.Behavior.Pinned = pinned;
        AppServices.SettingsStore.Save(AppServices.Settings);
        PinButton.Content = pinned ? "\uE77A" : "\uE718";
        if (!pinned) Collapse();
    }

    private void Settings_Click(object sender, RoutedEventArgs e) => SettingsWindow.ShowOnce();

    public void NavigateTo(string id)
    {
        if (!_expanded) Expand();
        _vm.NavigateTo(id);
    }

    public void ToggleTimer() => _vm.Focus.ToggleFromHotkey();

    public void RebuildNavigation() => _vm.RebuildNavVisibility();

    // ---------- fullscreen awareness ----------

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    private void CheckFullscreen()
    {
        try
        {
            var rule = AppServices.Settings.Behavior.FullscreenRule;
            if (rule == "stay") { if (_hiddenForFullscreen) { _hiddenForFullscreen = false; Visibility = Visibility.Visible; } return; }

            var hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return;
            GetWindowThreadProcessId(hwnd, out var pid);
            if (pid == 0) return;

            // Idle screensaver / secure desktop has no window rect match; use rect vs work area.
            var hMonitor = MonitorFromWindow(hwnd, 1); // MONITOR_DEFAULTTONEAREST
            var mi = new MONITORINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<MONITORINFO>() };
            if (!GetMonitorInfo(hMonitor, ref mi)) return;
            if (!MonitorService.IsSelectedMonitor(hMonitor, AppServices.Settings.Behavior.MonitorDeviceName))
            {
                if (_hiddenForFullscreen) { _hiddenForFullscreen = false; Visibility = Visibility.Visible; }
                return;
            }

            GetWindowRect(hwnd, out var rect);
            var fullscreen = rect.Left <= mi.rcMonitor.Left && rect.Top <= mi.rcMonitor.Top &&
                             rect.Right >= mi.rcMonitor.Right && rect.Bottom >= mi.rcMonitor.Bottom;

            // ignore our own window
            if (hwnd == new WindowInteropHelper(this).Handle) fullscreen = false;

            if (fullscreen && !_hiddenForFullscreen)
            {
                _hiddenForFullscreen = true;
                Visibility = Visibility.Hidden;
            }
            else if (!fullscreen && _hiddenForFullscreen)
            {
                _hiddenForFullscreen = false;
                Visibility = Visibility.Visible;
            }
        }
        catch { }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO info);

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;
        const int GWL_EXSTYLE = -20;
        const int WS_EX_TOOLWINDOW = 0x80;
        var exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_TOOLWINDOW);
        PositionPill(instant: true);
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong(IntPtr hwnd, int index);

    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
    private static extern int SetWindowLong(IntPtr hwnd, int index, int value);
}

