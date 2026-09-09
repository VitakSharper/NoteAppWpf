using System.IO;
using NoteApp.Data.Queries;
using NoteApp.Data.Repositories;
using NoteApp.Domain.Functional;
using NoteApp.Domain.Models;
using NoteApp.Domain.ValueObjects;
using NoteApp.Services;
using NoteApp.ViewModels;

namespace NoteApp.Tests.ViewModels;

// The list view model over an in-memory repository: what a delete does, what UNDO
// does, and that a trashed row never reaches the editor. Permanent deletion goes
// through a MessageBox and is left to a human.
public class NoteListTrashTests
{
    [Fact]
    public async Task Deleting_moves_the_note_to_the_trash_and_offers_undo()
    {
        var (vm, repo) = ListWith("Groceries");
        await vm.LoadNotes();
        var note = Assert.Single(vm.Notes);
        (string Message, Action Undo)? offered = null;
        vm.ShowUndoableMessage += (message, undo) => offered = (message, undo);

        await vm.DeleteNoteCommand.ExecuteAsync(note);

        Assert.Equal([note.Id.Value], repo.Deleted);
        Assert.Empty(vm.Notes);
        Assert.NotNull(offered);
        Assert.Contains("Groceries", offered.Value.Message);
        Assert.Contains("trash", offered.Value.Message);
    }

    [Fact]
    public async Task Undo_restores_the_note_into_the_list()
    {
        var (vm, repo) = ListWith("Groceries");
        await vm.LoadNotes();
        var note = Assert.Single(vm.Notes);
        Action? undo = null;
        vm.ShowUndoableMessage += (_, action) => undo = action;
        await vm.DeleteNoteCommand.ExecuteAsync(note);

        undo!();
        if (vm.RestoreNoteCommand.ExecutionTask is { } running)
            await running;

        Assert.Equal([note.Id.Value], repo.Restored);
        Assert.Equal("Groceries", Assert.Single(vm.Notes).Title.Value);
    }

    [Fact]
    public async Task Trash_view_lists_only_the_deleted_notes()
    {
        var (vm, repo) = ListWith("Kept", "Binned");
        repo.Trash(repo.IdOf("Binned"));

        vm.IsTrashView = true;
        await vm.LoadNotes();

        var row = Assert.Single(vm.Notes);
        Assert.Equal("Binned", row.Title.Value);
        Assert.True(row.IsDeleted);
    }

    [Fact]
    public async Task Selecting_a_trashed_note_does_not_open_the_editor()
    {
        var (vm, repo) = ListWith("Binned");
        repo.Trash(repo.IdOf("Binned"));
        vm.IsTrashView = true;
        await vm.LoadNotes();
        var opened = false;
        vm.EditNoteRequested += _ => opened = true;

        vm.SelectedNote = vm.Notes[0];

        Assert.False(opened);
    }

    [Fact]
    public async Task Selecting_a_live_note_still_opens_it()
    {
        var (vm, _) = ListWith("Groceries");
        await vm.LoadNotes();
        NoteSummary? opened = null;
        vm.EditNoteRequested += n => opened = n;

        vm.SelectedNote = vm.Notes[0];

        Assert.Equal("Groceries", opened?.Title.Value);
    }

    [Fact]
    public async Task Restoring_from_the_trash_takes_the_note_out_of_it()
    {
        var (vm, repo) = ListWith("Binned");
        repo.Trash(repo.IdOf("Binned"));
        vm.IsTrashView = true;
        await vm.LoadNotes();

        await vm.RestoreNoteCommand.ExecuteAsync(vm.Notes[0]);

        Assert.Equal([repo.IdOf("Binned")], repo.Restored);
        Assert.Empty(vm.Notes);
    }

    [Fact]
    public async Task Empty_trash_is_only_offered_in_a_non_empty_trash_view()
    {
        var (vm, repo) = ListWith("Kept", "Binned");
        await vm.LoadNotes();
        Assert.False(vm.EmptyTrashCommand.CanExecute(null));

        repo.Trash(repo.IdOf("Binned"));
        vm.IsTrashView = true;
        await vm.LoadNotes();

        Assert.True(vm.EmptyTrashCommand.CanExecute(null));
    }

