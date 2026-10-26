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

    private void Dashboard_SizeChanged(object sender, SizeChangedEventArgs e) => ApplyLayout();

    private void ApplyLayout()
    {
        // Reference-faithful single-row layout on wide panels; compact 2x2 fallback.
        var columns = DashboardGrid.ActualWidth >= 780 ? 4 : 2;
        for (var i = 0; i < DashboardGrid.ColumnDefinitions.Count; i++)
            DashboardGrid.ColumnDefinitions[i].Width = i < columns
                ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        DashboardGrid.RowDefinitions[1].Height = columns == 4
            ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
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
            Grid.SetRow(card, position / columns);
            Grid.SetColumn(card, position % columns);
            card.Margin = columns == 4
                ? new Thickness(position == 0 ? 0 : 5, 0, position == 3 ? 0 : 5, 0)
                : new Thickness(position % 2 == 0 ? 0 : 5, position < 2 ? 0 : 5,
                    position % 2 == 0 ? 5 : 0, position < 2 ? 5 : 0);
            position++;
        }
    }

    private void WeatherCard_Click(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is AetherIsland.App.ViewModels.DashboardViewModel vm)
            vm.OpenWeatherModuleCommand.Execute(null);
    }
}
