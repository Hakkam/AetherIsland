using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace AetherIsland.Portable;

public partial class MainWindow
{
    // ---------------------------------------------------------------- focus

    private static UIElement RenderFocus()
    {
        var focus = FocusService.Instance;
        var stack = new StackPanel { Margin = new Thickness(16, 6, 12, 2), VerticalAlignment = VerticalAlignment.Center };

        var modes = Ui.Segmented(new[] { "Pomodoro 25", "Break 5", "Custom 15", "Stopwatch" }, focus.Mode switch
        {
            "pomodoro" => 0, "break" => 1, "custom" => 2, _ => 3
        }, i =>
        {
            switch (i)
            {
                case 0: focus.Configure("pomodoro", 25); break;
                case 1: focus.Configure("break", 5); break;
                case 2: focus.Configure("custom", 15); break;
                default: focus.Configure("stopwatch", 0); break;
            }
            ReRenderIfVisible("focus");
        });
        stack.Children.Add(modes);
        stack.Children.Add(Ui.Spacer(0, 10));

        var clock = Ui.Text(focus.Clock, 40, true, Theme.Text, TextAlignment.Center);
        clock.HorizontalAlignment = HorizontalAlignment.Center;
        stack.Children.Add(clock);
        stack.Children.Add(Ui.Spacer(0, 6));

        var progress = Ui.Bar(focus.StopwatchMode ? 0 : 1 - focus.SecondsLeft / (25.0 * 60), 260, 5);
        progress.HorizontalAlignment = HorizontalAlignment.Center;
        stack.Children.Add(progress);
        stack.Children.Add(Ui.Spacer(0, 10));

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        buttons.Children.Add(Ui.TextButton(focus.Running ? "Pause" : "Start", () =>
        {
            focus.StartPause();
            ReRenderIfVisible("focus");
        }, "Start or pause", 10.5, background: Theme.Accent, foreground: Ui.Solid("#0B0E12"), padding: 16));
        buttons.Children.Add(Ui.TextButton("Reset", () =>
        {
            focus.Reset(focus.Mode switch { "pomodoro" => 25, "break" => 5, "custom" => 15, _ => 0 });
            ReRenderIfVisible("focus");
        }, "Reset timer", 10.5, background: Theme.Chip, foreground: Theme.Text, padding: 14));
        stack.Children.Add(buttons);

        stack.Children.Add(Ui.Spacer(0, 8));
        stack.Children.Add(Ui.Text($"{focus.CompletedPomodoros} pomodoro(s) completed today", 9.5, false, Theme.Dim, TextAlignment.Center));

        // Keep the clock live while visible; the timer stops itself when not running.
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        timer.Tick += (_, _) =>
        {
            clock.Text = focus.Clock;
            if (!IsModuleVisible("focus") || (!focus.Running && !FocusAlive))
            {
                timer.Stop();
            }
        };
        timer.Start();
        _liveFocusTimers.Add(timer);
        return Ui.WrapPage(stack);
    }

    private static bool FocusAlive => FocusService.Instance.Running;
    private static readonly List<DispatcherTimer> _liveFocusTimers = new();

    private static bool IsModuleVisible(string module) => Win.CurrentModule == module && Win.IsExpandedPublic;

    // ------------------------------------------------------------ wellbeing

    private static UIElement RenderWellbeing()
    {
        var s = PortableData.Settings;
        var stack = new StackPanel { Margin = new Thickness(16, 6, 12, 2), VerticalAlignment = VerticalAlignment.Center };

        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.Children.Add(Ui.SectionTitle("wellbeing", "Wellbeing reminders"));
        header.Children.Add(Ui.Segmented(new[] { s.WellbeingEnabled ? "Enabled" : "Disabled" }, 0, _ =>
        {
            s.WellbeingEnabled = !s.WellbeingEnabled;
            PortableData.SaveSettings();
            ReRenderIfVisible("wellbeing");
        }));
        stack.Children.Add(header);
        stack.Children.Add(Ui.Spacer(0, 8));

        if (!s.WellbeingEnabled)
        {
            stack.Children.Add(Ui.Text("Reminders are off. Enable above to get eye-break and movement nudges.", 10.5, false, Theme.Dim));
        }
        else
        {
            stack.Children.Add(Ui.Text($"Eye break every {s.EyeBreakMinutes} min · movement every {s.StandMinutes} min. Reminders arrive as tray notifications.", 10.5, false, Theme.Dim));
        }
        stack.Children.Add(Ui.Spacer(0, 12));

        // Hydration tracker (manual logging)
        if (s.HydrationDay != DateTime.Today) { s.HydrationDay = DateTime.Today; s.HydrationCups = 0; }
        var cupsRow = new StackPanel { Orientation = Orientation.Horizontal };
        cupsRow.Children.Add(Ui.Text("Hydration   ", 11, true));
        for (var i = 0; i < 8; i++)
        {
            var index = i;
            var cup = new ContentControl
            {
                Content = Iconography.Draw("drop", 15, 1.9, i < s.HydrationCups ? Theme.Accent : Theme.Track),
                Margin = new Thickness(2, 0, 2, 0),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            cup.MouseLeftButtonDown += (_, _) =>
            {
                s.HydrationCups = index + 1 == s.HydrationCups ? index : index + 1;
                PortableData.SaveSettings();
                ReRenderIfVisible("wellbeing");
            };
            cupsRow.Children.Add(cup);
        }
        cupsRow.Children.Add(Ui.Text($"   {s.HydrationCups}/8 cups today", 9.5, false, Theme.Dim));
        stack.Children.Add(cupsRow);
        stack.Children.Add(Ui.Spacer(0, 12));

        // Breathing timer (4-7-8)
        var breath = Ui.TextButton("Start 4-7-8 breathing", () => App.NotifyBalloon("Breathing", "Inhale 4s · hold 7s · exhale 8s. Repeat three times."),
            "Guided breathing", 10, background: Theme.Chip, foreground: Theme.Text, padding: 12);
        stack.Children.Add(breath);
        return Ui.WrapPage(stack);
    }
}
