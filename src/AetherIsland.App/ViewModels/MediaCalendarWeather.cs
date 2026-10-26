using System.Collections.ObjectModel;
using Windows.Media.Capture;
using Windows.Media.Capture.Frames;
using Windows.Graphics.Imaging;
using Windows.Media.MediaProperties;
using Windows.Storage.Streams;
using System.IO;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AetherIsland.Core;
using AetherIsland.Integrations.Calendar;
using AetherIsland.Integrations.Media;
using AetherIsland.Integrations.Weather;
using AetherIsland.Windows;

namespace AetherIsland.App.ViewModels;

public partial class MediaViewModel : ObservableObject, ILifecycleAware
{
    private DispatcherTimer? _positionTimer;
    private MediaCapture? _camera;
    private MediaFrameReader? _cameraReader;
    private int _frameProcessing;
    private DateTime _lastCameraFrame = DateTime.MinValue;

    [ObservableProperty] private BitmapSource? _cameraFrame;
    [ObservableProperty] private bool _isCameraActive;
    [ObservableProperty] private bool _cameraBusy;
    [ObservableProperty] private string _cameraStatus = Loc.T("media.cameraPrivacy");
    public string CameraTitle => Loc.T("media.cameraTitle");
    public string CameraActionLabel => IsCameraActive ? Loc.T("media.stopCamera") : Loc.T("media.startCamera");
    partial void OnIsCameraActiveChanged(bool value) => OnPropertyChanged(nameof(CameraActionLabel));

    [ObservableProperty] private bool _hasSession;
    [ObservableProperty] private string _title = "";
    [ObservableProperty] private string _artist = "";
    [ObservableProperty] private string _album = "";
    [ObservableProperty] private string _appName = "";
    [ObservableProperty] private string _positionText = "0:00";
    [ObservableProperty] private string _durationText = "0:00";
    [ObservableProperty] private double _progressFraction;
    [ObservableProperty] private string _playbackGlyph = "\uE768";
    [ObservableProperty] private bool _isPlaying;
    [ObservableProperty] private BitmapSource? _albumArt;
    [ObservableProperty] private string _emptyText = Loc.T("media.noSession");
    [ObservableProperty] private string _playLabel = Loc.T("media.play");
    [ObservableProperty] private string _pauseLabel = Loc.T("media.pause");

    public bool EmptyVisible => !HasSession;
    public bool NoArt => AlbumArt is null;
    public string ArtistLine => string.IsNullOrWhiteSpace(Album)
        ? string.IsNullOrWhiteSpace(AppName) ? Artist : $"{Artist} · {AppName}".Trim(' ', '·')
        : $"{Artist} · {Album}".Trim(' ', '·');

    partial void OnHasSessionChanged(bool value) => OnPropertyChanged(nameof(EmptyVisible));
    partial void OnAlbumArtChanged(BitmapSource? value) => OnPropertyChanged(nameof(NoArt));

    public void OnShown()
    {
        AppServices.Media.Updated += Apply;
        Apply(AppServices.Media.LastSession);
        _positionTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _positionTimer.Tick += async (_, _) =>
        {
            var info = await AppServices.Media.RefreshAsync();
            if (info is { IsValid: true })
            {
                PositionText = FormatTime(info.Position);
                DurationText = FormatTime(info.Duration);
                ProgressFraction = info.Duration > TimeSpan.Zero
                    ? Math.Clamp(info.Position / info.Duration, 0, 1)
                    : 0;
            }
        };
        _positionTimer.Start();
    }

    public void OnHidden()
    {
        AppServices.Media.Updated -= Apply;
        _positionTimer?.Stop();
        _positionTimer = null;
        _ = StopCameraAsync();
    }

