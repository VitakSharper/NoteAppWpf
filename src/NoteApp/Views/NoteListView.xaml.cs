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

    // Markdown and text files dropped on the list become notes (MainViewModel.ImportFilesAsync).
    private void OnFilesDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetData(DataFormats.FileDrop) is string[] paths && paths.Any(Services.Import.MarkdownImport.CanImport)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private async void OnFilesDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths || Window.GetWindow(this)?.DataContext is not ViewModels.MainViewModel shell)
            return;

        e.Handled = true;
        await shell.ImportFilesAsync(paths);
    }

    // Ctrl+F from anywhere outside a text block (MainWindow.OnFocusSearch). The
    // existing text is selected so the next keystroke replaces it.
    public void FocusSearchBox()
    {
        SearchBox.Focus();
        SearchBox.SelectAll();
    }
}
