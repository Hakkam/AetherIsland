using System.Windows;
using System.Windows.Input;

namespace AetherIsland.App.Views;

public partial class AiCenterView
{
    public AiCenterView()
    {
        InitializeComponent();
    }

    private void ProviderCard_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: AetherIsland.App.ViewModels.ProviderCardVm card })
            card.IsExpanded = !card.IsExpanded;
    }
}
