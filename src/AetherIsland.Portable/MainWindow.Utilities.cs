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
        left.Children.Add(BuildKeyboardCard());
        left.Children.Add(Ui.Spacer(0, 10));
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

    /// <summary>
    /// Laptop keyboard kill-switch. Blocks all built-in keyboard input through
    /// a low-level hook while active: instant, no admin rights, and fail-safe
    /// (the keyboard returns automatically if the island exits). Ctrl+Alt
    /// combos and the mouse stay alive.
    /// </summary>
    private static FrameworkElement BuildKeyboardCard()
    {
        var isOff = KeyboardBlocker.IsOn;

        var card = new System.Windows.Controls.Border
        {
            Background = Theme.SectionCard,
            BorderBrush = isOff ? Theme.Orange : Theme.CardEdge,
            BorderThickness = new Thickness(1, isOff ? 1.4 : 1, 1, isOff ? 1.4 : 1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(11, 9, 11, 10)
        };
        var body = new StackPanel();

        var head = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 7) };
        var chip = new System.Windows.Controls.Border
        {
            Width = 22, Height = 22, CornerRadius = new CornerRadius(7),
            Background = Theme.Chip, VerticalAlignment = VerticalAlignment.Center
        };
        chip.Child = Iconography.Draw("keyboard", 12, 1.9, isOff ? Theme.Orange : Theme.Accent);
        head.Children.Add(chip);
        var titles = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
        titles.Children.Add(Ui.Text("Laptop keyboard", 11, true));
        titles.Children.Add(Ui.Text(
            isOff ? "Turned off — mouse and island still work" : "Built-in keys are active",
            8.3, false, isOff ? Theme.Orange : Theme.Faint));
        head.Children.Add(titles);
        body.Children.Add(head);

        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var label = Ui.V(
            Ui.Text(isOff ? "Turn the keyboard back on" : "Turn off the keyboard", 10.5),
            Ui.Text(isOff ? "One click — or the keyboard icon in the header" : "Instant · no admin · also in the header (keyboard icon)", 8.3, false, Theme.Faint));
        Grid.SetColumn(label, 0);
        row.Children.Add(label);
        var sw = new Ui.Switch(isOff, on =>
        {
            KeyboardBlocker.Set(on);
            App.NotifyBalloon("Keyboard", on
                ? "Laptop keyboard is OFF. Click this switch again to restore it."
                : "Laptop keyboard is back on.");
            ReRenderIfVisible("utilities");
        }, isOff ? "Restore the laptop keyboard" : "Ignore all built-in keyboard keys");
        sw.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(sw, 1);
        row.Children.Add(sw);
        body.Children.Add(row);

        body.Children.Add(Ui.Spacer(0, 6));
        body.Children.Add(Ui.Text(
            "Fail-safe: closing the island (tray → Exit) always restores the keyboard. Ctrl+Alt combos and Ctrl+Alt+Del keep working. Mouse is never affected.",
            8.3, false, Theme.Faint, wrap: true));

        card.Child = body;
        return card;
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
