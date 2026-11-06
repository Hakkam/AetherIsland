using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AetherIsland.Integrations.Calendar;
using AetherIsland.Integrations.Media;
using AetherIsland.Windows;
using Windows.Graphics.Imaging;
using Windows.Media.Capture;
using System.Runtime.InteropServices.WindowsRuntime;

namespace AetherIsland.Portable;

public partial class MainWindow
{
    private TextBlock? _mediaTitle;
    private TextBlock? _mediaArtist;
    private TextBlock? _mediaPosition;
    private TextBlock? _mediaDuration;
    private System.Windows.Controls.Image? _mediaArt;

    private static UIElement RenderMedia()
    {
        var grid = new Grid { Margin = new Thickness(10, 4, 8, 0) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(320) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(92) });

        grid.Children.Add(BuildNowPlaying());
        var calendar = BuildCalendarPane(); Grid.SetColumn(calendar, 1); grid.Children.Add(calendar);
        var mirror = BuildMirrorCard(); Grid.SetColumn(mirror, 2); grid.Children.Add(mirror);

        return Ui.WrapPage(grid, horizontalPadding: 6);
    }

    private static FrameworkElement BuildNowPlaying()
    {
        var win = Win;
        var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 2, 14, 2) };

        var art = new System.Windows.Controls.Image { Width = 44, Height = 44, Stretch = Stretch.UniformToFill, MaxHeight = 44 };
        var artClip = new System.Windows.Controls.Border
        {
            Width = 44, Height = 44, CornerRadius = new CornerRadius(10),
            Background = Theme.Chip,
            Child = Iconography.Draw("media", 20, 1.9, Theme.Text),
            ClipToBounds = true
        };
        win._mediaArt = art;
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(artClip);
        var textCol = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
        var title = Ui.Text("Nothing playing", 14.5, true, wrap: true);
        var artist = Ui.Text("Open a compatible player — Spotify, YouTube, browser media.", 9.8, false, Theme.Dim, wrap: true);
        win._mediaTitle = title;
        win._mediaArtist = artist;
        textCol.Children.Add(title);
        textCol.Children.Add(artist);
        row.Children.Add(textCol);
        stack.Children.Add(row);

        stack.Children.Add(Ui.Spacer(0, 10));
        var timeline = new Grid();
        timeline.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        timeline.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        timeline.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var position = Ui.Text("0:00", 8.5, false, Theme.Dim);
        var duration = Ui.Text("0:00", 8.5, false, Theme.Dim);
        win._mediaPosition = position;
        win._mediaDuration = duration;
        timeline.Children.Add(position);
        var bar = Ui.Bar(0, 150, 3.5);
        bar.HorizontalAlignment = HorizontalAlignment.Stretch;
        Grid.SetColumn(bar, 1);
        bar.Margin = new Thickness(8, 0, 8, 0);
        timeline.Children.Add(bar);
        Grid.SetColumn(duration, 2);
        timeline.Children.Add(duration);
        stack.Children.Add(timeline);

        stack.Children.Add(Ui.Spacer(0, 10));
        var transport = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        transport.Children.Add(Ui.RoundIcon("prev", "Previous", 34, () => PowerHelper.SendMediaKey(0xB1)));
        transport.Children.Add(Ui.RoundIcon("play", "Play / pause", 40, async () => await win.Media.TogglePlayPauseAsync(), filled: true));
        transport.Children.Add(Ui.RoundIcon("next", "Next", 34, () => PowerHelper.SendMediaKey(0xB0)));
        stack.Children.Add(transport);

        void Show(MediaSessionInfo? session)
        {
            if (win._mediaTitle is null || win._mediaArtist is null) return;
            if (session is { IsValid: true })
            {
                title.Text = session.Title is { Length: > 0 } t ? t : "Unknown track";
                artist.Text = (session.Artist is { Length: > 0 } a ? a + " · " : "") + session.PlaybackState +
                    (session.AppName is { Length: > 0 } app ? " · " + app : "");
                if (session.Position is { } pos && session.Duration is { } dur && dur > TimeSpan.Zero)
                {
                    position.Text = $"{(int)pos.TotalMinutes}:{pos.Seconds:00}";
                    duration.Text = $"{(int)dur.TotalMinutes}:{dur.Seconds:00}";
                }
                if (session.AlbumArtPng is { Length: > 0 } png)
                {
                    try
                    {
                        var image = new BitmapImage();
                        image.BeginInit();
                        image.CacheOption = BitmapCacheOption.OnLoad;
                        using var ms = new MemoryStream(png);
                        image.StreamSource = ms;
                        image.EndInit();
                        image.Freeze();
                        art.Source = image;
                        artClip.Child = art;
                    }
                    catch { /* keep glyph */ }
                }
            }
            else
            {
                title.Text = "Nothing playing";
                artist.Text = "Open a compatible player — Spotify, YouTube, browser media.";
                position.Text = "0:00";
                duration.Text = "0:00";
            }
        }
        Show(win.Media.LastSession);
        win.Media.Updated += info => win.Dispatcher.BeginInvoke(() => Show(info));
        return stack;
    }

    private static FrameworkElement BuildCalendarPane()
    {
        var win = Win;
        var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 2, 4, 2) };
        var now = DateTime.Now;
        var monthStart = new DateTime(now.Year, now.Month, 1);
        var state = new CalendarState { Month = monthStart };

        var header = new Grid { Margin = new Thickness(2, 0, 2, 4) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var title = Ui.Text(state.Month.ToString("MMMM yyyy"), 12, true);
        header.Children.Add(title);
        var prev = Iconography.IconButton("collapse", "Previous month", 22, () => { state.Month = state.Month.AddMonths(-1); state.Rebuild(); }, 12);
        var nextBtn = Iconography.IconButton("collapse", "Next month", 22, () => { state.Month = state.Month.AddMonths(1); state.Rebuild(); }, 12);
        nextBtn.LayoutTransform = new System.Windows.Media.RotateTransform(180);
        header.Children.Add(prev); Grid.SetColumn(prev, 1);
        header.Children.Add(nextBtn); Grid.SetColumn(nextBtn, 2);
        stack.Children.Add(header);

        var gridHost = new ContentControl();
        stack.Children.Add(gridHost);
        var eventsHost = new StackPanel();
        stack.Children.Add(eventsHost);

        List<CalendarEvent> events = new();
        bool eventsLoaded = false;

        void Rebuild()
        {
            title.Text = state.Month.ToString("MMMM yyyy");
            gridHost.Content = BuildMonthGrid(state.Month, events);
            eventsHost.Children.Clear();
            if (events.Count == 0)
            {
                if (eventsLoaded)
                {
                    eventsHost.Children.Add(Ui.Spacer(0, 4));
                    eventsHost.Children.Add(Ui.Text("No upcoming events in your calendars.", 9.5, false, Theme.Dim));
                }
                else
                {
                    eventsHost.Children.Add(Ui.Spacer(0, 4));
                    eventsHost.Children.Add(Ui.Text("No calendars yet.  ", 9.5, false, Theme.Dim));
                    var link = Ui.Text("Add an .ics calendar in Settings", 9.5, true, Theme.Accent);
                    link.Cursor = System.Windows.Input.Cursors.Hand;
                    link.MouseLeftButtonDown += (_, _) => Win.Navigate("settings");
                    eventsHost.Children.Add(link);
                }
            }
            else
            {
                foreach (var ev in events.Where(e => e.End >= DateTime.Now).OrderBy(e => e.Start).Take(3))
                {
                    var row = Ui.H(
                        Ui.Text(ev.Start.ToString("ddd HH:mm"), 9, false, Theme.Faint),
                        Ui.Text("  " + ev.Title, 9.8, true));
                    row.Margin = new Thickness(2, 2, 0, 0);
                    if (ev.Url is { Length: > 0 } url)
                    {
                        row.Cursor = System.Windows.Input.Cursors.Hand;
                        row.MouseLeftButtonDown += (_, _) => External.Open(url);
                        row.ToolTip = ev.Url;
                    }
                    eventsHost.Children.Add(row);
                }
            }
        }

        state.Rebuild = Rebuild;
        Rebuild();
        _ = System.Threading.Tasks.Task.Run(async () =>
        {
            try
            {
                var sources = PortableData.Settings.CalendarSources;
                if (sources.Count == 0) { eventsLoaded = true; }
                else
                {
                    var provider = new IcsCalendarProvider();
                    events = await provider.LoadAsync(sources);
                    eventsLoaded = true;
                }
                _ = win.Dispatcher.BeginInvoke(Rebuild);
            }
            catch (Exception ex)
            {
                PortableData.Log("CALENDAR", ex.Message);
                eventsLoaded = true;
                _ = win.Dispatcher.BeginInvoke(Rebuild);
            }
        });
        return stack;
    }

    private sealed class CalendarState
    {
        public DateTime Month;
        public Action Rebuild = static () => { };
    }

    private static FrameworkElement BuildMonthGrid(DateTime month, List<CalendarEvent> events)
    {
        var grid = new Grid { Margin = new Thickness(0, 2, 0, 0) };
        for (var i = 0; i < 7; i++) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var first = new DateTime(month.Year, month.Month, 1);
        var offset = ((int)first.DayOfWeek + 6) % 7; // Monday-first
        var daysInMonth = DateTime.DaysInMonth(month.Year, month.Month);
        var weeks = (offset + daysInMonth + 6) / 7;
        for (var w = 0; w < weeks + 1; w++) grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(21) });

        string[] dayNames = { "Mo", "Tu", "We", "Th", "Fr", "Sa", "Su" };
        for (var d = 0; d < 7; d++)
        {
            var label = Ui.Text(dayNames[d], 8, false, Theme.Faint, TextAlignment.Center);
            Grid.SetColumn(label, d);
            grid.Children.Add(label);
        }

        var eventDays = events.Select(e => e.Start.Date).ToHashSet();
        for (var day = 1; day <= daysInMonth; day++)
        {
            var index = offset + day - 1;
            var row = 1 + index / 7;
            var col = index % 7;
            var date = new DateTime(month.Year, month.Month, day);
            var isToday = date == DateTime.Today;
            var cell = new System.Windows.Controls.Border
            {
                Width = 19, Height = 19,
                CornerRadius = new CornerRadius(6),
                Background = isToday ? Ui.Solid("#E4574F") : Brushes.Transparent,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            var number = Ui.Text(day.ToString(), isToday ? 9 : 9.3, isToday, isToday ? Brushes.White : Theme.Text, TextAlignment.Center);
            number.VerticalAlignment = VerticalAlignment.Center;
            cell.Child = number;
            Grid.SetRow(cell, row);
            Grid.SetColumn(cell, col);
            if (eventDays.Contains(date) && !isToday)
            {
                cell.Background = Theme.Chip;
                cell.ToolTip = "Has events";
            }
            grid.Children.Add(cell);
        }
        return grid;
    }

    private static FrameworkElement BuildMirrorCard()
    {
        var stack = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(2, 2, 6, 2),
            Cursor = System.Windows.Input.Cursors.Hand
        };
        var card = new System.Windows.Controls.Border
        {
            Width = 80, Height = 92,
            CornerRadius = new CornerRadius(12),
            Background = Theme.Chip,
            Child = Ui.V(
                Ui.Spacer(0, 16),
                new ContentControl
                {
                    Content = Iconography.Draw("camera", 26, 1.9, Theme.Text),
                    HorizontalAlignment = HorizontalAlignment.Center
                },
                Ui.Spacer(0, 10),
                Ui.Text("Mirror", 10, true, Theme.Text, TextAlignment.Center))
        };
        stack.Children.Add(card);
        stack.MouseLeftButtonDown += (_, _) => MirrorWindow.Toggle();
        stack.ToolTip = "Camera mirror preview";
        return stack;
    }
}

