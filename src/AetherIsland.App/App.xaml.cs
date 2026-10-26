using System.Windows;
using System.Windows.Controls;
using AetherIsland.App.Services;
using AetherIsland.App.Windows;
using AetherIsland.Core;

namespace AetherIsland.App;

public partial class App : Application
{
    private static Mutex? _singleInstanceMutex;
    public static App? Instance { get; private set; }
    public event Action? ToggleRequested;

    protected override void OnStartup(StartupEventArgs e)
    {
        Instance = this;

        _singleInstanceMutex = new Mutex(true, "AetherIsland.SingleInstance", out var isNew);
        if (!isNew)
        {
            Shutdown();
            return;
        }

        DispatcherUnhandledException += (_, args) =>
        {
            System.Diagnostics.Trace.WriteLine("AetherIsland: " + args.Exception.Message);
            args.Handled = true; // a floating utility should not crash the shell experience
        };

        AppServices.Build();
        AppServices.Theme.Apply();

        base.OnStartup(e);

        var window = new IslandWindow();
        AppServices.Hotkeys.Attach(window);
        RegisterHotkeys();
        AppServices.Hotkeys.HotkeyPressed += OnHotkey;

        window.Show();
        CurrentMainWindow = window;

        StartServices();

        if (!AppServices.Settings.General.OnboardingCompleted)
        {
            var onboarding = new OnboardingWindow();
            onboarding.Owner = null;
            onboarding.ShowDialog();
        }
    }

    public Window? CurrentMainWindow { get; private set; }

    private void RegisterHotkeys()
    {
        var s = AppServices.Settings.Shortcuts;
        if (!AppServices.Hotkeys.TryRegister("toggle", s.ToggleIsland))
            System.Diagnostics.Trace.WriteLine($"Hotkey conflict: {s.ToggleIsland}");
        AppServices.Hotkeys.TryRegister("ai", s.OpenAiCenter);
        AppServices.Hotkeys.TryRegister("shelf", s.OpenFileShelf);
        AppServices.Hotkeys.TryRegister("timer", s.StartPauseTimer);
    }

    private void OnHotkey(string name)
    {
        Dispatcher.Invoke(() =>
        {
            switch (name)
            {
                case "toggle": ToggleRequested?.Invoke(); break;
                case "ai": if (CurrentMainWindow is IslandWindow w) w.NavigateTo("ai"); break;
                case "shelf": if (CurrentMainWindow is IslandWindow w2) w2.NavigateTo("fileshelf"); break;
                case "timer": if (CurrentMainWindow is IslandWindow w3) w3.ToggleTimer(); break;
            }
        });
    }

    private void StartServices()
    {
        _ = AppServices.Media.StartAsync();
        AppServices.ScreenTime.Start();
        AppServices.Health.Start();
        _ = AppServices.Weather.RefreshAsync();
        _ = AppServices.Ai.RefreshAsync();
    }

    public void RestartHotkeys()
    {
        AppServices.Hotkeys.UnregisterAll();
        RegisterHotkeys();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        AppServices.ScreenTime.FlushNow();
        AppServices.ScreenTime.Pause();
        AppServices.Tray.Dispose();
        AppServices.Hotkeys.Dispose();
        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }

    public void BuildTrayMenu(Window island)
    {
        var menu = new ContextMenu();

        MenuItem Item(string header, RoutedEventHandler onClick, bool isChecked = false)
        {
            var mi = new MenuItem { Header = header, IsCheckable = false };
            if (isChecked) mi.Icon = new TextBlock { Text = "✓" };
            mi.Click += onClick;
            return mi;
        }

        menu.Items.Add(Item(Loc.T("tray.open"), (_, _) => (island as IslandWindow)?.Expand()));
        menu.Items.Add(Item(Loc.T("tray.pin"), (_, _) =>
            (island as IslandWindow)?.SetPinned(!AppServices.Settings.Behavior.Pinned)));
        menu.Items.Add(new Separator());
        menu.Items.Add(Item(Loc.T("tray.refreshAi"), (_, _) => _ = AppServices.Ai.RefreshAsync()));
        menu.Items.Add(Item(Loc.T("tray.settings"), (_, _) => SettingsWindow.ShowOnce()));
        menu.Items.Add(new Separator());
        menu.Items.Add(Item(Loc.T("common.exit"), (_, _) => Shutdown()));

        AppServices.Tray.Initialize(menu);
        AppServices.Tray.LeftClick += () => Dispatcher.Invoke(() =>
        {
            if (island is IslandWindow iw) iw.Toggle();
        });
    }
}
