using NoteApp.Data.Repositories;
using NoteApp.Domain.Functional;
using NoteApp.Domain.Models;
using NoteApp.Domain.ValueObjects;
using NoteApp.Services;
using NoteApp.ViewModels;
using static NoteApp.Domain.Models.ChecklistItemState;

namespace NoteApp.Tests.ViewModels;

// The short note under a checklist item: what marks the note edited, and what a save keeps.
public class ChecklistNoteTests
{
    [Fact]
    public void Opening_a_note_is_not_an_edit_typing_in_it_is()
    {
        var vm = Editor(new ChecklistItem("call the bank", Todo));
        var item = vm.Blocks[0].ChecklistItems[0];

        item.IsNoteOpen = true;
        Assert.True(item.ShowsNote);
        Assert.False(vm.IsDirty);

        item.Note = "ask for the advisor";
        Assert.True(vm.IsDirty);
    }

    [Fact]
    public void A_stored_note_shows_without_being_opened()
    {
        var vm = Editor(new ChecklistItem("call the bank", Todo, Note: "ask for the advisor"), new ChecklistItem("pay rent", Todo));

        Assert.Equal([true, false], vm.Blocks[0].ChecklistItems.Select(i => i.ShowsNote));
    }

    [Fact]
    public async Task A_note_is_saved_trimmed_and_a_blank_one_as_none()
    {
        var repo = new CapturingRepository();
        var vm = NewChecklist(repo, "call the bank", "pay rent");
        var items = vm.Blocks[0].ChecklistItems;
        items[0].Note = "  ask for\nthe advisor \n";
        items[1].Note = " \n ";

        await vm.SaveCommand.ExecuteAsync(null);

        var checklist = Assert.IsType<NoteBlock.Checklist>(Assert.Single(repo.Created!.Blocks));
        Assert.Equal(["ask for\nthe advisor", ""], checklist.Items.Select(i => i.Note));
    }

    [Fact]
    public async Task A_note_of_512_characters_is_kept_and_a_longer_one_refused()
    {
        var repo = new CapturingRepository();
        var vm = NewChecklist(repo, "call the bank");
        var item = vm.Blocks[0].ChecklistItems[0];

        item.Note = new string('x', ChecklistItem.MaxNoteLength + 1);
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.Equal("Checklist block #1: a note is longer than 512 characters.", vm.ErrorMessage);
        Assert.Null(repo.Created);

        item.Note = new string('x', ChecklistItem.MaxNoteLength);
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.Empty(vm.ErrorMessage);
        Assert.NotNull(repo.Created);
    }

    // A blank row is dropped on save; one that holds a note would take the note with it.
    [Fact]
    public async Task An_item_with_a_note_but_no_text_is_refused_rather_than_dropped()
    {
        var vm = NewChecklist(new CapturingRepository(), "call the bank", "");
        vm.Blocks[0].ChecklistItems[1].Note = "what was this for?";

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal("Checklist block #1: an item with a note needs a text.", vm.ErrorMessage);
    }

    [Fact]
    public void The_word_count_includes_the_notes()
    {
        var vm = Editor(new ChecklistItem("call the bank", Todo, Note: "ask for the advisor"));
        Assert.Equal(7, vm.WordCount);

        vm.Blocks[0].ChecklistItems[0].Note = "ask";

        Assert.Equal(4, vm.WordCount);
    }

    private static NoteEditorViewModel Editor(params ChecklistItem[] items)
    {
        var note = Note.Create(Title("Plan"), [new NoteBlock.Checklist(items)], []).Unwrap();
        return new NoteEditorViewModel(new NoteService(new CapturingRepository()), new NoTags(), [], note);
    }

    // A new note with one checklist holding these item texts, as typed in the editor.
    private static NoteEditorViewModel NewChecklist(CapturingRepository repo, params string[] texts)
    {
        var vm = new NoteEditorViewModel(new NoteService(repo), new NoTags(), []) { Title = "Plan" };
        vm.AddChecklistBlockCommand.Execute(null);
        var block = vm.Blocks[0];
        for (var i = 1; i < texts.Length; i++)
            vm.AddChecklistItemCommand.Execute(block);
        for (var i = 0; i < texts.Length; i++)
            block.ChecklistItems[i].Text = texts[i];
        return vm;
    }

    private sealed class CapturingRepository : ThrowingNoteRepository
    {
        public Note? Created { get; private set; }

        public override Task<Result<Note, AppError>> CreateAsync(Note note, byte[]? encryptedContent = null)
        {
            Created = note;
            return Task.FromResult(Result<Note, AppError>.Ok(note));
        }
    }

    private sealed class NoTags : ITagRepository
    {
        public Task<Result<IReadOnlyList<Tag>, AppError>> GetAllAsync() => throw new NotSupportedException();
        public Task<Result<Tag, AppError>> CreateAsync(Tag tag) => throw new NotSupportedException();
        public Task<Result<Tag, AppError>> UpdateAsync(Tag tag) => throw new NotSupportedException();
        public Task<Result<Unit, AppError>> DeleteAsync(Guid id) => throw new NotSupportedException();
    }
}