    [RelayCommand]
    private async Task ToggleCameraAsync()
    {
        if (CameraBusy) return;
        if (IsCameraActive) { await StopCameraAsync(); return; }
        CameraBusy = true;
        CameraStatus = Loc.T("media.cameraOpening");
        try
        {
            var groups = await MediaFrameSourceGroup.FindAllAsync();
            var group = groups.FirstOrDefault(g => g.SourceInfos.Any(s => s.SourceKind == MediaFrameSourceKind.Color));
            if (group is null) throw new InvalidOperationException(Loc.T("media.noCamera"));
            _camera = new MediaCapture();
            await _camera.InitializeAsync(new MediaCaptureInitializationSettings
            {
                SourceGroup = group,
                StreamingCaptureMode = StreamingCaptureMode.Video,
                MemoryPreference = MediaCaptureMemoryPreference.Cpu,
                SharingMode = MediaCaptureSharingMode.SharedReadOnly
            });
            var source = _camera.FrameSources.Values.FirstOrDefault(s => s.Info.SourceKind == MediaFrameSourceKind.Color);
            if (source is null) throw new InvalidOperationException(Loc.T("media.noCamera"));
            _cameraReader = await _camera.CreateFrameReaderAsync(source, MediaEncodingSubtypes.Bgra8);
            _cameraReader.FrameArrived += CameraFrameArrived;
            var status = await _cameraReader.StartAsync();
            if (status != MediaFrameReaderStartStatus.Success)
                throw new InvalidOperationException($"Camera start status: {status}");
            AppServices.Settings.Privacy.CameraConsent = true;
            AppServices.SettingsStore.Save(AppServices.Settings);
            IsCameraActive = true;
            CameraStatus = Loc.T("media.cameraOn");
        }
        catch (Exception ex)
        {
            CameraStatus = ex.Message;
            await StopCameraAsync(preserveStatus: true);
        }
        finally { CameraBusy = false; }
    }

    private async void CameraFrameArrived(MediaFrameReader sender, MediaFrameArrivedEventArgs args)
    {
        if (Interlocked.Exchange(ref _frameProcessing, 1) != 0) return;
        try
        {
            if (!IsCameraActive || (DateTime.UtcNow - _lastCameraFrame).TotalMilliseconds < 350) return;
            _lastCameraFrame = DateTime.UtcNow;
            using var frame = sender.TryAcquireLatestFrame();
            var bitmap = frame?.VideoMediaFrame?.SoftwareBitmap;
            if (bitmap is null) return;
            using var bgra = SoftwareBitmap.Convert(bitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore);
            using var stream = new InMemoryRandomAccessStream();
            var encoder = await Windows.Graphics.Imaging.BitmapEncoder.CreateAsync(Windows.Graphics.Imaging.BitmapEncoder.PngEncoderId, stream);
            encoder.BitmapTransform.ScaledWidth = (uint)Math.Min(bgra.PixelWidth, 320);
            encoder.BitmapTransform.ScaledHeight = (uint)Math.Max(1, bgra.PixelHeight * (int)encoder.BitmapTransform.ScaledWidth / bgra.PixelWidth);
            encoder.SetSoftwareBitmap(bgra);
            await encoder.FlushAsync();
            stream.Seek(0);
            using var netStream = stream.AsStreamForRead();
            using var buffer = new MemoryStream();
            await netStream.CopyToAsync(buffer);
            buffer.Position = 0;
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = buffer;
            image.EndInit();
            image.Freeze();
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                if (IsCameraActive) CameraFrame = image;
            });
        }
        catch { /* Frames may be invalidated while the camera is stopping. */ }
        finally { Interlocked.Exchange(ref _frameProcessing, 0); }
    }

    private async Task StopCameraAsync(bool preserveStatus = false)
    {
        var reader = _cameraReader;
        _cameraReader = null;
        if (reader is not null)
        {
            reader.FrameArrived -= CameraFrameArrived;
            try { await reader.StopAsync(); } catch { }
            reader.Dispose();
        }
        _camera?.Dispose();
        _camera = null;
        IsCameraActive = false;
        CameraFrame = null;
        if (!preserveStatus) CameraStatus = Loc.T("media.cameraPrivacy");
    }

    public void Apply(MediaSessionInfo? info)
    {
        if (info is null || !info.IsValid)
        {
            HasSession = false;
            return;
        }
        HasSession = true;
        Title = info.Title;
        Artist = info.Artist;
        Album = info.Album;
        AppName = info.AppName;
        IsPlaying = info.PlaybackState == "playing";
        PlaybackGlyph = IsPlaying ? "\uE769" : "\uE768";
        PositionText = FormatTime(info.Position);
        DurationText = FormatTime(info.Duration);
        ProgressFraction = info.Duration > TimeSpan.Zero ? Math.Clamp(info.Position / info.Duration, 0, 1) : 0;
        if (info.AlbumArtPng is { Length: > 0 })
        {
            try
            {
                var img = new BitmapImage();
                using var ms = new MemoryStream(info.AlbumArtPng);
                img.BeginInit();
                img.CacheOption = BitmapCacheOption.OnLoad;
                img.StreamSource = ms;
                img.EndInit();
                img.Freeze();
                AlbumArt = img;
            }
            catch { AlbumArt = null; }
        }
        else AlbumArt = null;
    }

    [RelayCommand]
    private async Task PlayPauseAsync() => await AppServices.Media.TogglePlayPauseAsync();

    [RelayCommand]
    private void Prev() => PowerHelper.SendMediaKey(0xB1);

    [RelayCommand]
    private void Next() => PowerHelper.SendMediaKey(0xB0);

    private static string FormatTime(TimeSpan t) =>
        t > TimeSpan.Zero ? $"{(int)t.TotalMinutes}:{t.Seconds:00}" : "0:00";
}

