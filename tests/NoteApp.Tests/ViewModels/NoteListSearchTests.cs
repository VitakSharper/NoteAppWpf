using System.IO;
using NoteApp.Data.Queries;
using NoteApp.Domain.Functional;
using NoteApp.Domain.Models;
using NoteApp.Domain.ValueObjects;
using NoteApp.Services;
using NoteApp.ViewModels;

namespace NoteApp.Tests.ViewModels;

// What the list asks storage for, and the archive shelf.
public class NoteListSearchTests
{
    [Fact]
    public async Task The_search_box_and_the_chips_become_one_query()
    {
        var (list, repo) = List("Groceries");
        list.SearchText = "milk \"oat drink\" tag:home has:file is:pinned";
        await list.LoadNotes();
        list.SelectedTypeFilter = NoteTypeFilter.LinkOnly;
        await list.LoadNotes();

        var query = repo.LastQuery!;
        Assert.Equal(NoteShelf.Active, query.Shelf);
        Assert.Equal(["milk", "oat drink"], query.AllTerms);
        Assert.Equal(["home"], query.AllTagNames);
        Assert.Equal([BlockType.File, BlockType.Link], query.AllMustHave);
        Assert.True(query.PinnedOnly);
        Assert.Equal(["milk", "oat drink"], list.HighlightTerms);
    }

    [Fact]
    public async Task One_shelf_at_a_time()
    {
        var (list, repo) = List("Groceries");

        list.IsArchiveView = true;
        await list.LoadNotes();
        Assert.Equal(NoteShelf.Archived, repo.LastQuery!.Shelf);

        list.IsTrashView = true;
        await list.LoadNotes();
        Assert.False(list.IsArchiveView);
        Assert.Equal(NoteShelf.Trash, repo.LastQuery!.Shelf);

        list.IsArchiveView = true;
        Assert.False(list.IsTrashView);
    }

    [Fact]
    public async Task Archiving_takes_the_note_out_of_the_list_and_undo_puts_it_back()
    {
        var (list, repo) = List("Groceries");
        await list.LoadNotes();
        var note = Assert.Single(list.Notes);
        Action? undo = null;
        list.ShowUndoableMessage += (_, action) => undo = action;

        await list.ToggleArchiveCommand.ExecuteAsync(note);

        Assert.Empty(list.Notes);
        Assert.True(repo.IsArchived(note.Id));

        undo!();
        await Task.Delay(50);
        Assert.False(repo.IsArchived(note.Id));
        Assert.Single(list.Notes);
    }

    private static (NoteListViewModel List, Shelves Repo) List(params string[] titles)
    {
        var repo = new Shelves(titles);
        var settings = new AppSettingsService(Path.Combine(Path.GetTempPath(), "NoteApp.Tests", Guid.NewGuid() + ".json"));
        return (new NoteListViewModel(new NoteService(repo), new NoTags(), settings), repo);
    }

    private sealed class Shelves(string[] titles) : ThrowingNoteRepository
    {
        private readonly Dictionary<Guid, string> _notes = titles.ToDictionary(_ => Guid.NewGuid(), t => t);
        private readonly HashSet<Guid> _archived = [];

        public NoteQuery? LastQuery { get; private set; }

        public bool IsArchived(NoteId id) => _archived.Contains(id.Value);

        public override Task<Result<IReadOnlyList<NoteSummaryRow>, AppError>> SearchSummariesAsync(NoteQuery query)
        {
            LastQuery = query;
            var rows = _notes
                .Where(kv => query.Shelf switch
                {
                    NoteShelf.Archived => _archived.Contains(kv.Key),
                    NoteShelf.Trash => false,
                    _ => !_archived.Contains(kv.Key)
                })
                .Select(kv => new NoteSummaryRow
                {
                    Id = kv.Key, Title = kv.Value, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
                    ArchivedAt = _archived.Contains(kv.Key) ? DateTime.UtcNow : null, HasText = true
                })
                .ToList();
            return Task.FromResult(Result<IReadOnlyList<NoteSummaryRow>, AppError>.Ok(rows));
        }

        public override Task<Result<Unit, AppError>> SetArchivedAsync(NoteId id, bool isArchived)
        {
            if (isArchived) _archived.Add(id.Value); else _archived.Remove(id.Value);
            return Task.FromResult(Result<Unit, AppError>.Ok(Unit.Value));
        }
    }

    private sealed class NoTags : NoteApp.Data.Repositories.ITagRepository
    {
        public Task<Result<IReadOnlyList<Tag>, AppError>> GetAllAsync() => Task.FromResult(Result<IReadOnlyList<Tag>, AppError>.Ok([]));
        public Task<Result<Tag, AppError>> CreateAsync(Tag tag) => throw new NotSupportedException();
        public Task<Result<Tag, AppError>> UpdateAsync(Tag tag) => throw new NotSupportedException();
        public Task<Result<Unit, AppError>> DeleteAsync(Guid id) => throw new NotSupportedException();
    }
}
