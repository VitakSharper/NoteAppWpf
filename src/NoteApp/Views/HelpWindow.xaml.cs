using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Input;

namespace NoteApp.Views;

public sealed record HelpShortcut(string Keys, string Description);

// The in-app help: static text plus the shortcut table. Kept as one instance by
// MainViewModel.ShowHelp so F1 brings the same window back rather than a second one.
public partial class HelpWindow : Window
{
    public HelpWindow()
    {
        InitializeComponent();

        var version = Assembly.GetExecutingAssembly().GetName().Version;
        VersionText.Text = version is null ? string.Empty : $"Version {version.Major}.{version.Minor}.{version.Build}";

        // Same folder App.xaml.cs and AppSettingsService write to.
        DataFolderText.Text = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NoteApp");

        ShortcutsList.ItemsSource = new HelpShortcut[]
        {
            new("Ctrl + N", "New note."),
            new("Ctrl + S", "Save the open note."),
            new("Esc", "Close the editor (asks first if the note has unsaved changes). Closes Settings when it is open."),
            new("Ctrl + F", "Find in the text block you are typing in; from anywhere else, jump to the note search."),
            new("Ctrl + B / I / U", "Bold, italic, underline in a text block."),
            new("Enter", "In the note search: run the search. In a text block find bar: next match (Shift + Enter: previous)."),
            new("F1", "This help.")
        };
    }

    private void OnClose(object sender, ExecutedRoutedEventArgs e) => Close();
}