public partial class CalendarViewModel : ObservableObject, ILifecycleAware
{
    [ObservableProperty] private ObservableCollection<CalendarEventRowVm> _events = new();
    [ObservableProperty] private string _upcomingTitle = Loc.T("calendar.upcoming");
    [ObservableProperty] private string _emptyText = Loc.T("calendar.noEvents");
    [ObservableProperty] private string _noAccountsText = Loc.T("calendar.noAccounts");
    [ObservableProperty] private string _addIcsLabel = Loc.T("calendar.addIcs");
    [ObservableProperty] private string _sourceUrl = "";
    [ObservableProperty] private string _calendarStatus = "";
    public string SourceUrlHint => Loc.T("calendar.icsUrl");
    public string AddUrlLabel => Loc.T("calendar.addUrl");
    public string ConnectGoogleLabel => Loc.T("calendar.connectGoogle");
    public string ConnectMicrosoftLabel => Loc.T("calendar.connectMicrosoft");
    public string DisconnectGoogleLabel => Loc.T("calendar.disconnectGoogle");
    public string DisconnectMicrosoftLabel => Loc.T("calendar.disconnectMicrosoft");
    public bool GoogleConnected => AppServices.OAuthCalendar.IsConnected(OAuthCalendarProvider.Google);
    public bool MicrosoftConnected => AppServices.OAuthCalendar.IsConnected(OAuthCalendarProvider.Microsoft);
    [ObservableProperty] private bool _hasAccounts;
    [ObservableProperty] private ObservableCollection<WeekDayVm> _weekDays = new();
    [ObservableProperty] private string _weekTitle = Loc.T("calendar.week");

    public bool NoAccounts => !HasAccounts;
    public bool EmptyVisible => HasAccounts && Events.Count == 0;

    partial void OnHasAccountsChanged(bool value)
    {
        OnPropertyChanged(nameof(NoAccounts));
        OnPropertyChanged(nameof(EmptyVisible));
    }

    public CalendarViewModel()
    {
        Events.CollectionChanged += (_, _) => OnPropertyChanged(nameof(EmptyVisible));
        BuildWeek();
    }

    public void OnShown()
    {
        HasAccounts = AppServices.Settings.Calendar.IcsUrls.Count > 0 || GoogleConnected || MicrosoftConnected;
        _ = LoadAsync();
    }

    public void OnHidden() { }

    private void BuildWeek()
    {
        WeekDays.Clear();
        var today = DateTime.Today;
        var start = today.AddDays(-(int)today.DayOfWeek + (today.DayOfWeek == DayOfWeek.Sunday ? -6 : 1));
        for (var i = 0; i < 7; i++)
        {
            var d = start.AddDays(i);
            WeekDays.Add(new WeekDayVm(d.ToString("ddd"), d.Day, d.Date == today));
        }
    }

