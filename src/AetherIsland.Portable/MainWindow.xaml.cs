using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using AetherIsland.Integrations.Media;

namespace AetherIsland.Portable;

/// <summary>
/// An explicit, no-focus-loss auto-collapse state machine. A click expands;
/// a move past the OS drag threshold drags, not expands. No fullscreen polling.
/// </summary>
public partial class MainWindow : Window
{
    private const double PillWidth = 188, PillHeight = 48, PanelWidth = 945, PanelHeight = 256;
    private bool _expanded;
    private bool _pinned;
    private bool _pillPressed;
    private Point _pressPosition;
    private string _current = "dashboard";
    private readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromMinutes(1) };
    private readonly Dictionary<string, Button> _buttons = new();
    private readonly System.Windows.Forms.NotifyIcon _tray;
    private readonly SmtcMediaWatcher _media = new();
    private bool _mediaStarted;
    private readonly ActivityTracker _activity = new();
    private readonly string[] _sections =
    {
        "dashboard", "screen", "media", "shelf", "tasks", "notes",
        "focus", "weather", "network", "ai"
    };
    private readonly Dictionary<string, string> _titles = new()
    {
        ["dashboard"] = "Dashboard", ["screen"] = "Screen time", ["media"] = "Now playing",
        ["shelf"] = "File shelf", ["tasks"] = "Tasks", ["notes"] = "Notes",
        ["focus"] = "Focus timer", ["weather"] = "Weather", ["network"] = "Network",
        ["ai"] = "Verified AI quota"
    };

    public MainWindow()
    {
        InitializeComponent();
        _pinned = PortableData.Settings.Pinned;
        PinButton.Content = Iconography.Draw(_pinned ? "pinned" : "pin", 14);
        SettingsButton.Content = Iconography.Draw("settings", 14);
        CollapseButton.Content = Iconography.Draw("collapse", 14);
        var wa = SystemParameters.WorkArea;
        Left = PortableData.Settings.Left ?? wa.Left + (wa.Width - PillWidth) / 2;
        Top = PortableData.Settings.Top ?? wa.Top + 7;
        ClampToVisibleMonitor();
        BuildNavigation();
        _tray = BuildTray();
        _clock.Tick += (_, _) => UpdateClock();
        _clock.Start();
        UpdateClock();
        Navigate("dashboard");  // Prepare panel before first expansion.
        Closed += (_, _) =>
        {
            _clock.Stop();
            _activity.Dispose();
            _media.Dispose();
            _tray.Visible = false;
            _tray.Dispose();
        };
    }

    private System.Windows.Forms.NotifyIcon BuildTray()
    {
        var tray = new System.Windows.Forms.NotifyIcon
        {
            Text = "Aether Island Portable",
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
        menu.Items.Add("Open / collapse", null, (_, _) => Toggle());
        menu.Items.Add("Pin / unpin", null, (_, _) => TogglePin());
        menu.Items.Add("Reset position", null, (_, _) =>
        {
            PortableData.Settings.Left = null;
            PortableData.Settings.Top = null;
            var wa = SystemParameters.WorkArea;
            var x = wa.Left + (wa.Width - PillWidth) / 2;
            var y = wa.Top + 7;
            if (_expanded) x -= (PanelWidth - PillWidth) / 2;
            Left = x; Top = y;
            PortableData.SaveSettings();
        });
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add("Open published releases", null, (_, _) =>
            OpenUrl("https://github.com/Hakkam/AetherIsland/releases"));
        menu.Items.Add("Settings", null, (_, _) => { Expand(); Navigate("settings"); });
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add("Exit Aether Island", null, (_, _) => System.Windows.Application.Current.Shutdown());
        tray.ContextMenuStrip = menu;
        return tray;
    }

    private void BuildNavigation()
    {
        foreach (var id in _sections)
        {
            var button = Iconography.Button(id, _titles[id], 34);
            button.Tag = id;
            button.Click += (_, _) => Navigate(id);
            Navigation.Children.Add(button);
            _buttons[id] = button;
        }
    }

    private void Navigate(string name)
    {
        _current = name;
        ModuleTitle.Text = _titles.GetValueOrDefault(name, "Aether Island");
        foreach (var (id, button) in _buttons)
            button.Background = id == name
                ? new SolidColorBrush(Color.FromRgb(49, 57, 71)) : Brushes.Transparent;
        // Content is reconstructed lazily, without polling other tabs.
        ContentHost.Content = name switch
        {
            "dashboard" => RenderDashboard(),
            "screen" => RenderScreenTime(),
            "media" => RenderMedia(),
            "shelf" => RenderShelf(),
            "tasks" => RenderTasks(),
            "notes" => RenderNotes(),
            "focus" => RenderFocus(),
            "weather" => RenderWeather(),
            "network" => RenderNetwork(),
            "ai" => RenderAi(),
            "settings" => RenderSettings(),
            _ => RenderDashboard()
        };
    }

    private void UpdateClock()
    {
        var now = DateTime.Now;
        PillClock.Text = now.ToString("HH:mm", CultureInfo.CurrentCulture);
        HeaderClock.Text = now.ToString("HH:mm", CultureInfo.CurrentCulture);
        HeaderDate.Text = now.ToString("MMM dd ddd", CultureInfo.CurrentCulture);
        if (_expanded && (_current == "dashboard" || _current == "screen"))
            Navigate(_current);
    }

    private void Toggle() { if (_expanded) Collapse(); else Expand(); }

    private void Expand()
    {
        if (_expanded) { Activate(); return; }
        _expanded = true;
        var x = Left - (PanelWidth - PillWidth) / 2;
        var y = Top;
        Panel.Visibility = Visibility.Visible;
        Pill.Visibility = Visibility.Collapsed;
        AnimateWindow(PanelWidth, PanelHeight, x, y);
        Activate();
    }

    private void Collapse()
    {
        if (!_expanded) return;
        _expanded = false;
        var x = Left + (Width - PillWidth) / 2;
        var y = Top;
        Panel.Visibility = Visibility.Collapsed;
        Pill.Visibility = Visibility.Visible;
        AnimateWindow(PillWidth, PillHeight, x, y);
    }

    private void AnimateWindow(double width, double height, double x, double y)
    {
        // Layered WPF windows can report stale hit-test geometry during
        // Width/Height dependency-property animations. Snap the HWND to its
        // final bounds first; animate only panel content opacity.
        BeginAnimation(WidthProperty, null);
        BeginAnimation(HeightProperty, null);
        BeginAnimation(LeftProperty, null);
        BeginAnimation(TopProperty, null);
        var area = SystemParameters.WorkArea;
        x = Math.Clamp(x, area.Left, Math.Max(area.Left, area.Right - width));
        y = Math.Clamp(y, area.Top, Math.Max(area.Top, area.Bottom - height));
        Width = width; Height = height; Left = x; Top = y;
        UpdateLayout();
        if (!PortableData.Settings.ReducedMotion)
        {
            var content = _expanded ? (UIElement)Panel : Pill;
            content.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.4, 1.0,
                TimeSpan.FromMilliseconds(150)) { FillBehavior = FillBehavior.Stop });
        }
    }

    private void ClampToVisibleMonitor()
    {
        var area = SystemParameters.WorkArea;
        if (Left < area.Left - 30 || Left > area.Right - 30)
            Left = area.Left + (area.Width - PillWidth) / 2;
        if (Top < area.Top || Top > area.Bottom - PillHeight)
            Top = area.Top + 7;
    }

    private void SavePosition()
    {
        // Store the equivalent collapsed pill position, not the expanded panel's x.
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
        // Only the explicit drag grip handles mouse-down; tab clicks never bubble
        // into DragMove and cannot be swallowed by a parent grid.
        DragPanel();
        e.Handled = true;
    }

    /// <summary>Verifies real WPF layout and routed tab actions on the Windows runner.</summary>
    public void VerifyShellInteractions()
    {
        Expand();
        UpdateLayout();
        if (!_expanded || Panel.Visibility != Visibility.Visible || Pill.Visibility != Visibility.Collapsed)
            throw new InvalidOperationException("Expand failed to switch the island state.");
        if (Width < 900 || Height < 200 || ContentHost.ActualHeight < 80)
            throw new InvalidOperationException($"Expanded panel clipped: {Width}x{Height}, content {ContentHost.ActualHeight}.");
        if (Navigation.Children.Count != _sections.Length)
            throw new InvalidOperationException("Navigation button count differs from expected modules.");
        foreach (var id in _sections)
        {
            var button = _buttons[id];
            if (button.Content is not System.Windows.Controls.Viewbox)
                throw new InvalidOperationException($"The {id} icon is not a vector control.");
            button.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent, button));
            UpdateLayout();
            if (_current != id || ModuleTitle.Text != _titles[id] || ContentHost.Content is null)
                throw new InvalidOperationException($"Navigation failed for {id}.");
        }
        Collapse();
        UpdateLayout();
        if (_expanded || Pill.Visibility != Visibility.Visible || Panel.Visibility != Visibility.Collapsed || Width > 220)
            throw new InvalidOperationException("Collapse did not restore the compact pill.");
    }

    private void TogglePin()
    {
        _pinned = !_pinned;
        PortableData.Settings.Pinned = _pinned;
        PinButton.Content = Iconography.Draw(_pinned ? "pinned" : "pin", 14);
        PortableData.SaveSettings();
    }

    private void PinClick(object sender, RoutedEventArgs e) => TogglePin();
    private void SettingsClick(object sender, RoutedEventArgs e) => Navigate("settings");
    private void CollapseClick(object sender, RoutedEventArgs e) => Collapse();

    private static void OpenUrl(string target)
    {
        try { Process.Start(new ProcessStartInfo(target) { UseShellExecute = true }); }
        catch (Exception ex) { PortableData.Log("LAUNCH", ex.Message); }
    }
}
