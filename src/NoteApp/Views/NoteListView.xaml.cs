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

    // Ctrl+F from anywhere outside a text block (MainWindow.OnFocusSearch). The
    // existing text is selected so the next keystroke replaces it.
    public void FocusSearchBox()
    {
        SearchBox.Focus();
        SearchBox.SelectAll();
    }
}