/// <summary>
/// Explicit camera mirror: activation only on open, mirrored preview,
/// resources released immediately when closed. Shows a truthful error state
/// when no camera is available or access is denied.
/// </summary>
internal sealed class MirrorWindow : System.Windows.Window
{
    private static MirrorWindow? _instance;
    private MediaCapture? _capture;
    private CancellationTokenSource? _cts;
    private readonly System.Windows.Controls.Image _preview = new() { Stretch = Stretch.Uniform };
    private readonly System.Windows.Controls.TextBlock _status = Ui.Text("Starting camera…", 11, false, Theme.Dim);

    private MirrorWindow()
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        Width = 320; Height = 400;
        WindowStartupLocation = WindowStartupLocation.Manual;
        var wa = SystemParameters.WorkArea;
        Left = wa.Right - Width - 24;
        Top = wa.Top + 64;

        var border = new System.Windows.Controls.Border
        {
            CornerRadius = new CornerRadius(18),
            Background = Theme.Bg,
            BorderBrush = Theme.PanelEdge,
            BorderThickness = new Thickness(1),
            Child = new Grid()
        };
        var grid = (Grid)border.Child;
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        var header = Ui.H(
            Ui.Text("Mirror", 11, true),
            Ui.Spacer(10),
            Iconography.IconButton("close", "Close mirror", 26, () => Close(), 13));
        header.Margin = new Thickness(10, 8, 8, 4);
        grid.Children.Add(header);
        var host = new Grid();
        host.Children.Add(_preview);
        host.Children.Add(_status);
        _status.HorizontalAlignment = HorizontalAlignment.Center;
        _status.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetRow(host, 1);
        grid.Children.Add(host);
        Content = border;

