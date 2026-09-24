using System.Reflection;
using System.Windows;
using System.Windows.Input;
using NoteApp.Services;

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
        DataFolderText.Text = AppPaths.DataFolder;

        ShortcutsList.ItemsSource = new HelpShortcut[]
        {
            new("Ctrl + N", "New note."),
            new("Ctrl + S", "Save the open note."),
            new("Esc", "Close the editor (asks first if the note has unsaved changes). Closes Settings when it is open."),
            new("Ctrl + F", "Find in the text block you are typing in; from anywhere else, jump to the note search."),
            new("Ctrl + B / I / U", "Bold, italic, underline in a text block."),
            new("Ctrl + Click", "Open the web address under the pointer, in a text block, a checklist item or a link block. A plain click edits it."),
            new("Enter", "In the note search: run the search. In a text block find bar: next match (Shift + Enter: previous)."),
            new("Alt + Up / Down", "In a checklist item: move the item up or down."),
            new("F1", "This help.")
        };
    }

    private void OnClose(object sender, ExecutedRoutedEventArgs e) => Close();
}
