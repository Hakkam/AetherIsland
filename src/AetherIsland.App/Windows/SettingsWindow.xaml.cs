using System.Windows;
using System.Windows.Controls;
using AetherIsland.App.Services;
using AetherIsland.App.ViewModels;

namespace AetherIsland.App.Windows;

public partial class SettingsWindow : Window
{
    private static SettingsWindow? _open;

    public static void ShowOnce(string? section = null)
    {
        if (_open is { IsLoaded: true })
        {
            _open.Activate();
            _open.NavigateSection(section);
            return;
        }
        _open = new SettingsWindow();
        _open.Show();
        _open.NavigateSection(section);
    }

    private void NavigateSection(string? section)
    {
        if (section == "ai")
            Dispatcher.BeginInvoke((Action)(() => AiSectionHeader.BringIntoView()));
    }

    public SettingsWindow()
    {
        InitializeComponent();
        DataContext = new SettingsViewModel();
        ApplyLanguageDirection();
        AppServices.Theme.ThemeChanged += ApplyLanguageDirection;
        Closed += (_, _) => AppServices.Theme.ThemeChanged -= ApplyLanguageDirection;
    }

    private SettingsViewModel VM => (SettingsViewModel)DataContext;

    private void ApplyLanguageDirection() =>
        FlowDirection = ThemeService.FlowDirectionForCurrentLanguage();

    private void OnChanged(object sender, RoutedEventArgs e)
    {
        if (IsLoaded && DataContext is SettingsViewModel vm) vm.Save();
    }

    private void ProviderKey_Changed(object sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel vm) vm.NewProviderKey = ProviderKeyBox.Password;
    }

    private void AddProvider_Click(object sender, RoutedEventArgs e)
    {
        VM.AddProviderCommand.Execute(null);
        if (string.IsNullOrEmpty(VM.NewProviderKey)) ProviderKeyBox.Clear();
    }

    private void SaveAndClose_Click(object sender, RoutedEventArgs e)
    {
        VM.Save();
        Close();
    }

    private void OnTestProvider(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is AiProviderRowVm row)
            _ = VM.TestProviderAsync(row);
    }
}