    private async Task LoadAsync()
    {
        var urls = AppServices.Settings.Calendar.IcsUrls;
        HasAccounts = urls.Count > 0 || GoogleConnected || MicrosoftConnected;
        Events.Clear();
        if (!HasAccounts) return;
        try
        {
            var events = await AppServices.CalendarProvider.LoadAsync(urls);
            var failures = new List<string>();
            foreach (var provider in new[] { OAuthCalendarProvider.Google, OAuthCalendarProvider.Microsoft })
            {
                if (!AppServices.OAuthCalendar.IsConnected(provider)) continue;
                var id = provider == OAuthCalendarProvider.Google
                    ? AppServices.Settings.Calendar.GoogleClientId
                    : AppServices.Settings.Calendar.MicrosoftClientId;
                try { events.AddRange(await AppServices.OAuthCalendar.LoadEventsAsync(provider, id)); }
                catch (Exception ex) { failures.Add(provider + ": " + ex.Message); }
            }
            CalendarStatus = string.Join(" · ", failures);
            foreach (var e in events.Where(e => e.End > DateTime.Now).OrderBy(e => e.Start).Take(12))
                Events.Add(new CalendarEventRowVm(e));
            if (Events.Count == 0) EmptyText = Loc.T("calendar.noEvents");
        }
        catch
        {
            EmptyText = Loc.T("common.error");
        }
    }

    [RelayCommand]
    private void Join(CalendarEventRowVm row)
    {
        if (row.Url is { Length: > 0 })
        {
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(row.Url) { UseShellExecute = true }); }
            catch (Exception ex) { AppServices.Notify.ShowBalloon("Aether Island", ex.Message); }
        }
    }

    [RelayCommand]
    private void AddIcs()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog { Title = Loc.T("calendar.addIcs"), Filter = "iCalendar (*.ics)|*.ics|All files (*.*)|*.*" };
        if (dlg.ShowDialog() != true) return;
        var s = AppServices.Settings;
        s.Calendar.IcsUrls.Add(dlg.FileName);
        AppServices.SettingsStore.Save(s);
        HasAccounts = true;
        _ = LoadAsync();
    }

    [RelayCommand]
    private void AddUrl()
    {
        if (!Uri.TryCreate(SourceUrl.Trim(), UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps)
        {
            CalendarStatus = Loc.T("calendar.invalidUrl");
            return;
        }
        var sources = AppServices.Settings.Calendar.IcsUrls;
        if (!sources.Contains(uri.AbsoluteUri, StringComparer.OrdinalIgnoreCase))
        {
            sources.Add(uri.AbsoluteUri);
            AppServices.SettingsStore.Save(AppServices.Settings);
        }
        SourceUrl = "";
        CalendarStatus = "";
        _ = LoadAsync();
    }

    [RelayCommand]
    private void Refresh() => _ = LoadAsync();

    [RelayCommand]
    private async Task ConnectGoogleAsync() => await ConnectAsync(OAuthCalendarProvider.Google);

    [RelayCommand]
    private async Task ConnectMicrosoftAsync() => await ConnectAsync(OAuthCalendarProvider.Microsoft);

    private async Task ConnectAsync(string provider)
    {
        var id = provider == OAuthCalendarProvider.Google
            ? AppServices.Settings.Calendar.GoogleClientId : AppServices.Settings.Calendar.MicrosoftClientId;
        try
        {
            CalendarStatus = Loc.T("calendar.openBrowser");
            await AppServices.OAuthCalendar.ConnectAsync(provider, id);
            CalendarStatus = Loc.T("common.connected");
            OnPropertyChanged(nameof(GoogleConnected));
            OnPropertyChanged(nameof(MicrosoftConnected));
            await LoadAsync();
        }
        catch (Exception ex) { CalendarStatus = ex.Message; }
    }

    [RelayCommand]
    private async Task DisconnectGoogleAsync() => await DisconnectAsync(OAuthCalendarProvider.Google);

    [RelayCommand]
    private async Task DisconnectMicrosoftAsync() => await DisconnectAsync(OAuthCalendarProvider.Microsoft);

    private async Task DisconnectAsync(string provider)
    {
        AppServices.OAuthCalendar.Disconnect(provider);
        OnPropertyChanged(nameof(GoogleConnected));
        OnPropertyChanged(nameof(MicrosoftConnected));
        CalendarStatus = Loc.T("common.disconnected");
        await LoadAsync();
    }
}

