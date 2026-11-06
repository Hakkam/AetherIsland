using System.Windows;
using System.Windows.Threading;

namespace AetherIsland.Portable;

public partial class App : System.Windows.Application
{
    private Mutex? _mutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        PortableData.Initialize();
        _mutex = new Mutex(true, "AetherIsland.Portable." + PortableData.InstanceKey, out var created);
        if (!created) { Shutdown(); return; }
        DispatcherUnhandledException += (_, args) =>
        {
            PortableData.Log("UI", args.Exception.ToString());
            MessageBox.Show("Aether Island error. Details: Data\\logs\\app.log\n" + args.Exception.Message,
                "Aether Island", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };
        System.AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            PortableData.Log("FATAL", args.ExceptionObject.ToString() ?? "Unknown");

        if (!string.Equals(PortableData.Settings.Language, "en", StringComparison.Ordinal))
            AetherIsland.Core.Localization.Loc.SetLanguage(PortableData.Settings.Language);

        var window = new MainWindow();
        MainWindow = window;
        window.Show();

        // Explicit UI smoke mode runs only when requested by the Windows
        // acceptance workflow. This checks actual WPF layout and routed
        // button handlers, which compilation-only tests cannot cover.
        if (e.Args.Any(a => string.Equals(a, "--ui-selfcheck", StringComparison.OrdinalIgnoreCase)))
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    window.VerifyShellInteractions();
                    PortableData.Log("UI SELF CHECK", "Passed");
                    Shutdown(0);
                }
                catch (Exception ex)
                {
                    PortableData.Log("UI SELF CHECK", ex.ToString());
                    Shutdown(1);
                }
            }), DispatcherPriority.ApplicationIdle);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _mutex?.Dispose();
        base.OnExit(e);
    }

    /// <summary>Balloon notifications surfaced through the tray icon.</summary>
    public static void NotifyBalloon(string title, string message)
    {
        try
        {
            if (Current.MainWindow is MainWindow win) win.ShowBalloon(title, message);
        }
        catch { /* notification must never crash the app */ }
    }
}
