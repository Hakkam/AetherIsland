using System.Windows;
using AetherIsland.Core;
using AetherIsland.Core.Storage;
using AetherIsland.Data;

namespace AetherIsland.App.Services;

/// <summary>User-visible feedback: tray balloon tips plus compact island context events.</summary>
public sealed class NotifyService
{
    private readonly TrayIconService _tray;

    public event Action<(string Icon, string Message)>? IslandContext; // transient pill event
    public event Action<(string Title, string Message)>? Balloon;

    public NotifyService(TrayIconService tray) => _tray = tray;

    public void ShowBalloon(string title, string message)
    {
        try
        {
            _tray.ShowBalloon(title, message);
        }
        catch { }
        Balloon?.Invoke((title, message));
    }

    public void ShowIslandContext(string glyph, string message, int seconds = 6)
    {
        IslandContext?.Invoke((glyph, message));
    }

    public void HideBalloon() => _tray.CloseBalloon();
}
