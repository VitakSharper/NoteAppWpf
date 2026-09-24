using System.IO;
using NoteApp.Domain.Functional;
using NoteApp.Domain.Models;
using NoteApp.Data.Repositories;
using NoteApp.Services;
using NoteApp.ViewModels;

namespace NoteApp.UiTests;

// The shell's focus mode: only with a note open, Escape leaves it before closing anything,
// and it goes when the note does. MainViewModel needs a dispatcher (MDIX's snackbar queue).
public class FocusModeUiTests
{
    [Fact]
    public void Focus_mode_needs_an_open_note() => Wpf.Run(() =>
        Assert.False(Shell().ToggleFocusModeCommand.CanExecute(null)));

    [Fact]
    public void Escape_leaves_focus_mode_before_closing_the_editor() => Wpf.Run(() =>
    {
        var shell = Shell();
        shell.CreateNoteCommand.Execute(null);
        Wpf.Pump();
        shell.ToggleFocusModeCommand.Execute(null);
        Assert.True(shell.IsFocusMode);

        shell.DismissCommand.Execute(null);
        Wpf.Pump();

        Assert.False(shell.IsFocusMode);
        Assert.NotNull(shell.CurrentEditor);
    });

    [Fact]
    public void Closing_the_note_leaves_focus_mode() => Wpf.Run(() =>
    {
        var shell = Shell();
        shell.CreateNoteCommand.Execute(null);
        Wpf.Pump();
        shell.ToggleFocusModeCommand.Execute(null);

        shell.CurrentEditor = null;

        Assert.False(shell.IsFocusMode);
    });

    internal static MainViewModel Shell(INoteRepository? repository = null)
    {
        var folder = Path.Combine(Path.GetTempPath(), "NoteApp.UiTests", $"shell-{Guid.NewGuid()}");
        var settings = new AppSettingsService(Path.Combine(folder, "settings.json"));
        var notes = new NoteService(repository ?? new UnusedNoteRepository());
        var tags = new NoTags();
        var backup = new BackupService("Server=.;Database=none", "none");
        return new MainViewModel(notes, settings, tags,
            new NoteListViewModel(notes, tags, settings),
            new TagManagerViewModel(tags, settings),
            new SettingsViewModel(settings, backup),
            new AutoBackupService(settings, backup),
            new DraftStore(Path.Combine(folder, "Drafts")));
    }

    private sealed class NoTags : ITagRepository
    {
        public Task<Result<IReadOnlyList<Tag>, AppError>> GetAllAsync() => Task.FromResult(Result<IReadOnlyList<Tag>, AppError>.Ok([]));
        public Task<Result<Tag, AppError>> CreateAsync(Tag tag) => throw new NotSupportedException();
        public Task<Result<Tag, AppError>> UpdateAsync(Tag tag) => throw new NotSupportedException();
        public Task<Result<Unit, AppError>> DeleteAsync(Guid id) => throw new NotSupportedException();
    }
}
