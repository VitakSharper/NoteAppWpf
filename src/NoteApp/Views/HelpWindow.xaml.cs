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
            new("Ctrl + Alt + N", "From any application: a quick note — Ctrl+Enter saves it (its first line becomes the title), Esc drops it."),
            new("Ctrl + K", "Quick switcher: type part of a note's title and press Enter to open it — or run a command (new note, templates, archive, trash, settings, theme…)."),
            new("Alt + Left / Right", "Back / Forward through the notes you opened (also the mouse's side buttons and the arrows in the editor header)."),
            new("Ctrl + S", "Save the open note."),
            new("Esc", "Close the editor (asks first if the note has unsaved changes). Closes Settings when it is open, and leaves focus mode first when it is on."),
            new("Ctrl + F", "Find in the text block you are typing in; from anywhere else, jump to the note search."),
            new("Ctrl + B / I / U", "Bold, italic, underline in a text block."),
            new("Ctrl + Shift + X / H / C", "Strikethrough, highlighter, inline code in a text block (again to take it off)."),
            new("# ,  - ,  1. ,  [] ", "Typed at the start of a line of a text block, then a space: a heading (## and ### for smaller ones), a bullet list, a numbered list, a checklist block."),
            new("Click / Alt + Click", "In a text block: a click opens a link, Alt + Click puts the cursor in it to edit its text."),
            new("[[", "In a text block: link to another note — type part of its title, then Enter (Esc keeps the brackets)."),
            new("Ctrl + Click", "Opens the web address under the pointer anywhere — also in a checklist item or a link block, where a plain click edits."),
            new("Enter", "In the note search: run the search. In a text block find bar: next match (Shift + Enter: previous)."),
            new("Alt + Up / Down", "In a checklist item: move the item up or down."),
            new("Ctrl + wheel / + / -", "Zoom the note's blocks in or out (50 % to 250 %, remembered). Ctrl + 0: back to 100 %."),
            new("F11", "Focus mode: hide the rail and the note list while a note is open (Esc leaves it)."),
            new("F1", "This help.")
        };
    }

    private void OnClose(object sender, ExecutedRoutedEventArgs e) => Close();
}