    private static (NoteListViewModel Vm, FakeNoteRepository Repo) ListWith(params string[] titles)
    {
        var repo = new FakeNoteRepository();
        foreach (var title in titles)
            repo.Add(title);

        // A settings file that does not exist yields the defaults, without touching
        // the real %LocalAppData% one.
        var settings = new AppSettingsService(Path.Combine(Path.GetTempPath(), "NoteApp.Tests", Guid.NewGuid() + ".json"));
        var vm = new NoteListViewModel(new NoteService(repo), new EmptyTagRepository(), settings);
        return (vm, repo);
    }

    // Titles keyed by id plus the set of trashed ids; rows are built per query the
    // way the real projection would, DeletedAt included.
    private sealed class FakeNoteRepository : INoteRepository
    {
        private readonly Dictionary<Guid, string> _titles = [];
        private readonly Dictionary<Guid, DateTime> _trashed = [];

        public List<Guid> Deleted { get; } = [];
        public List<Guid> Restored { get; } = [];

        public Guid Add(string title)
        {
            var id = Guid.NewGuid();
            _titles[id] = title;
            return id;
        }

        public Guid IdOf(string title) => _titles.Single(kv => kv.Value == title).Key;
        public void Trash(Guid id) => _trashed[id] = DateTime.UtcNow;

        public Task<Result<Unit, AppError>> DeleteAsync(NoteId id)
        {
            Deleted.Add(id.Value);
            _trashed[id.Value] = DateTime.UtcNow;
            return Task.FromResult(Result<Unit, AppError>.Ok(Unit.Value));
        }

        public Task<Result<Unit, AppError>> RestoreAsync(NoteId id)
        {
            Restored.Add(id.Value);
            _trashed.Remove(id.Value);
            return Task.FromResult(Result<Unit, AppError>.Ok(Unit.Value));
        }

        public Task<Result<IReadOnlyList<NoteSummaryRow>, AppError>> SearchSummariesAsync(
            string? searchText, IReadOnlyList<Guid>? tagIds, BlockType? blockType, bool deletedOnly = false)
        {
            var rows = _titles
                .Where(kv => _trashed.ContainsKey(kv.Key) == deletedOnly)
                .Select(kv => new NoteSummaryRow
                {
                    Id = kv.Key,
                    Title = kv.Value,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    DeletedAt = _trashed.TryGetValue(kv.Key, out var at) ? at : null,
                    HasText = true,
                    FirstTextPlain = "plain"
                })
                .ToList();
            return Task.FromResult(Result<IReadOnlyList<NoteSummaryRow>, AppError>.Ok(rows));
        }

        public Task<Result<Note, AppError>> GetByIdAsync(NoteId id) => throw new NotSupportedException();
        public Task<Result<Note, AppError>> CreateAsync(Note note, byte[]? encryptedContent = null) => throw new NotSupportedException();
        public Task<Result<Note, AppError>> UpdateAsync(Note note, byte[]? encryptedContent = null) => throw new NotSupportedException();
        public Task<Result<Unit, AppError>> PurgeAsync(NoteId id) => throw new NotSupportedException();
        public Task<Result<int, AppError>> PurgeAllDeletedAsync() => throw new NotSupportedException();
        public Task<Result<byte[]?, AppError>> GetEncryptedContentAsync(NoteId id) => throw new NotSupportedException();
    }

    private sealed class EmptyTagRepository : ITagRepository
    {
        public Task<Result<IReadOnlyList<Tag>, AppError>> GetAllAsync() =>
            Task.FromResult(Result<IReadOnlyList<Tag>, AppError>.Ok([]));
        public Task<Result<Tag, AppError>> CreateAsync(Tag tag) => throw new NotSupportedException();
        public Task<Result<Tag, AppError>> UpdateAsync(Tag tag) => throw new NotSupportedException();
        public Task<Result<Unit, AppError>> DeleteAsync(Guid id) => throw new NotSupportedException();
    }
}
