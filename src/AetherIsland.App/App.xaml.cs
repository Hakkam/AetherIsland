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
                case "toggle": if (CurrentMainWindow is IslandWindow toggleWindow) toggleWindow.Toggle(); break;
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

        MenuItem Item(string key, string glyph, RoutedEventHandler onClick)
        {
            var mi = new MenuItem
            {
                Header = Loc.T(key),
                Tag = key,
                Icon = new TextBlock
                {
                    Text = glyph,
                    FontFamily = new System.Windows.Media.FontFamily("Segoe MDL2 Assets"),
                    FontSize = 13,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            mi.Click += onClick;
            return mi;
        }

        menu.Items.Add(Item("tray.open", "\uE8A7", (_, _) => (island as IslandWindow)?.Expand()));
        menu.Items.Add(Item("tray.pin", "\uE718", (_, _) =>
            (island as IslandWindow)?.SetPinned(!AppServices.Settings.Behavior.Pinned)));
        menu.Items.Add(Item("tray.resetPosition", "\uE7AD", (_, _) =>
            (island as IslandWindow)?.ResetPosition()));
        menu.Items.Add(new Separator());
        menu.Items.Add(Item("tray.refreshAi", "\uE72C", (_, _) => _ = AppServices.Ai.RefreshAsync()));
        menu.Items.Add(Item("tray.checkUpdates", "\uE895", async (_, _) => await CheckForUpdatesAsync()));
        menu.Items.Add(Item("tray.settings", "\uE713", (_, _) => SettingsWindow.ShowOnce()));
        menu.Items.Add(new Separator());
        menu.Items.Add(Item("common.exit", "\uE8BB", (_, _) => Shutdown()));

        // The tray menu remains open across theme/language changes; refresh labels
        // and the pin status on every right-click, without recreating native icons.
        menu.Opened += (_, _) =>
        {
            menu.FlowDirection = ThemeService.FlowDirectionForCurrentLanguage();
            foreach (var mi in menu.Items.OfType<MenuItem>())
                if (mi.Tag is string key)
                    mi.Header = Loc.T(key == "tray.pin" && AppServices.Settings.Behavior.Pinned
                        ? "tray.unpin" : key);
        };

        AppServices.Tray.Initialize(menu);
        AppServices.Tray.LeftClick += () => Dispatcher.Invoke(() =>
        {
            if (island is IslandWindow iw) iw.Toggle();
        });
    }

    private static async Task CheckForUpdatesAsync()
    {
        try
        {
            var result = await AppUpdateService.CheckAsync();
            switch (result.State)
            {
                case UpdateCheckState.UpdateAvailable:
                    if (MessageBox.Show(Loc.TF("tray.updateAvailable", result.LatestVersion ?? ""),
                        "Aether Island", MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
                            "https://github.com/Hakkam/AetherIsland/releases/latest") { UseShellExecute = true });
                    break;
                case UpdateCheckState.UpToDate:
                    MessageBox.Show(Loc.T("tray.upToDate"), "Aether Island", MessageBoxButton.OK, MessageBoxImage.Information);
                    break;
                case UpdateCheckState.NoRelease:
                    MessageBox.Show(Loc.T("tray.noRelease"), "Aether Island", MessageBoxButton.OK, MessageBoxImage.Information);
                    break;
                default:
                    MessageBox.Show(Loc.T("tray.updateFailed"), "Aether Island", MessageBoxButton.OK, MessageBoxImage.Warning);
                    break;
            }
        }
        catch
        {
            MessageBox.Show(Loc.T("tray.updateFailed"), "Aether Island", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
