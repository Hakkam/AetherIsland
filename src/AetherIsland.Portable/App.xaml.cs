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
    }
    protected override void OnExit(ExitEventArgs e) { _mutex?.Dispose(); base.OnExit(e); }
}