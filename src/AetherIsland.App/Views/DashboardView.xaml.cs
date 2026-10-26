using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using AetherIsland.App.Services;
using AetherIsland.App.ViewModels;

namespace AetherIsland.App.Views;

public partial class DashboardView
{
    public DashboardView()
    {
        InitializeComponent();
    }

    private DashboardViewModel? _vm;

    private void Dashboard_Loaded(object sender, RoutedEventArgs e)
    {
        _vm = DataContext as DashboardViewModel;
        if (_vm is not null) _vm.LayoutChanged += ApplyLayout;
        ApplyLayout();
    }

    private void Dashboard_Unloaded(object sender, RoutedEventArgs e)
    {
        if (_vm is not null) _vm.LayoutChanged -= ApplyLayout;
        _vm = null;
    }

    private void ApplyLayout()
    {
        var cards = new Dictionary<string, FrameworkElement>
        {
            ["weather"] = WeatherCard, ["toggles"] = TogglesCard,
            ["apps"] = AppsCard, ["dayprogress"] = DayCard
        };
        var settings = AppServices.Settings.Dashboard;
        var order = (settings.WidgetOrder ?? new()).Concat(cards.Keys).Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(cards.ContainsKey).ToList();
        var position = 0;
        foreach (var id in order)
        {
            var card = cards[id];
            var visible = !(settings.HiddenWidgets ?? new()).Contains(id);
            card.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            if (!visible) continue;
            Grid.SetRow(card, position / 2);
            Grid.SetColumn(card, position % 2);
            position++;
        }
    }

    private void WeatherCard_Click(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is AetherIsland.App.ViewModels.DashboardViewModel vm)
            vm.OpenWeatherModuleCommand.Execute(null);
    }
}
