using System.IO;
using System.Windows.Controls;
using NoteApp.Data.Entities;
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

    [Fact]
    public void Tag_chips_take_their_tags_colour() => Wpf.Run(() =>
    {
        var vm = List(("tagged", DateTime.UtcNow, false));
        var view = new NoteListView { DataContext = vm };
        var window = Wpf.Show(view, 400, 800);
        try
        {
            Wpf.Pump();
            var chip = Wpf.Descendants<Border>(view).First(b => b.Child is TextBlock { Text: "blue one" });
            var blue = NoteApp.Theme.TagPalette.For(TagColor.Blue);
            Assert.Same(blue.Background, chip.Background);
            Assert.Same(blue.Border, chip.BorderBrush);
            Assert.Same(blue.Foreground, ((TextBlock)chip.Child).Foreground);
            var plain = Wpf.Descendants<Border>(view).First(b => b.Child is TextBlock { Text: "plain one" });
            Assert.Same(NoteApp.Theme.TagPalette.For(TagColor.Violet).Background, plain.Background);
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

    private sealed class Rows((string Title, DateTime UpdatedUtc, bool Pinned)[] notes) : UnusedNoteRepository
    {
        public override Task<Result<IReadOnlyList<NoteSummaryRow>, AppError>> SearchSummariesAsync(NoteQuery query) =>
            Task.FromResult(Result<IReadOnlyList<NoteSummaryRow>, AppError>.Ok(query.Shelf == NoteShelf.Trash ? [] : notes.Select(n => new NoteSummaryRow
            {
                Id = Guid.NewGuid(),
                Title = n.Title,
                CreatedAt = DateTime.SpecifyKind(n.UpdatedUtc, DateTimeKind.Unspecified),
                UpdatedAt = DateTime.SpecifyKind(n.UpdatedUtc, DateTimeKind.Unspecified),
                IsPinned = n.Pinned,
                Tags = [new TagEntity { Id = Guid.NewGuid(), Name = "blue one", Color = "Blue" }, new TagEntity { Id = Guid.NewGuid(), Name = "plain one" }],
                HasText = true,
                FirstTextPlain = "text"
            }).ToList()));
        public override Task<Result<byte[]?, AppError>> GetEncryptedContentAsync(NoteId id) => throw new NotSupportedException();
    }

    private sealed class NoTags : ITagRepository
    {
        public Task<Result<IReadOnlyList<Tag>, AppError>> GetAllAsync() => Task.FromResult(Result<IReadOnlyList<Tag>, AppError>.Ok([]));
        public Task<Result<Tag, AppError>> CreateAsync(Tag tag) => throw new NotSupportedException();
        public Task<Result<Tag, AppError>> UpdateAsync(Tag tag) => throw new NotSupportedException();
        public Task<Result<Unit, AppError>> DeleteAsync(Guid id) => throw new NotSupportedException();
    }
}

public class HighlightingUiTests
{
    [Fact]
    public void The_list_marks_the_search_terms_in_its_text() => Wpf.Run(() =>
    {
        var block = new System.Windows.Controls.TextBlock();
        NoteApp.Views.Highlighting.SetTerms(block, ["milk"]);
        NoteApp.Views.Highlighting.SetText(block, "Buy milk today");

        var runs = block.Inlines.OfType<System.Windows.Documents.Run>().ToList();
        Assert.Equal(["Buy ", "milk", " today"], runs.Select(r => r.Text));
        Assert.Equal([false, true, false], runs.Select(r => r.Background is not null));
    });
}
