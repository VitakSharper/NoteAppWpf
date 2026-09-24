using System.Windows;
using System.Windows.Input;

namespace NoteApp.Views;

// The quick-capture window (Ctrl+Alt+N, or the tray's Quick note). Save hands the title and
// the text to the callback; a failure (a database down) keeps the window and says why.
public partial class QuickNoteWindow : Window
{
    private readonly Func<string, string, Task<string?>> _save;

    // save: returns an error message, or null once the note is stored.
    public QuickNoteWindow(Func<string, string, Task<string?>> save)
    {
        InitializeComponent();
        _save = save;
        Loaded += (_, _) =>
        {
            Activate();
            BodyBox.Focus();
        };
    }

    private async void OnSave(object sender, ExecutedRoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(BodyBox.Text) && string.IsNullOrWhiteSpace(TitleBox.Text))
        {
            Close();
            return;
        }

        IsEnabled = false;
        var error = await _save(TitleBox.Text, BodyBox.Text);
        IsEnabled = true;
        if (error is null)
            Close();
        else
            Error.Text = error;
    }

    private void OnCancel(object sender, ExecutedRoutedEventArgs e) => Close();
}
