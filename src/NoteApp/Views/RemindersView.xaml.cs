using System.Windows;
using System.Windows.Controls;

namespace NoteApp.Views;

public partial class RemindersView : UserControl
{
    public RemindersView()
    {
        InitializeComponent();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.RemindersViewModel vm)
            vm.LoadCommand.Execute(null);
    }
}