public sealed class WeekDayVm(string dayName, int dayNumber, bool isToday)
{
    public string DayName { get; } = dayName;
    public int DayNumber { get; } = dayNumber;
    public bool IsToday { get; } = isToday;
}

public partial class CalendarEventRowVm : ObservableObject
{
    public string Title { get; }
    public DateTime Start { get; }
    public string StartText { get; }
    public string DateText { get; }
    public string RelativeText { get; }
    public string? Location { get; }
    public string? Url { get; }
    public bool HasJoin { get; }
    public string JoinLabel { get; } = Loc.T("calendar.join");
    public string Source { get; }

    public CalendarEventRowVm(CalendarEvent e)
    {
        Title = e.Title;
        Start = e.Start;
        StartText = e.AllDay ? Loc.T("calendar.allDay") : e.Start.ToString("H:mm");
        DateText = e.Start.ToString("d MMM");
        var until = e.Start - DateTime.Now;
        if (until > TimeSpan.Zero)
            RelativeText = Loc.TF("calendar.in", $"{(int)until.TotalHours}h {until.Minutes}m");
        else
            RelativeText = Loc.T("common.today");
        Location = e.Location;
        Url = e.Url;
        HasJoin = !string.IsNullOrEmpty(e.Url);
        Source = e.Source;
    }
}

public partial class WeatherViewModel : ObservableObject, ILifecycleAware
{
    private DispatcherTimer? _hourlyTimer;

    [ObservableProperty] private string _locationName = "";
    [ObservableProperty] private string _tempText = "--°";
    [ObservableProperty] private string _feelsLikeText = "";
    [ObservableProperty] private string _description = "";
    [ObservableProperty] private string _weatherGlyph = "\uE9D6";
    [ObservableProperty] private string _hiLoText = "";
    [ObservableProperty] private string _humidityText = "";
    [ObservableProperty] private string _windText = "";
    [ObservableProperty] private string _pressureText = "";
    [ObservableProperty] private string _uvText = "";
    [ObservableProperty] private string _sunriseText = "";
    [ObservableProperty] private string _sunsetText = "";
    [ObservableProperty] private bool _isFromCache;
    [ObservableProperty] private bool _hasWeather;
    [ObservableProperty] private string _errorText = Loc.T("weather.error");
    [ObservableProperty] private string _retryLabel = Loc.T("common.retry");
    [ObservableProperty] private string _searchLabel = Loc.T("weather.searchLocation");

    [ObservableProperty] private ObservableCollection<HourRowVm> _hourly = new();
    [ObservableProperty] private ObservableCollection<DayRowVm> _daily = new();

    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private ObservableCollection<GeoRowVm> _searchResults = new();

    public bool HasResults => SearchResults.Count > 0;
    public bool ShowError => !HasWeather;
    public string CachedText => Loc.T("weather.cachedOffline");
    public string HumidityLabel => Loc.T("weather.humidity");
    public string WindLabel => Loc.T("weather.wind");
    public string PressureLabel => Loc.T("weather.pressure");
    public string UvLabel => Loc.T("weather.uv");
    public string SunriseLabel => Loc.T("weather.sunrise");
    public string SunsetLabel => Loc.T("weather.sunset");
    public string HourlyTitle => Loc.T("weather.hourly");
    public string DailyTitle => Loc.T("weather.daily");

    partial void OnSearchResultsChanged(ObservableCollection<GeoRowVm> value)
        => OnPropertyChanged(nameof(HasResults));

    partial void OnHasWeatherChanged(bool value) => OnPropertyChanged(nameof(ShowError));

