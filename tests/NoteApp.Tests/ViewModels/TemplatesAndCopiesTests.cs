using System.IO;
using NoteApp.Data.Queries;
using NoteApp.Domain.Functional;
using NoteApp.Domain.Models;
using NoteApp.Domain.ValueObjects;
using NoteApp.Services;
using NoteApp.ViewModels;

namespace NoteApp.Tests.ViewModels;

public class TemplatesAndCopiesTests
{
    [Fact]
    public void A_copy_is_titled_as_one_and_still_fits_a_title()
    {
        Assert.Equal("Plan (copy)", NoteCopies.TitleFor(" Plan "));

        var copy = NoteCopies.TitleFor(new string('x', 500));
        Assert.Equal(500, copy.Length);
        Assert.EndsWith(" (copy)", copy);
    }

    [Fact]
    public async Task Duplicating_from_the_list_copies_under_the_copy_title_and_hands_the_copy_on()
    {
        var repo = new Recording();
        var list = new NoteListViewModel(new NoteService(repo), new NoTags(), Settings());
        var summary = new NoteSummary(NoteId.New(), Title("Plan"), "", [], false, true, false, false, false, DateTime.UtcNow, DateTime.UtcNow);
        NoteId? opened = null;
        list.NoteDuplicated += id => opened = id;

        await list.DuplicateNoteCommand.ExecuteAsync(summary);

        Assert.Equal((summary.Id, "Plan (copy)"), repo.Duplicated);
        Assert.Equal(repo.CopyId, opened);
    }

    [Fact]
    public async Task Saving_as_a_template_takes_what_the_editor_holds_under_new_block_ids()
    {
        var repo = new Recording();
        var note = Note.Create(Title("Meeting"), [new NoteBlock.Checklist([new ChecklistItem("agenda", false)])], []).Unwrap();
        var editor = new NoteEditorViewModel(new NoteService(repo), new NoTags(), [], note);
        editor.Blocks[0].ChecklistItems[0].Text = "agenda, then actions";
        string? message = null;
        editor.ShowMessage += m => message = m;

        await editor.SaveAsTemplateCommand.ExecuteAsync(null);

        var template = repo.Template!;
        Assert.Equal("Meeting", template.Title.Value);
        var checklist = Assert.IsType<NoteBlock.Checklist>(Assert.Single(template.Blocks));
        Assert.Equal("agenda, then actions", checklist.Items[0].Text);
        Assert.NotEqual(note.Blocks[0].Id, checklist.Id);
        Assert.NotEqual(note.Id, template.Id);
        Assert.Contains("Meeting", message);
    }

    [Fact]
    public async Task An_encrypted_note_is_not_made_a_template()
    {
        var repo = new Recording();
        var editor = new NoteEditorViewModel(new NoteService(repo), new NoTags(), []) { Title = "Codes", IsEncrypted = true };
        editor.AddTextBlockCommand.Execute(null);

        await editor.SaveAsTemplateCommand.ExecuteAsync(null);

        Assert.Null(repo.Template);
        Assert.Contains("encrypted", editor.ErrorMessage);
    }

    [Fact]
    public void A_note_from_a_template_starts_as_a_modified_copy_under_new_ids()
    {
        var work = Tag.Create(Name("work"));
        var template = Note.Create(Title("Meeting"), [new NoteBlock.Code("agenda")], [work]).Unwrap();
        var editor = new NoteEditorViewModel(new NoteService(new Recording()), new NoTags(), [work]);

        editor.FillFrom(template);

        Assert.Equal("Meeting", editor.Title);
        Assert.Equal("agenda", Assert.Single(editor.Blocks).CodeText);
        Assert.NotEqual(template.Blocks[0].Id, editor.Blocks[0].Id);
        Assert.Equal([work.Id], editor.SelectedTags.Select(t => t.Id));
        Assert.Null(editor.EditedNoteId);
        Assert.True(editor.IsDirty);
    }

    private static AppSettingsService Settings() =>
        new(Path.Combine(Path.GetTempPath(), "NoteApp.Tests", Guid.NewGuid() + ".json"));

    private sealed class Recording : ThrowingNoteRepository
    {
        public NoteId CopyId { get; } = NoteId.New();
        public (NoteId Id, string Title)? Duplicated { get; private set; }
        public Note? Template { get; private set; }

        public override Task<Result<NoteId, AppError>> DuplicateAsync(NoteId id, string title)
        {
            Duplicated = (id, title);
            return Task.FromResult(Result<NoteId, AppError>.Ok(CopyId));
        }

        public override Task<Result<Unit, AppError>> SaveTemplateAsync(Note template)
        {
            Template = template;
            return Task.FromResult(Result<Unit, AppError>.Ok(Unit.Value));
        }

        public override Task<Result<IReadOnlyList<NoteSummaryRow>, AppError>> SearchSummariesAsync(
            string? searchText, IReadOnlyList<Guid>? tagIds, BlockType? blockType, bool deletedOnly = false) =>
            Task.FromResult(Result<IReadOnlyList<NoteSummaryRow>, AppError>.Ok([]));
    }

    private sealed class NoTags : NoteApp.Data.Repositories.ITagRepository
    {
        public Task<Result<IReadOnlyList<Tag>, AppError>> GetAllAsync() => Task.FromResult(Result<IReadOnlyList<Tag>, AppError>.Ok([]));
        public Task<Result<Tag, AppError>> CreateAsync(Tag tag) => throw new NotSupportedException();
        public Task<Result<Tag, AppError>> UpdateAsync(Tag tag) => throw new NotSupportedException();
        public Task<Result<Unit, AppError>> DeleteAsync(Guid id) => throw new NotSupportedException();
    }
}
