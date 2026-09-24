using NoteApp.Domain.Models;
using NoteApp.Services;
using NoteApp.ViewModels;

namespace NoteApp.Tests.Services;

public class NoteStatusTests
{
    [Theory]
    [InlineData(null, 0)]
    [InlineData("   ", 0)]
    [InlineData("one", 1)]
    [InlineData("l'été est là, e-mail it: 3 times!", 7)]
    [InlineData("SELECT * FROM Notes;", 3)]
    public void Words_are_runs_of_letters_and_digits(string? text, int words) =>
        Assert.Equal(words, NoteStatus.Words(text));

    [Fact]
    public void The_word_label_says_one_word_or_many() =>
        Assert.Equal(("1 word", "0 words"), (NoteStatus.WordLabel(1), NoteStatus.WordLabel(0)));

    [Fact]
    public void The_saved_label_names_the_day_only_when_it_is_not_today()
    {
        var now = new DateTime(2026, 9, 24, 15, 0, 0, DateTimeKind.Local);
        DateTime Utc(DateTime local) => local.ToUniversalTime();

        Assert.Equal("Not saved yet", NoteStatus.SavedLabel(null, now));
        Assert.StartsWith("Saved at ", NoteStatus.SavedLabel(Utc(now.AddHours(-2)), now));
        Assert.StartsWith("Saved yesterday at ", NoteStatus.SavedLabel(Utc(now.AddDays(-1)), now));
        Assert.DoesNotContain("2026", NoteStatus.SavedLabel(Utc(now.AddDays(-10)), now));
        Assert.Contains("2025", NoteStatus.SavedLabel(Utc(now.AddYears(-1)), now));
    }

    [Fact]
    public void The_editor_counts_the_words_of_every_block_as_they_change()
    {
        var note = Note.Create(Title("Plan"), [new NoteBlock.Text("x", "two words"), new NoteBlock.Code("a b c")], []).Unwrap();
        var editor = new NoteEditorViewModel(new NoteService(new ThrowingNoteRepository()), new NoTags(), [], note);
        Assert.Equal(5, editor.WordCount);

        editor.UpdateLiveText(editor.Blocks[0].Id, "now three words");
        editor.AddChecklistBlockCommand.Execute(null);
        editor.Blocks[2].ChecklistItems[0].Text = "buy milk";

        Assert.Equal(8, editor.WordCount);
        Assert.Equal("8 words", editor.WordLabel);
        Assert.StartsWith("Saved", editor.SavedLabel);
    }

    private sealed class NoTags : NoteApp.Data.Repositories.ITagRepository
    {
        public Task<NoteApp.Domain.Functional.Result<IReadOnlyList<Tag>, NoteApp.Domain.Functional.AppError>> GetAllAsync() => throw new NotSupportedException();
        public Task<NoteApp.Domain.Functional.Result<Tag, NoteApp.Domain.Functional.AppError>> CreateAsync(Tag tag) => throw new NotSupportedException();
        public Task<NoteApp.Domain.Functional.Result<Tag, NoteApp.Domain.Functional.AppError>> UpdateAsync(Tag tag) => throw new NotSupportedException();
        public Task<NoteApp.Domain.Functional.Result<NoteApp.Domain.Functional.Unit, NoteApp.Domain.Functional.AppError>> DeleteAsync(Guid id) => throw new NotSupportedException();
    }
}
