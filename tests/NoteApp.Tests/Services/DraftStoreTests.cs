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
            new DraftBlock(BlockType.Checklist, Items: [new DraftChecklistItem("milk", true)])
        ], [Guid.NewGuid()], new DateTime(2026, 9, 24, 10, 0, 0));

        store.Save(draft);
        var loaded = Assert.Single(store.LoadAll());

        Assert.Equal(draft.Key, loaded.Key);
        Assert.Equal(draft.Title, loaded.Title);
        Assert.Equal(draft.TagIds, loaded.TagIds);
        Assert.Equal([BlockType.Text, BlockType.Link, BlockType.File, BlockType.Checklist], loaded.Blocks.Select(b => b.Type));
        Assert.Equal("https://half", loaded.Blocks[1].LinkUrl);
        Assert.Equal(new byte[] { 1, 2 }, loaded.Blocks[2].FileData);
        Assert.Equal("milk", Assert.Single(loaded.Blocks[3].Items!).Text);
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
