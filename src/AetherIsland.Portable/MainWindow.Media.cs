using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using AetherIsland.Integrations.Media;
using AetherIsland.Windows;

namespace AetherIsland.Portable;

public partial class MainWindow
{
    private TextBlock? _mediaTitle;
    private TextBlock? _mediaArtist;
    private UIElement RenderMedia()
    {
        _mediaTitle = Ui.Text("No active media session", 19, true);
        _mediaArtist = Ui.Text("Open a compatible media player.", 11, false, Ui.Dim);
        var v = Ui.V(Ui.Title("Now playing · Windows media session"),
            new Border { Height = 13 }, _mediaTitle, _mediaArtist,
            new Border { Height = 16 },
            Ui.H(Ui.Button("⏮", () => PowerHelper.SendMediaKey(0xB1)),
                Ui.Button("⏯  Play / Pause", async () => await _media.TogglePlayPauseAsync()),
                Ui.Button("⏭", () => PowerHelper.SendMediaKey(0xB0)),
                Ui.Button("Refresh", async () => await _media.RefreshAsync())));
        void ShowMedia(MediaSessionInfo? session)
        {
            if (_mediaTitle == null || _mediaArtist == null) return;
            _mediaTitle.Text = session?.Title is { Length: > 0 } title ? title : "No active media session";
            _mediaArtist.Text = session?.Artist is { Length: > 0 } artist
                ? artist + " · " + session?.PlaybackState
                : "Start a compatible player to see playback metadata.";
        }
        if (!_mediaStarted)
        {
            _mediaStarted = true;
            _media.Updated += info => Dispatcher.BeginInvoke((Action)(() => ShowMedia(info)));
            _ = _media.StartAsync();
        }
        else _ = _media.RefreshAsync();
        ShowMedia(_media.LastSession);
        return Ui.WrapPage(v);
    }

    private UIElement RenderScreenTime()
    {
        if (!PortableData.Settings.ScreenTimeConsent)
        {
            return Ui.WrapPage(Ui.V(Ui.Title("Screen time · Off by default"),
                new Border { Height = 13 },
                Ui.Text("Only active app names and approximate seconds are recorded locally.", 12, false, Ui.Dim),
                new Border { Height = 10 },
                Ui.Button("Enable on-device screen time tracking", () =>
                {
                    PortableData.Settings.ScreenTimeConsent = true;
                    PortableData.SaveSettings();
                    _activity.Start();
                    Navigate("screen");
                })));
        }
        var records = _activity.Snapshot();
        var total = records.Sum(x => x.Value);
        var main = new StackPanel { Orientation = Orientation.Horizontal };
        var ring = new Grid { Width = 115, Height = 115, Margin = new Thickness(0, 0, 20, 0) };
        ring.Children.Add(new Ellipse
        {
            Width = 95, Height = 95, StrokeThickness = 12,
            Stroke = new SolidColorBrush(Color.FromRgb(53, 57, 64))
        });
        var ratio = records.Count == 0 || total == 0 ? 0 :
            records[0].Value / (double)total;
        if (ratio > 0)
        {
            var r = 47.5;
            var angle = -Math.PI / 2 + Math.Min(.999999, ratio) * Math.PI * 2;
            var start = new Point(57.5, 10);
            var end = new Point(57.5 + r * Math.Cos(angle), 57.5 + r * Math.Sin(angle));
            var figure = new PathFigure { StartPoint = start, IsClosed = false };
            figure.Segments.Add(new ArcSegment(end, new Size(r, r), 0, ratio > 0.5, SweepDirection.Clockwise, true));
            ring.Children.Add(new Path
            {
                Data = new PathGeometry(new[] { figure }), Stroke = Ui.Cyan, StrokeThickness = 12,
                StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round
            });
        }
        ring.Children.Add(new TextBlock { Text = $"{total / 60}m", FontSize = 18, FontWeight = FontWeights.Bold,
            VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center,
            Foreground = Brushes.White });
        main.Children.Add(ring);
        var usage = new StackPanel { Width = 480 };
        usage.Children.Add(Ui.Title("Most used apps · Today"));
        if (records.Count == 0) usage.Children.Add(Ui.Text("No recorded activity yet.", 11, false, Ui.Dim));
        foreach (var entry in records.Take(5))
        {
            var row = Ui.H(Ui.Text(entry.Key, 10), Ui.Text($"   {entry.Value / 60}m", 10, false, Ui.Dim));
            row.Margin = new Thickness(0, 3, 0, 0);
            usage.Children.Add(row);
            usage.Children.Add(Ui.Bar(total == 0 ? 0 : entry.Value / (double)total, 300));
        }
        main.Children.Add(usage);
        var page = Ui.V(Ui.H(Ui.Title("Screen time · Private on-device sampling"),
            Ui.Button("Pause tracking", () =>
            {
                PortableData.Settings.ScreenTimeConsent = false;
                PortableData.SaveSettings(); _activity.Pause(); Navigate("screen");
            }),
            Ui.Button("Delete history", () => { _activity.Clear(); Navigate("screen"); })),
            new Border { Height = 7 }, main);
        return Ui.WrapPage(page);
    }

    private UIElement RenderSettings()
    {
        var s = PortableData.Settings;
        var wake = Ui.Input(s.WakeHour.ToString(), 65);
        var bedtime = Ui.Input(s.BedHour.ToString(), 65);
        var reduced = new CheckBox { Content = "Reduced motion", IsChecked = s.ReducedMotion };
        var save = Ui.Button("Save settings", () =>
        {
            if (int.TryParse(wake.Text, out var w) && w >= 0 && w < 24) s.WakeHour = w;
            if (int.TryParse(bedtime.Text, out var b) && b >= 0 && b < 24) s.BedHour = b;
            s.ReducedMotion = reduced.IsChecked == true;
            PortableData.SaveSettings();
            Navigate("dashboard");
        });
        return Ui.WrapPage(Ui.V(Ui.Title("Aether Island Portable V2 · All data in ./Data"),
            new Border { Height = 13 },
            Ui.H(Ui.Text("Wake hour", 12), wake, Ui.Text("  Bed hour", 12), bedtime),
            new Border { Height = 10 }, reduced,
            new Border { Height = 12 }, save,
            new Border { Height = 10 },
            Ui.Text("Portable API credentials use Windows DPAPI and are bound to this Windows account.",
                11, false, Ui.Dim),
            Ui.Text("No telemetry · No registry auto-start · No installer required", 11, false, Ui.Dim)));
    }
}
