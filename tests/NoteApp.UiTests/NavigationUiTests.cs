using NoteApp.Data.Queries;
using NoteApp.Domain.Functional;
using NoteApp.Domain.Models;
using NoteApp.Domain.ValueObjects;
using NoteApp.ViewModels;
using NoteApp.Views;
using static NoteApp.UiTests.Notes;

namespace NoteApp.UiTests;

public class NavigationUiTests
{
    [Fact]
    public void Back_and_forward_reopen_the_notes_opened_before() => Wpf.Run(() =>
    {
        var repo = new ThreeNotes();
        var shell = FocusModeUiTests.Shell(repo);
        Open(shell, repo.A);
        Open(shell, repo.B);

        shell.GoBackCommand.ExecuteAsync(null).GetAwaiter().GetResult();
        Assert.Equal(repo.A, OpenId(shell));

        shell.GoForwardCommand.ExecuteAsync(null).GetAwaiter().GetResult();
        Assert.Equal(repo.B, OpenId(shell));
        Assert.False(shell.GoForwardCommand.CanExecute(null));
    });

    // With the editor closed, Back first brings back the note that was open.
    [Fact]
    public void Back_after_closing_the_editor_reopens_the_last_note() => Wpf.Run(() =>
    {
        var repo = new ThreeNotes();
        var shell = FocusModeUiTests.Shell(repo);
        Open(shell, repo.A);
        Open(shell, repo.B);
        shell.DismissCommand.ExecuteAsync(null).GetAwaiter().GetResult();
        Assert.Null(shell.CurrentEditor);

        shell.GoBackCommand.ExecuteAsync(null).GetAwaiter().GetResult();

        Assert.Equal(repo.B, OpenId(shell));
    });

    [Fact]
    public void The_switcher_offers_notes_and_commands_and_opens_the_one_chosen() => Wpf.Run(() =>
    {
        var repo = new ThreeNotes();
        var shell = FocusModeUiTests.Shell(repo);

        var entries = shell.QuickSwitchEntriesAsync().GetAwaiter().GetResult();
        Assert.Equal(["Groceries", "Plan", "Wifi"], entries.OfType<NoteEntry>().Select(n => n.Title).Order());
        Assert.Contains(entries, e => e is CommandEntry { Label: "New note" });

        var switcher = new QuickSwitcherWindow(entries) { Left = -10000, Top = -10000, ShowActivated = false };
        switcher.Show();
        switcher.QueryBox.Text = "wif";
        Wpf.Pump();
        var first = Assert.IsType<NoteEntry>(switcher.Results.Items[0]);
        Assert.Equal("Wifi", first.Title);
        switcher.Choose(first);

        shell.RunQuickSwitchAsync(switcher.Chosen!).GetAwaiter().GetResult();
        Assert.Equal(repo.C, OpenId(shell));
    });

    private static void Open(MainViewModel shell, NoteId id) =>
        Assert.True(shell.OpenNoteByIdAsync(id).GetAwaiter().GetResult());

    private static NoteId? OpenId(MainViewModel shell) => (shell.CurrentEditor as NoteEditorViewModel)?.EditedNoteId;

    private sealed class ThreeNotes : UnusedNoteRepository
    {
        public NoteId A { get; } = NoteId.New();
        public NoteId B { get; } = NoteId.New();
        public NoteId C { get; } = NoteId.New();

        private string TitleOf(NoteId id) => id == A ? "Plan" : id == B ? "Groceries" : "Wifi";

        public override Task<Result<Note, AppError>> GetByIdAsync(NoteId id) =>
            Task.FromResult(Result<Note, AppError>.Ok(
                With(Text(TitleOf(id))) with { Id = id, Title = NoteTitle.From(TitleOf(id)).Match(t => t, e => throw new InvalidOperationException(e.Message)) }));

        public override Task<Result<DateTime?, AppError>> GetReminderAsync(NoteId id) =>
            Task.FromResult(Result<DateTime?, AppError>.Ok(null));

        public override Task<Result<IReadOnlyList<NoteRow>, AppError>> LinkedFromAsync(NoteId id) =>
            Task.FromResult(Result<IReadOnlyList<NoteRow>, AppError>.Ok([]));

        public override Task<Result<IReadOnlyList<NoteSummaryRow>, AppError>> SearchSummariesAsync(NoteQuery query) =>
            Task.FromResult(Result<IReadOnlyList<NoteSummaryRow>, AppError>.Ok(
                new[] { A, B, C }.Select(id => new NoteSummaryRow { Id = id.Value, Title = TitleOf(id), HasText = true }).ToList()));

        public override Task<Result<IReadOnlyList<NoteRow>, AppError>> TemplatesAsync() =>
            Task.FromResult(Result<IReadOnlyList<NoteRow>, AppError>.Ok([]));
    }
}
