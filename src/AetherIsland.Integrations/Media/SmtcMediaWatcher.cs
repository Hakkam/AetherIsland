using Windows.Media.Control;

namespace AetherIsland.Integrations.Media;

public sealed class MediaSessionInfo
{
    public string AppName { get; set; } = "";
    public string Title { get; set; } = "";
    public string Artist { get; set; } = "";
    public string Album { get; set; } = "";
    public string PlaybackState { get; set; } = "stopped"; // playing | paused | stopped
    public TimeSpan Position { get; set; }
    public TimeSpan Duration { get; set; }
    public byte[]? AlbumArtPng { get; set; }
    public bool IsValid => !string.IsNullOrEmpty(Title) || !string.IsNullOrEmpty(Artist);
}

/// <summary>
/// Reads media sessions through the official Windows System Media Transport Controls
/// (GlobalSystemMediaTransportControlsSessionManager). Transport next/previous is sent
/// through virtual media keys because SMTC does not expose next/prev to observers.
/// </summary>
public sealed class SmtcMediaWatcher : IDisposable
{
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private MediaSessionInfo? _last;
    public MediaSessionInfo? LastSession => _last;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _artDirty = true;

    public event Action<MediaSessionInfo?>? Updated;

    public async Task StartAsync()
    {
        try
        {
            _manager = await GlobalSystemMediaTransportControlsSessionManager
                .RequestAsync().AsTask();
            _manager.SessionsChanged += OnSessionsChanged;
            _manager.CurrentSessionChanged += (_, _) => _ = RefreshAsync();
            await RefreshAsync();
        }
        catch
        {
            // SMTC unavailable (service disabled) — module shows its empty state
        }
    }

    private void OnSessionsChanged(GlobalSystemMediaTransportControlsSessionManager sender, SessionsChangedEventArgs args)
        => _ = RefreshAsync();

    public async Task<MediaSessionInfo?> RefreshAsync()
    {
        await _gate.WaitAsync();
        try
        {
            var session = _manager?.GetSessions()
                .OrderByDescending(s => s.GetPlaybackInfo().PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
                .FirstOrDefault();
            if (session is null)
            {
                _last = null;
                Updated?.Invoke(null);
                return null;
            }

            var info = new MediaSessionInfo
            {
                AppName = session.SourceAppUserModelId.Split('!')[0],
                PlaybackState = session.GetPlaybackInfo().PlaybackStatus switch
                {
                    GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing => "playing",
                    GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused => "paused",
                    _ => "stopped"
                }
            };
            try
            {
                var tl = session.GetTimelineProperties();
                info.Position = tl.Position;
                if (tl.EndTime > TimeSpan.Zero) info.Duration = tl.EndTime;
            }
            catch { }

            var props = await session.TryGetMediaPropertiesAsync().AsTask();
            info.Title = props.Title ?? "";
            info.Artist = props.Artist ?? "";
            info.Album = props.AlbumTitle ?? "";

            if (_artDirty || _last is null ||
                _last.Title != info.Title || _last.Artist != info.Artist)
            {
                _artDirty = false;
                try
                {
                    var stream = await props.Thumbnail.OpenReadAsync().AsTask();
                    using var net = new System.IO.MemoryStream();
                    using var netStream = stream.AsStreamForRead();
                    await netStream.CopyToAsync(net);
                    info.AlbumArtPng = net.ToArray();
                }
                catch { info.AlbumArtPng = null; }
            }
            else
            {
                info.AlbumArtPng = _last.AlbumArtPng;
            }

            _last = info;
            Updated?.Invoke(info);
            return info;
        }
        catch
        {
            return _last;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task TogglePlayPauseAsync()
    {
        var session = _manager?.GetSessions().OrderByDescending(s => s.GetPlaybackInfo().PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing).FirstOrDefault();
        if (session is not null)
        {
            try { await session.TryTogglePlayPauseAsync().AsTask(); return; } catch { }
        }
        WindowsHelper.SendMediaPlayPause();
    }

    public void Dispose()
    {
        if (_manager is not null) _manager.SessionsChanged -= OnSessionsChanged;
    }
}

internal static class WindowsHelper
{
    public static void SendMediaPlayPause()
    {
        AetherIsland.Windows.PowerHelper.SendMediaKey(0xB3);
    }
}
