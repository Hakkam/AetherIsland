using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AetherIsland.App.ViewModels;

namespace AetherIsland.App.Views;

public partial class FileShelfView
{
    public FileShelfView() => InitializeComponent();

    private FileShelfViewModel? VM => DataContext as FileShelfViewModel;

    private void View_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void View_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files && VM is not null)
            VM.AddDroppedPaths(files);
    }

    private ShelfItemVm? _dragCandidate;

    private void Item_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragCandidate = (sender as FrameworkElement)?.DataContext as ShelfItemVm;
        if (e.ClickCount == 2 && _dragCandidate is not null)
            VM?.OpenCommand.Execute(_dragCandidate);
    }

    private void Item_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || VM is null) return;
        if (_dragCandidate is null) return;
        var item = _dragCandidate;
        _dragCandidate = null;
        if (sender is UIElement element)
            VM.BeginDragOut(item, element);
    }
}
