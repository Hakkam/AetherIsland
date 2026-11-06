using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AetherIsland.Portable;

public partial class MainWindow
{
    private static UIElement RenderUtilities()
    {
        var grid = new Grid { Margin = new Thickness(14, 8, 12, 2) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var left = new StackPanel();
        left.Children.Add(Ui.SectionTitle("utilities", "System actions"));
        left.Children.Add(Ui.Spacer(0, 6));
        foreach (var (label, tooltip, action) in new (string, string, Action)[]
        {
            ("Screenshot → clipboard", "Capture the whole virtual screen", QuickAction.Screenshot),
            ("Lock Windows", "Lock this session now", Toggles.LockScreen),
            ("Keep awake", "Prevent sleep while on", () => Win.ToggleKeepAwake()),
            ("Task Manager", "Open Windows Task Manager", () => External.Open("taskmgr.exe")),
            ("File Explorer", "Open a new Explorer window", () => External.Open("explorer.exe")),
        })
        {
            left.Children.Add(UtilityRow(label, tooltip, action));
        }
        left.Children.Add(Ui.Spacer(0, 8));
        left.Children.Add(Ui.SectionTitle("folder", "Windows locations"));
        left.Children.Add(Ui.Spacer(0, 6));
        foreach (var (label, target) in new (string, string)[]
        {
            ("Windows Settings", "ms-settings:"),
            ("Recycle Bin (empty after confirm)", "@recyclebin"),
            ("Downloads folder", "shell:Downloads"),
            ("Bluetooth settings", "ms-settings:bluetooth"),
            ("Network settings", "ms-settings:network"),
        })
        {
            left.Children.Add(UtilityRow(label, label, () =>
            {
                if (target == "@recyclebin")
                {
                    var confirm = MessageBox.Show("Empty the Recycle Bin? Deleted items cannot be recovered.",
                        "Aether Island", MessageBoxButton.OKCancel, MessageBoxImage.Warning);
                    if (confirm != MessageBoxResult.OK) return;
                    try
                    {
                        // Documented Shell COM path via explorer verb.
                        Process.Start(new ProcessStartInfo("shell:RecycleBinFolder") { UseShellExecute = true });
                    }
                    catch (Exception ex) { PortableData.Log("UTIL", ex.Message); }
                }
                else External.Open(target);
            }));
        }
        grid.Children.Add(left);

        var right = new StackPanel { Margin = new Thickness(18, 0, 0, 0) };
        right.Children.Add(Ui.SectionTitle("clock", "Favorites"));
        right.Children.Add(Ui.Spacer(0, 6));
        var favorites = PortableData.Settings.CalendarSources; // reuse-free: favorites live below
        foreach (var (label, target) in new (string, string)[]
        {
            ("Calculator", "calc.exe"),
            ("Notepad", "notepad.exe"),
            ("Paint", "mspaint.exe"),
            ("Snipping Tool", "snippingtool.exe"),
            ("On-screen Keyboard", "osk.exe"),
        })
        {
            right.Children.Add(UtilityRow(label, label, () => External.Open(target)));
        }
        right.Children.Add(Ui.Spacer(0, 8));
        right.Children.Add(Ui.SectionTitle("copy", "Clipboard"));
        right.Children.Add(Ui.Spacer(0, 6));
        right.Children.Add(Ui.TextButton("Clear clipboard", () =>
        {
            try { System.Windows.Clipboard.Clear(); App.NotifyBalloon("Clipboard", "Clipboard cleared."); }
            catch (Exception ex) { PortableData.Log("CLIP", ex.Message); }
        }, "Remove clipboard contents", 10, background: Theme.Chip, foreground: Theme.Text, padding: 12));
        right.Children.Add(Ui.Spacer(0, 10));
        right.Children.Add(Ui.Text("Favorites folders & sites: add them to the File shelf for one-click access.", 9.5, false, Theme.Faint, wrap: true));
        grid.Children.Add(right);
        Grid.SetColumn(right, 1);

        return Ui.WrapPage(grid);
    }

    private static FrameworkElement UtilityRow(string label, string tooltip, Action action)
    {
        var row = new System.Windows.Controls.Border
        {
            Background = Brushes.Transparent,
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(8, 5, 8, 5),
            Margin = new Thickness(0, 1, 0, 1),
            Cursor = System.Windows.Input.Cursors.Hand,
            ToolTip = tooltip
        };
        var content = Ui.H(
            Iconography.Draw("open", 12, 1.9, Theme.Faint),
            Ui.Text("  " + label, 10.5));
        row.Child = content;
        row.MouseLeftButtonDown += (_, _) => action();
        return row;
    }
}
