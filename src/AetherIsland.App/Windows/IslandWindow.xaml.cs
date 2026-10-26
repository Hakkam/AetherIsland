using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
    private bool _pillPressActive;
    private System.Windows.Point _pillPressPoint;

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
        var (targetLeft, targetTop) = TargetPosition(width, height);

        // Animations must not keep the Left/Top dependency properties controlled
        // after completion, or WPF's native DragMove will fail to persist movement.
        var fromWidth = Width;
        var fromHeight = Height;
        var fromLeft = Left;
        var fromTop = Top;
        BeginAnimation(WidthProperty, null);
        BeginAnimation(HeightProperty, null);
        BeginAnimation(LeftProperty, null);
        BeginAnimation(TopProperty, null);
        Width = width; Height = height; Left = targetLeft; Top = targetTop;

        if (AppServices.Settings.Behavior.ReducedMotion) return;
        var duration = TimeSpan.FromMilliseconds(210);
        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };

        DoubleAnimation Slide(double from, double to) => new(from, to, duration)
        {
            EasingFunction = easing,
            FillBehavior = FillBehavior.Stop
        };
        BeginAnimation(WidthProperty, Slide(fromWidth, width));
        BeginAnimation(HeightProperty, Slide(fromHeight, height));
        BeginAnimation(LeftProperty, Slide(fromLeft, targetLeft));
        BeginAnimation(TopProperty, Slide(fromTop, targetTop));
    }

    private void PositionPill(bool instant)
    {
        if (!instant) { AnimateTo(PillWidth, PillHeight); return; }
        BeginAnimation(WidthProperty, null);
        BeginAnimation(HeightProperty, null);
        BeginAnimation(LeftProperty, null);
        BeginAnimation(TopProperty, null);
        Width = PillWidth;
        Height = PillHeight;
        (Left, Top) = TargetPosition(PillWidth, PillHeight);
    }

    private (double left, double top) TargetPosition(double width, double height)
    {
        var work = MonitorService.SelectedWorkArea(AppServices.Settings.Behavior.MonitorDeviceName);
        var source = PresentationSource.FromVisual(this);
        var transform = source?.CompositionTarget?.TransformFromDevice ?? System.Windows.Media.Matrix.Identity;
        var topLeft = transform.Transform(new System.Windows.Point(work.Left, work.Top));
        var bottomRight = transform.Transform(new System.Windows.Point(work.Right, work.Bottom));
        var behavior = AppServices.Settings.Behavior;
        var left = topLeft.X + (bottomRight.X - topLeft.X - width) / 2
                   + (behavior.IslandCenterOffsetX ?? 0);
        var top = topLeft.Y + (behavior.IslandTopOffsetY ?? 6);
        return (Math.Clamp(left, topLeft.X, Math.Max(topLeft.X, bottomRight.X - width)),
                Math.Clamp(top, topLeft.Y, Math.Max(topLeft.Y, bottomRight.Y - height)));
    }

    public void ResetPosition()
    {
        AppServices.Settings.Behavior.IslandCenterOffsetX = null;
        AppServices.Settings.Behavior.IslandTopOffsetY = null;
        AppServices.SettingsStore.Save(AppServices.Settings);
        Reposition();
    }

    private void DragIsland()
    {
        // Remember the animated on-screen position before letting Windows move it.
        var x = Left;
        var y = Top;
        BeginAnimation(LeftProperty, null);
        BeginAnimation(TopProperty, null);
        Left = x; Top = y;
        try { DragMove(); }
        catch (InvalidOperationException) { return; }
        SaveDraggedPosition();
    }

    private void SaveDraggedPosition()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        var behavior = AppServices.Settings.Behavior;
        var monitor = MonitorService.DeviceNameForWindow(hwnd);
        if (!string.IsNullOrWhiteSpace(monitor))
            behavior.MonitorDeviceName = monitor;

        var work = MonitorService.SelectedWorkArea(behavior.MonitorDeviceName);
        var transform = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformFromDevice
                        ?? System.Windows.Media.Matrix.Identity;
        var origin = transform.Transform(new System.Windows.Point(work.Left, work.Top));
        var edge = transform.Transform(new System.Windows.Point(work.Right, work.Bottom));
        behavior.IslandCenterOffsetX = (Left + Width / 2) - (origin.X + (edge.X - origin.X) / 2);
        behavior.IslandTopOffsetY = Top - origin.Y;
        AppServices.SettingsStore.Save(AppServices.Settings);
        Reposition(); // clamp to the work area, keeping the new anchored position
    }

    public void Reposition() => AnimateTo(_expanded ? ExpandedWidth : PillWidth,
        _expanded ? ExpandedHeight : PillHeight);

    private void OnDisplaySettingsChanged(object? sender, EventArgs e) =>
        Dispatcher.BeginInvoke((Action)Reposition);

    // ---------- input ----------

    private void Pill_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        _pillPressActive = true;
        _pillPressPoint = e.GetPosition(this);
        PillRoot.CaptureMouse();
        e.Handled = true;
    }

    private void Pill_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_pillPressActive || e.LeftButton != MouseButtonState.Pressed) return;
        var pointer = e.GetPosition(this);
        if (Math.Abs(pointer.X - _pillPressPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(pointer.Y - _pillPressPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        _pillPressActive = false;
        PillRoot.ReleaseMouseCapture();
        DragIsland();
        e.Handled = true;
    }

    private void Pill_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        var click = _pillPressActive;
        _pillPressActive = false;
        if (PillRoot.IsMouseCaptured) PillRoot.ReleaseMouseCapture();
        if (click) Expand();
        e.Handled = true;
    }

    private void Rail_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        // Do not steal mouse input from navigation, pin/settings, or scroll controls.
        if (e.OriginalSource is DependencyObject d &&
            (FindAncestor<ButtonBase>(d) is not null ||
             FindAncestor<ScrollBar>(d) is not null))
            return;
        DragIsland();
        e.Handled = true;
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

