using System.Windows;
using System.Windows.Controls;

namespace NoteApp.Views;

public partial class NoteListView : UserControl
{
    public NoteListView()
    {
        InitializeComponent();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.NoteListViewModel vm)
            vm.LoadNotesCommand.Execute(null);
    }
}
