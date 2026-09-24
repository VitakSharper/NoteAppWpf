using System.IO;
using System.Windows.Controls;
using NoteApp.Data.Queries;
using NoteApp.Data.Repositories;
using NoteApp.Domain.Functional;
using NoteApp.Domain.Models;
using NoteApp.Domain.ValueObjects;
using NoteApp.Services;
using NoteApp.ViewModels;
using NoteApp.Views;

namespace NoteApp.UiTests;

public class NoteListUiTests
{
    [Fact]
    public void A_date_sort_shows_headings_and_a_title_sort_does_not() => Wpf.Run(() =>
    {
        var now = DateTime.UtcNow;
        var vm = List(("pinned one", now.AddDays(-50), true), ("fresh", now, false), ("older", now.AddDays(-1), false));
        var view = new NoteListView { DataContext = vm };
        var window = Wpf.Show(view, 400, 800);
        try
        {
            Wpf.Pump();
            Assert.Equal(["pinned one", "fresh", "older"], vm.Notes.Select(n => n.Title.Value));
            Assert.Equal(["Pinned", "Today", "Yesterday"], Headings(view));

            vm.SelectedSort = SortOption.TitleAsc;
            Wpf.Pump();
            Assert.Empty(Headings(view));
        }
        finally
        {
            window.Close();
        }
    });

    private static List<string> Headings(NoteListView view) =>
        Wpf.Descendants<GroupItem>(view)
            .Select(g => ((System.Windows.Data.CollectionViewGroup)g.DataContext).Name as string ?? "")
            .ToList();

    private static NoteListViewModel List(params (string Title, DateTime UpdatedUtc, bool Pinned)[] notes)
    {
        var settings = new AppSettingsService(Path.Combine(Path.GetTempPath(), "NoteApp.UiTests", $"{Guid.NewGuid()}.json"));
        return new NoteListViewModel(new NoteService(new Rows(notes)), new NoTags(), settings);
    }

    private sealed class Rows((string Title, DateTime UpdatedUtc, bool Pinned)[] notes) : INoteRepository
    {
        public Task<Result<IReadOnlyList<NoteSummaryRow>, AppError>> SearchSummariesAsync(string? searchText, IReadOnlyList<Guid>? tagIds, BlockType? blockType, bool deletedOnly = false) =>
            Task.FromResult(Result<IReadOnlyList<NoteSummaryRow>, AppError>.Ok(deletedOnly ? [] : notes.Select(n => new NoteSummaryRow
            {
                Id = Guid.NewGuid(),
                Title = n.Title,
                CreatedAt = DateTime.SpecifyKind(n.UpdatedUtc, DateTimeKind.Unspecified),
                UpdatedAt = DateTime.SpecifyKind(n.UpdatedUtc, DateTimeKind.Unspecified),
                IsPinned = n.Pinned,
                HasText = true,
                FirstTextPlain = "text"
            }).ToList()));

        public Task<Result<Note, AppError>> GetByIdAsync(NoteId id) => throw new NotSupportedException();
        public Task<Result<Note, AppError>> CreateAsync(Note note, byte[]? encryptedContent = null) => throw new NotSupportedException();
        public Task<Result<Note, AppError>> UpdateAsync(Note note, byte[]? encryptedContent = null) => throw new NotSupportedException();
        public Task<Result<Unit, AppError>> DeleteAsync(NoteId id) => throw new NotSupportedException();
        public Task<Result<Unit, AppError>> RestoreAsync(NoteId id) => throw new NotSupportedException();
        public Task<Result<Unit, AppError>> SetPinnedAsync(NoteId id, bool isPinned) => throw new NotSupportedException();
        public Task<Result<Unit, AppError>> PurgeAsync(NoteId id) => throw new NotSupportedException();
        public Task<Result<int, AppError>> PurgeAllDeletedAsync() => throw new NotSupportedException();
        public Task<Result<byte[]?, AppError>> GetEncryptedContentAsync(NoteId id) => throw new NotSupportedException();
    }

    private sealed class NoTags : ITagRepository
    {
        public Task<Result<IReadOnlyList<Tag>, AppError>> GetAllAsync() => Task.FromResult(Result<IReadOnlyList<Tag>, AppError>.Ok([]));
        public Task<Result<Tag, AppError>> CreateAsync(Tag tag) => throw new NotSupportedException();
        public Task<Result<Tag, AppError>> UpdateAsync(Tag tag) => throw new NotSupportedException();
        public Task<Result<Unit, AppError>> DeleteAsync(Guid id) => throw new NotSupportedException();
    }
}
