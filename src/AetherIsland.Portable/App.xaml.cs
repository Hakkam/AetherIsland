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
        _mutex = new Mutex(true, "AetherIsland.Portable.V2." + PortableData.InstanceKey, out var created);
        if (!created) { Shutdown(); return; }
        DispatcherUnhandledException += (_, args) =>
        {
            PortableData.Log("UI", args.Exception.ToString());
            MessageBox.Show("Aether Island error. Details: Data\\logs\\app.log\n" + args.Exception.Message,
                "Aether Island", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) => PortableData.Log("FATAL", args.ExceptionObject.ToString() ?? "Unknown");
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
    protected override void OnExit(ExitEventArgs e) { _mutex?.Dispose(); base.OnExit(e); }
}