    public void OnShown()
    {
        AppServices.Weather.Updated += OnWeatherUpdated;
        Apply(AppServices.Weather.Current);
        _hourlyTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(10) };
        _hourlyTimer.Tick += (_, _) => _ = AppServices.Weather.RefreshAsync();
        _hourlyTimer.Start();
    }

    public void OnHidden()
    {
        AppServices.Weather.Updated -= OnWeatherUpdated;
        _hourlyTimer?.Stop();
        _hourlyTimer = null;
    }

    private void OnWeatherUpdated() => Apply(AppServices.Weather.Current);

    private void Apply(WeatherData? w)
    {
        if (w is null) { HasWeather = false; return; }
        var units = AppServices.Settings.Weather.Units == "imperial";
        double? Conv(double? c) => units && c is not null ? c * 9 / 5 + 32 : c;
        var suffix = units ? "°F" : "°C";

        HasWeather = true;
        IsFromCache = w.FromCache;
        LocationName = w.LocationName;
        TempText = w.TempC is null ? "--°" : $"{Math.Round(Conv(w.TempC) ?? 0)}{suffix}";
        Description = WeatherCodes.Describe(w.Code, w.IsDay).Desc;
        WeatherGlyph = WeatherCodes.Describe(w.Code, w.IsDay).Glyph;
        FeelsLikeText = $"{Loc.T("weather.feelsLike")} {Math.Round(Conv(w.FeelsLikeC) ?? 0)}{suffix}";
        var today = w.Daily.FirstOrDefault();
        HiLoText = today is null ? "" : $"H {Math.Round(Conv(today.MaxC) ?? 0)}°  L {Math.Round(Conv(today.MinC) ?? 0)}°";
        HumidityText = w.Humidity is null ? "—" : $"{w.Humidity}%";
        WindText = w.WindKmh is null ? "—" : units ? $"{Math.Round(w.WindKmh.Value * 0.621371)} mph" : $"{Math.Round(w.WindKmh.Value)} km/h";
        PressureText = w.PressureHpa is null ? "—" : $"{Math.Round(w.PressureHpa.Value)} hPa";
        UvText = today?.UvIndex is null ? "—" : $"{today.UvIndex:0.#}";
        SunriseText = w.Sunrise?.ToString("H:mm") ?? "—";
        SunsetText = w.Sunset?.ToString("H:mm") ?? "—";

        Hourly.Clear();
        foreach (var h in w.Hourly.Take(12))
            Hourly.Add(new HourRowVm(
                h.Time.ToString("H:mm"),
                h.TempC is null ? "—" : $"{Math.Round(Conv(h.TempC) ?? 0)}°",
                WeatherCodes.Describe(h.Code).Glyph,
                h.PrecipProb is null ? "" : $"{h.PrecipProb}%"));
        Daily.Clear();
        foreach (var d in w.Daily.Take(7))
            Daily.Add(new DayRowVm(
                d.Date.ToString("ddd"),
                d.MinC is null ? "—" : $"{Math.Round(Conv(d.MinC) ?? 0)}°",
                d.MaxC is null ? "—" : $"{Math.Round(Conv(d.MaxC) ?? 0)}°",
                WeatherCodes.Describe(d.Code).Glyph,
                d.PrecipProb is null ? "" : $"{d.PrecipProb}%"));
    }


    [RelayCommand]
    private async Task SearchAsync()
    {
        SearchResults.Clear();
        if (string.IsNullOrWhiteSpace(SearchText)) return;
        var results = await OpenMeteoService.GeocodeAsync(SearchText);
        foreach (var r in results)
            SearchResults.Add(new GeoRowVm($"{r.Name}, {r.Country}", r.Latitude, r.Longitude));
        OnPropertyChanged(nameof(HasResults));
    }

    [RelayCommand]
    private void PickResult(GeoRowVm result)
    {
        var s = AppServices.Settings;
        s.Weather.LocationName = result.Label;
        s.Weather.Latitude = result.Lat;
        s.Weather.Longitude = result.Lon;
        AppServices.SettingsStore.Save(s);
        SearchResults.Clear();
        SearchText = "";
        OnPropertyChanged(nameof(HasResults));
        _ = AppServices.Weather.RefreshAsync();
    }

    [RelayCommand]
    private void Refresh() => _ = AppServices.Weather.RefreshAsync();
}

public sealed record HourRowVm(string TimeText, string TempText, string Glyph, string PrecipText);
public sealed record DayRowVm(string DayName, string MinText, string MaxText, string Glyph, string PrecipText);
public sealed record GeoRowVm(string Label, double Lat, double Lon);
