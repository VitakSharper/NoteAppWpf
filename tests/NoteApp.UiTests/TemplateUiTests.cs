using NoteApp.Data.Queries;
using NoteApp.Domain.Functional;
using NoteApp.Domain.Models;
using NoteApp.Domain.ValueObjects;
using NoteApp.ViewModels;
using static NoteApp.UiTests.Notes;

namespace NoteApp.UiTests;

public class TemplateUiTests
{
    private static readonly NoteId TemplateId = NoteId.New();

    [Fact]
    public void New_from_template_opens_a_new_note_filled_from_it() => Wpf.Run(() =>
    {
        var shell = FocusModeUiTests.Shell(new OneTemplate());
        shell.LoadTemplatesAsync().GetAwaiter().GetResult();
        var template = Assert.Single(shell.Templates);

        shell.NewFromTemplateCommand.ExecuteAsync(template).GetAwaiter().GetResult();

        var editor = Assert.IsType<NoteEditorViewModel>(shell.CurrentEditor);
        Assert.Null(editor.EditedNoteId);
        Assert.Equal("Weekly review", editor.Title);
        Assert.Equal(["Wins", "Next week"], editor.Blocks[0].ChecklistItems.Select(i => i.Text));
        Assert.True(editor.IsDirty);
    });

    private sealed class OneTemplate : UnusedNoteRepository
    {
        public override Task<Result<IReadOnlyList<NoteRow>, AppError>> TemplatesAsync() =>
            Task.FromResult(Result<IReadOnlyList<NoteRow>, AppError>.Ok([new NoteRow { Id = TemplateId.Value, Title = "Weekly review" }]));

        public override Task<Result<Note, AppError>> GetTemplateAsync(NoteId id) =>
            Task.FromResult(Result<Note, AppError>.Ok(
                Note.Create(NoteTitle.From("Weekly review").Match(t => t, e => throw new InvalidOperationException(e.Message)),
                    [Checklist("Wins", "Next week")], []).Match(n => n, e => throw new InvalidOperationException(e.Message))));
    }
}
