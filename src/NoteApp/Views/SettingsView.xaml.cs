using System.Windows;
using System.Windows.Controls;
using NoteApp.ViewModels;

namespace NoteApp.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
    }

    private void OnClose(object sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel vm)
            vm.CloseCommand.Execute(null);
    }
}
