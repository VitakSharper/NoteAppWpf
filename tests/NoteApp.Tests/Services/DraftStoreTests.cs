using System.IO;
using NoteApp.Domain.Models;
using NoteApp.Services;
using NoteApp.ViewModels;

namespace NoteApp.Tests.Services;

public class DraftStoreTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "NoteApp.Tests", $"drafts-{Guid.NewGuid()}");

    [Fact]
    public void A_draft_round_trips_every_block_kind()
    {
        var store = new DraftStore(_folder);
        var draft = new NoteDraft(Guid.NewGuid(), Guid.NewGuid(), "Title",
        [
            new DraftBlock(BlockType.Text, RichText: "rich", PlainText: "plain"),
            new DraftBlock(BlockType.Link, LinkUrl: "https://half", LinkDescription: "d"),
            new DraftBlock(BlockType.File, FileName: "a.bin", FileExtension: ".bin", FileData: [1, 2]),
            new DraftBlock(BlockType.Checklist, Items: [new DraftChecklistItem("milk", true), new DraftChecklistItem("bread", false, InProgress: true, Note: "wholemeal")])
        ], [Guid.NewGuid()], new DateTime(2026, 9, 24, 10, 0, 0));

        store.Save(draft);
        var loaded = Assert.Single(store.LoadAll());

        Assert.Equal(draft.Key, loaded.Key);
        Assert.Equal(draft.Title, loaded.Title);
        Assert.Equal(draft.TagIds, loaded.TagIds);
        Assert.Equal([BlockType.Text, BlockType.Link, BlockType.File, BlockType.Checklist], loaded.Blocks.Select(b => b.Type));
        Assert.Equal("https://half", loaded.Blocks[1].LinkUrl);
        Assert.Equal(new byte[] { 1, 2 }, loaded.Blocks[2].FileData);
        Assert.Equal([("milk", ChecklistItemState.Done, (string?)null), ("bread", ChecklistItemState.InProgress, "wholemeal")],
            loaded.Blocks[3].Items!.Select(i => (i.Text, i.State, i.Note)));
    }

    // A draft left behind by a release that knew only "done" is still offered as it was.
    [Fact]
    public void A_draft_written_before_items_could_be_in_progress_still_reads()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(Path.Combine(_folder, $"{Guid.NewGuid()}.json"), $$"""
            {"Key":"{{Guid.NewGuid()}}","NoteId":null,"Title":"Old","Blocks":[{"Type":3,"Items":[{"Text":"milk","IsDone":true,"Due":null},{"Text":"bread","IsDone":false,"Due":null}]}],"TagIds":[],"SavedAt":"2026-09-24T10:00:00"}
            """);

        var items = Assert.Single(Assert.Single(new DraftStore(_folder).LoadAll()).Blocks).Items!;

        Assert.Equal([ChecklistItemState.Done, ChecklistItemState.Todo], items.Select(i => i.State));
    }

    [Fact]
    public void Saving_again_replaces_and_delete_removes()
    {
        var store = new DraftStore(_folder);
        var key = Guid.NewGuid();

        store.Save(new NoteDraft(key, null, "first", [], [], DateTime.Now));
        store.Save(new NoteDraft(key, null, "second", [], [], DateTime.Now));
        Assert.Equal("second", Assert.Single(store.LoadAll()).Title);

        store.Delete(key);
        Assert.Empty(store.LoadAll());
        Assert.Empty(Directory.GetFiles(_folder));
    }

    [Fact]
    public void An_unreadable_draft_is_skipped_but_kept()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(Path.Combine(_folder, $"{Guid.NewGuid()}.json"), "{ half");

        Assert.Empty(new DraftStore(_folder).LoadAll());
        Assert.Single(Directory.GetFiles(_folder));
    }

    [Fact]
    public void A_missing_folder_means_no_drafts() => Assert.Empty(new DraftStore(_folder).LoadAll());

    [Fact]
    public void An_editor_captures_and_restores_its_state()
    {
        var work = Tag.Create(Name("work"));
        var source = new NoteEditorViewModel(new NoteService(null!), null!, [work]) { };
        source.Title = "Draft title";
        source.AddChecklistBlockCommand.Execute(null);
        source.Blocks[0].ChecklistItems[0].Text = "call the bank";
        source.Blocks[0].ChecklistItems[0].State = ChecklistItemState.InProgress;
        source.Blocks[0].ChecklistItems[0].Note = "ask for the advisor";
        source.AddLinkBlockCommand.Execute(null);
        source.Blocks[1].LinkUrlText = "https://not-finished";
        source.ToggleTagCommand.Execute(work);

        var draft = source.CaptureDraft();
        Assert.Null(draft.NoteId);
        Assert.Equal(source.NewNoteDraftKey, draft.Key);

        var target = new NoteEditorViewModel(new NoteService(null!), null!, [work]);
        target.RestoreDraft(draft);

        Assert.Equal("Draft title", target.Title);
        Assert.Equal("call the bank", target.Blocks[0].ChecklistItems[0].Text);
        Assert.Equal(ChecklistItemState.InProgress, target.Blocks[0].ChecklistItems[0].State);
        Assert.Equal("ask for the advisor", target.Blocks[0].ChecklistItems[0].Note);
        Assert.Equal("https://not-finished", target.Blocks[1].LinkUrlText);
        Assert.Equal([work.Id], target.SelectedTags.Select(t => t.Id));
        Assert.True(target.IsDirty);
        // The restored new note keeps writing to the same draft file.
        Assert.Equal(draft.Key, target.DraftKey);
    }

    [Fact]
    public void An_encrypted_note_never_keeps_a_draft()
    {
        var editor = new NoteEditorViewModel(new NoteService(null!), null!, []);
        Assert.True(editor.CanKeepDraft);

        editor.IsEncrypted = true;

        Assert.False(editor.CanKeepDraft);
        Assert.False(new NoteEditorViewModel(new NoteService(null!), null!, [],
            SampleNote(SampleBlocks()) with { IsEncrypted = true }).CanKeepDraft);
    }

    public void Dispose()
    {
        if (Directory.Exists(_folder))
            Directory.Delete(_folder, recursive: true);
    }
}
