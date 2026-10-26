using System.Windows;
using System.Windows.Media;
using AetherIsland.Core;
using AetherIsland.Core.Models;

namespace AetherIsland.App.Services;

/// <summary>Applies palette, accent, opacity and RTL flow direction to the running application.</summary>
public sealed class ThemeService
{
    public event Action? ThemeChanged;

    private readonly AppSettings _settings;

    public ThemeService(AppSettings settings) => _settings = settings;

    public bool IsDark { get; private set; } = true;

    public void Apply()
    {
        var theme = _settings.Appearance.Theme;
        IsDark = theme switch
        {
            "light" => false,
            "system" => !IsWindowsLightTheme(),
            _ => true
        };

        var uri = new Uri(IsDark ? "Themes/Dark.xaml" : "Themes/Light.xaml", UriKind.Relative);
        var palette = new ResourceDictionary { Source = uri };
        var app = Application.Current.Resources.MergedDictionaries;
        var existing = app.FirstOrDefault(d => d.Source is not null &&
            (d.Source.OriginalString.Contains("Dark.xaml") || d.Source.OriginalString.Contains("Light.xaml")));
        if (existing is not null) app.Remove(existing);
        // palette must precede Controls.xaml
        app.Insert(0, palette);

        // Accent override
        try
        {
            if (!string.IsNullOrWhiteSpace(_settings.Appearance.AccentHex))
            {
                var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_settings.Appearance.AccentHex));
                Application.Current.Resources["AccentDynamicBrush"] = brush;
            }
        }
        catch { }

        Application.Current.Resources["PanelOpacity"] = _settings.Appearance.PanelOpacity;
        Application.Current.Resources["OuterCornerRadius"] = _settings.Appearance.CornerRadius;

        Loc.SetLanguage(_settings.General.Language);
        ThemeChanged?.Invoke();
    }

    private static bool IsWindowsLightTheme()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int v && v == 1;
        }
        catch { return false; }
    }

    public static FlowDirection FlowDirectionForCurrentLanguage() =>
        Loc.IsRightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
}
