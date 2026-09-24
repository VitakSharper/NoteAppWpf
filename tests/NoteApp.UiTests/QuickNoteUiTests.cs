using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using NoteApp.Domain.Functional;
using NoteApp.Domain.Models;
using NoteApp.Services;
using NoteApp.Views;

namespace NoteApp.UiTests;

public class QuickNoteUiTests
{
    [Fact]
    public void A_quick_note_is_one_text_block_titled_by_its_first_line() => Wpf.Run(() =>
    {
        var repo = new Capturing();
        var shell = FocusModeUiTests.Shell(repo);

        var error = shell.SaveQuickNoteAsync("", "\r\nCall the bank\nabout https://bank.example.com/card\n").GetAwaiter().GetResult();

        Assert.Null(error);
        var note = repo.Created!;
        Assert.Equal("Call the bank", note.Title.Value);
        var text = Assert.IsType<NoteBlock.Text>(Assert.Single(note.Blocks));
        Assert.Equal("Call the bank\nabout https://bank.example.com/card", text.PlainText.ReplaceLineEndings("\n"));
        Assert.Equal(text.PlainText.ReplaceLineEndings(), text.PlainText);
        var document = Notes.Load(text.RichText);
        Assert.Equal(2, document.Blocks.Count);
        Assert.Equal("https://bank.example.com/card", Notes.TextOf(Assert.Single(Notes.Hyperlinks(document))));
    });

    [Fact]
    public void The_window_saves_through_its_callback_and_stays_open_on_an_error() => Wpf.Run(() =>
    {
        string? failWith = "The database is not reachable.";
        (string Title, string Text)? saved = null;
        var window = new QuickNoteWindow((title, text) =>
        {
            saved = (title, text);
            return Task.FromResult<string?>(failWith);
        }) { Left = -10000, Top = -10000, ShowActivated = false, Topmost = false };
        window.Show();
        window.TitleBox.Text = "Idea";
        window.BodyBox.Text = "tea";

        ApplicationCommands.Save.Execute(null, window);
        Wpf.Pump();
        Assert.Equal(("Idea", "tea"), saved);
        Assert.True(window.IsVisible);
        Assert.Equal("The database is not reachable.", window.Error.Text);

        failWith = null;
        ApplicationCommands.Save.Execute(null, window);
        Wpf.Pump();
        Assert.False(window.IsVisible);
    });

    // A hotkey arrives as WM_HOTKEY on the main window; Ctrl+Alt+Shift+F12 stays clear of
    // anything a person uses.
    [Fact]
    public void The_global_shortcut_calls_back_on_its_message() => Wpf.Run(() =>
    {
        var window = new Window { Left = -10000, Top = -10000, Width = 10, Height = 10, ShowActivated = false, ShowInTaskbar = false };
        var pressed = 0;
        using (var hotKey = new GlobalHotKey(window, ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift, Key.F12, () => pressed++))
        {
            var handle = new WindowInteropHelper(window).Handle;
            SendMessage(handle, 0x0312, new IntPtr(0x4E41), IntPtr.Zero);
            SendMessage(handle, 0x0312, new IntPtr(0x1234), IntPtr.Zero);
        }
        window.Close();

        Assert.Equal(1, pressed);
    });

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    private sealed class Capturing : UnusedNoteRepository
    {
        public Note? Created { get; private set; }

        public override Task<Result<Note, AppError>> CreateAsync(Note note, byte[]? encryptedContent = null)
        {
            Created = note;
            return Task.FromResult(Result<Note, AppError>.Ok(note));
        }

        public override Task<Result<IReadOnlyList<NoteApp.Data.Queries.NoteSummaryRow>, AppError>> SearchSummariesAsync(NoteQuery query) =>
            Task.FromResult(Result<IReadOnlyList<NoteApp.Data.Queries.NoteSummaryRow>, AppError>.Ok([]));
    }
}
