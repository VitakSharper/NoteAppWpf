using NoteApp.Data.Queries;
using NoteApp.Data.Repositories;
using NoteApp.Domain.Functional;
using NoteApp.Domain.Models;
using NoteApp.Domain.ValueObjects;
using NoteApp.Services;
using NoteApp.ViewModels;

namespace NoteApp.Tests.ViewModels;

// The editor tracks "modified" at the source of every change, so these tests drive the
// view model the way the UI does — no database, no dispatcher, no STA thread involved.
public class NoteEditorDirtyTests
{
    [Fact]
    public void A_new_note_starts_clean() =>
        Assert.False(Editor().IsDirty);

    [Fact]
    public void Loading_an_existing_note_is_not_an_edit() =>
        Assert.False(Editor(SampleNote(SampleBlocks())).IsDirty);

    [Fact]
    public void Editing_the_title_marks_the_note_modified()
    {
        var vm = Editor(SampleNote(SampleBlocks()));

        vm.Title = "Another title";

        Assert.True(vm.IsDirty);
    }

    [Fact]
    public void Turning_on_encryption_marks_the_note_modified()
    {
        var vm = Editor(SampleNote(SampleBlocks()));

        vm.IsEncrypted = true;

        Assert.True(vm.IsDirty);
    }

    [Fact]
    public void Adding_a_block_marks_the_note_modified()
    {
        var vm = Editor(SampleNote(SampleBlocks()));

        vm.AddTextBlockCommand.Execute(null);

        Assert.True(vm.IsDirty);
    }

    [Fact]
    public void Removing_a_block_marks_the_note_modified()
    {
        var vm = Editor(SampleNote(SampleBlocks()));

        vm.RemoveBlockCommand.Execute(vm.Blocks[0]);

        Assert.True(vm.IsDirty);
    }

    [Fact]
    public void Reordering_blocks_marks_the_note_modified()
    {
        var vm = Editor(SampleNote(SampleBlocks()));

        vm.MoveBlockDownCommand.Execute(vm.Blocks[0]);

        Assert.True(vm.IsDirty);
    }

    [Fact]
    public void Editing_a_link_block_marks_the_note_modified()
    {
        var vm = Editor(SampleNote(SampleBlocks()));

        vm.Blocks.Single(b => b.BlockType == BlockType.Link).LinkUrlText = "https://example.com/other";

        Assert.True(vm.IsDirty);
    }

    [Fact]
    public void Toggling_a_tag_marks_the_note_modified()
    {
        var vm = Editor(SampleNote(SampleBlocks()));

        vm.ToggleTagCommand.Execute(vm.AvailableTags[0]);

        Assert.True(vm.IsDirty);
    }

    // Only the view sees a rich text edit: the payload reaches the block on LostFocus
    // or right before a save, which is far too late to guard the exits.
    [Fact]
    public void The_view_can_report_a_rich_text_edit()
    {
        var vm = Editor(SampleNote(SampleBlocks()));

        vm.MarkDirty();

        Assert.True(vm.IsDirty);
    }

    // That same sync writes RichTextContent/PlainTextContent on every LostFocus and
    // before every save. It is a serialization artefact, not a user edit.
    [Fact]
    public void Syncing_rich_text_back_from_the_view_is_not_an_edit()
    {
        var vm = Editor(SampleNote(SampleBlocks()));
        var text = vm.Blocks.Single(b => b.BlockType == BlockType.Text);

        text.RichTextContent = "<a fresh serialization of the very same document/>";
        text.PlainTextContent = "plain text";

        Assert.False(vm.IsDirty);
    }

    // A checklist edit never touches a property of the block itself, so the tracking
    // has to reach into the items and into their collection.
    [Fact]
    public void Ticking_a_checklist_item_marks_the_note_modified()
    {
        var vm = Editor(SampleNote(SampleBlocks()));
        var checklist = vm.Blocks.Single(b => b.BlockType == BlockType.Checklist);

        checklist.ChecklistItems[1].IsDone = true;

        Assert.True(vm.IsDirty);
    }

    [Fact]
    public void Retyping_a_checklist_item_marks_the_note_modified()
    {
        var vm = Editor(SampleNote(SampleBlocks()));
        var checklist = vm.Blocks.Single(b => b.BlockType == BlockType.Checklist);

        checklist.ChecklistItems[0].Text = "buy oat milk";

        Assert.True(vm.IsDirty);
    }

    [Fact]
    public void Adding_and_removing_checklist_items_marks_the_note_modified()
    {
        var vm = Editor(SampleNote(SampleBlocks()));
        var checklist = vm.Blocks.Single(b => b.BlockType == BlockType.Checklist);

        vm.AddChecklistItemCommand.Execute(checklist);
        Assert.True(vm.IsDirty);
        Assert.Equal(3, checklist.ChecklistItems.Count);

        vm.RefreshAfterSave(SampleNote(SampleBlocks()));
        vm.RemoveChecklistItemCommand.Execute(checklist.ChecklistItems[2]);

        Assert.True(vm.IsDirty);
        Assert.Equal(2, checklist.ChecklistItems.Count);
    }

