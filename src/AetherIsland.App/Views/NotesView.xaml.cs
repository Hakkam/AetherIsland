using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AetherIsland.App.ViewModels;

namespace AetherIsland.App.Views;

public partial class NotesView
{
    public NotesView() => InitializeComponent();

    private void NoteRow_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: NoteRowVm row } &&
            DataContext is NotesViewModel vm)
            vm.SelectedNote = row;
    }
}
