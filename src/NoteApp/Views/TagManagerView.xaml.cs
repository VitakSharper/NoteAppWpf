using System.Windows;
using System.Windows.Controls;

namespace NoteApp.Views;

public partial class TagManagerView : UserControl
{
    public TagManagerView()
    {
        InitializeComponent();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.TagManagerViewModel vm)
            vm.LoadTagsCommand.Execute(null);
    }
}