    // Enter in an item: the new row lands right below the one being typed in, and
    // typing in it counts as an edit like any other.
    [Fact]
    public void Enter_inserts_an_item_directly_below_the_current_one()
    {
        var vm = Editor(SampleNote(SampleBlocks()));
        var checklist = vm.Blocks.Single(b => b.BlockType == BlockType.Checklist);

        var inserted = vm.InsertChecklistItemAfter(checklist, checklist.ChecklistItems[0]);

        Assert.Equal(1, checklist.ChecklistItems.IndexOf(inserted));
        Assert.True(vm.IsDirty);

        vm.RefreshAfterSave(SampleNote(SampleBlocks()));
        inserted.Text = "typed into the new row";
        Assert.True(vm.IsDirty);
    }

    [Fact]
    public void A_new_checklist_block_starts_with_one_empty_row()
    {
        var vm = Editor();

        vm.AddChecklistBlockCommand.Execute(null);

        var block = Assert.Single(vm.Blocks);
        Assert.Equal(BlockType.Checklist, block.BlockType);
        Assert.Equal(string.Empty, Assert.Single(block.ChecklistItems).Text);
        Assert.Equal("0/1 done", block.ChecklistSummary);
    }

    [Fact]
    public void The_block_reports_how_many_items_are_done()
    {
        var vm = Editor(SampleNote(SampleBlocks()));
        var checklist = vm.Blocks.Single(b => b.BlockType == BlockType.Checklist);

        Assert.Equal("1/2 done", checklist.ChecklistSummary);

        checklist.ChecklistItems[1].IsDone = true;

        Assert.Equal("2/2 done", checklist.ChecklistSummary);
    }

    [Fact]
    public void Selecting_a_block_is_not_an_edit()
    {
        var vm = Editor(SampleNote(SampleBlocks()));

        vm.SelectedBlock = vm.Blocks[^1];

        Assert.False(vm.IsDirty);
    }

    [Fact]
    public void A_successful_save_clears_the_modified_state()
    {
        var note = SampleNote(SampleBlocks());
        var vm = Editor(note);
        vm.Title = "Another title";

        vm.RefreshAfterSave(note with { Title = Title("Another title") });

        Assert.False(vm.IsDirty);
    }

    // The guard needs it to put the list selection back on the note it kept open.
    [Fact]
    public void The_edited_note_id_is_exposed_for_the_leave_guard()
    {
        var note = SampleNote(SampleBlocks());

        Assert.Equal(note.Id, Editor(note).EditedNoteId);
        Assert.Null(Editor().EditedNoteId);
    }

    private static NoteEditorViewModel Editor(Note? existingNote = null) =>
        new(new NoteService(new UnusedNoteRepository()),
            new UnusedTagRepository(),
            [Tag.Create(Name("work")), Tag.Create(Name("home"))],
            existingNote);

    // Nothing under test reaches storage: any call is a bug in the test, not a scenario.
    private sealed class UnusedNoteRepository : INoteRepository
    {
        public Task<Result<Note, AppError>> GetByIdAsync(NoteId id) => throw new NotSupportedException();
        public Task<Result<Note, AppError>> CreateAsync(Note note, byte[]? encryptedContent = null) => throw new NotSupportedException();
        public Task<Result<Note, AppError>> UpdateAsync(Note note, byte[]? encryptedContent = null) => throw new NotSupportedException();
        public Task<Result<Unit, AppError>> DeleteAsync(NoteId id) => throw new NotSupportedException();
        public Task<Result<Unit, AppError>> RestoreAsync(NoteId id) => throw new NotSupportedException();
        public Task<Result<Unit, AppError>> PurgeAsync(NoteId id) => throw new NotSupportedException();
        public Task<Result<int, AppError>> PurgeAllDeletedAsync() => throw new NotSupportedException();
        public Task<Result<IReadOnlyList<NoteSummaryRow>, AppError>> SearchSummariesAsync(string? searchText, IReadOnlyList<Guid>? tagIds, BlockType? blockType, bool deletedOnly = false) => throw new NotSupportedException();
        public Task<Result<byte[]?, AppError>> GetEncryptedContentAsync(NoteId id) => throw new NotSupportedException();
    }

    private sealed class UnusedTagRepository : ITagRepository
    {
        public Task<Result<IReadOnlyList<Tag>, AppError>> GetAllAsync() => throw new NotSupportedException();
        public Task<Result<Tag, AppError>> CreateAsync(Tag tag) => throw new NotSupportedException();
        public Task<Result<Tag, AppError>> UpdateAsync(Tag tag) => throw new NotSupportedException();
        public Task<Result<Unit, AppError>> DeleteAsync(Guid id) => throw new NotSupportedException();
    }
}