        _preview.RenderTransform = new ScaleTransform(-1, 1);
        _preview.RenderTransformOrigin = new Point(0.5, 0.5);
        Closed += (_, _) => Stop();
    }

    public static void Toggle()
    {
        if (_instance != null) { _instance.Close(); return; }
        _instance = new MirrorWindow();
        _instance.Show();
        _ = _instance.StartAsync();
    }

    private async Task StartAsync()
    {
        try
        {
            _capture = new MediaCapture();
            await _capture.InitializeAsync(new MediaCaptureInitializationSettings
            {
                StreamingCaptureMode = StreamingCaptureMode.Video,
                MediaCategory = MediaCategory.Media
            });
            _status.Text = "";
            _cts = new CancellationTokenSource();
            _ = PreviewLoop(_cts.Token);
        }
        catch (Exception ex)
        {
            PortableData.Log("MIRROR", ex.Message);
            _status.Text = ex.Message.Contains("access", StringComparison.OrdinalIgnoreCase)
                ? "Camera access denied in Windows privacy settings."
                : "No camera available on this device.";
        }
    }

    private async Task PreviewLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested && _capture != null)
        {
            try
            {
                var frame = await _capture.GetPreviewFrameAsync();
                if (frame is null) { await Task.Delay(100, token); continue; }
                var soft = frame.SoftwareBitmap;
                if (soft is null) { await Task.Delay(100, token); continue; }
                var converted = soft.BitmapPixelFormat == BitmapPixelFormat.Bgra8
                    ? soft
                    : SoftwareBitmap.Convert(soft, BitmapPixelFormat.Bgra8);
                if (!ReferenceEquals(converted, soft)) soft.Dispose();
                using var final = converted;
                var width = final.PixelWidth;
                var height = final.PixelHeight;
                var buffer = new byte[width * height * 4];
                final.CopyToBuffer(WindowsRuntimeBufferExtensions.AsBuffer(buffer));
                var bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
                bitmap.WritePixels(new System.Windows.Int32Rect(0, 0, width, height), buffer, width * 4, 0);
                bitmap.Freeze();
                _preview.Source = bitmap;
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                PortableData.Log("MIRROR", "frame: " + ex.Message);
                _status.Text = "Preview stopped — camera is busy or unavailable.";
                break;
            }
            await Task.Delay(66, token); // ~15 fps keeps GPU/CPU cost modest
        }
    }

    private void Stop()
    {
        try { _cts?.Cancel(); } catch { }
        try { _capture?.Dispose(); } catch { }
        _capture = null;
        _instance = null;
    }
}
