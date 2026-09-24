using System.Text;
using NoteApp.Data.Queries;
using NoteApp.Domain.Functional;
using NoteApp.Domain.Models;
using NoteApp.Domain.ValueObjects;
using NoteApp.Services;
using NoteApp.ViewModels;

namespace NoteApp.Tests.Services;

public class VersionTests
{
    private static readonly Guid PlainId = Guid.NewGuid(), EncryptedId = Guid.NewGuid();

    [Fact]
    public async Task A_plain_version_opens_as_the_blocks_it_had()
    {
        var service = new NoteService(new Versions());

        var content = (await service.OpenVersionAsync(PlainId, password: null)).Unwrap();

        Assert.Equal("Old title", content.Title);
        Assert.Equal(["buy milk"], Assert.IsType<NoteBlock.Checklist>(Assert.Single(content.Blocks)).Items.Select(i => i.Text));
    }

    [Fact]
    public async Task An_encrypted_version_needs_the_password_it_was_saved_with()
    {
        var service = new NoteService(new Versions());

        Assert.Equal("hidden", Assert.IsType<NoteBlock.Text>((await service.OpenVersionAsync(EncryptedId, "pw")).Unwrap().Blocks.Single()).PlainText);
        Assert.Contains("another password", (await service.OpenVersionAsync(EncryptedId, "other")).Match(_ => "", e => e.Message));
        Assert.True((await service.OpenVersionAsync(EncryptedId, null)).IsFailure);
    }

    [Fact]
    public async Task Restoring_a_version_puts_it_in_the_editor_until_it_is_saved()
    {
        var note = Note.Create(Title("New title"), [new NoteBlock.Text("x", "now")], []).Unwrap();
        var editor = new NoteEditorViewModel(new NoteService(new Versions()), new NoTags(), [], note);
        var version = (await editor.OpenVersionAsync(PlainId)).Unwrap();

        editor.RestoreVersion(version);

        Assert.Equal("Old title", editor.Title);
        Assert.Equal(BlockType.Checklist, Assert.Single(editor.Blocks).BlockType);
        Assert.True(editor.IsDirty);
    }

    [Fact]
    public void The_preview_says_what_each_block_held_but_no_password()
    {
        var lines = VersionPreview.Lines([
            new NoteBlock.Text("x", "first line\r\n\r\nsecond"),
            new NoteBlock.Checklist([new ChecklistItem("done", true), new ChecklistItem("todo", false)]),
            new NoteBlock.Secret("Wifi", "me", "hunter2"),
            new NoteBlock.Code("ls\n  -la")]);

        Assert.Equal(["first line", "second", "☑ done", "☐ todo", "🔑 Wifi · me · password hidden", "  ls", "    -la"], lines);
        Assert.DoesNotContain(lines, l => l.Contains("hunter2"));
    }

    private sealed class Versions : ThrowingNoteRepository
    {
        public override Task<Result<NoteVersionRow, AppError>> GetVersionAsync(Guid versionId)
        {
            var row = versionId == PlainId
                ? new NoteVersionRow
                {
                    Id = PlainId, SavedAt = DateTime.UtcNow, Title = "Old title",
                    Content = Encoding.UTF8.GetBytes(EncryptionService.BlocksToJson([new NoteBlock.Checklist([new ChecklistItem("buy milk", false)])]))
                }
                : new NoteVersionRow
                {
                    Id = EncryptedId, SavedAt = DateTime.UtcNow, Title = "Secret", IsEncrypted = true,
                    Content = EncryptionService.EncryptBlocks([new NoteBlock.Text("x", "hidden")], "pw")
                };
            return Task.FromResult(Result<NoteVersionRow, AppError>.Ok(row));
        }
    }

    private sealed class NoTags : NoteApp.Data.Repositories.ITagRepository
    {
        public Task<Result<IReadOnlyList<Tag>, AppError>> GetAllAsync() => throw new NotSupportedException();
        public Task<Result<Tag, AppError>> CreateAsync(Tag tag) => throw new NotSupportedException();
        public Task<Result<Tag, AppError>> UpdateAsync(Tag tag) => throw new NotSupportedException();
        public Task<Result<Unit, AppError>> DeleteAsync(Guid id) => throw new NotSupportedException();
    }
}
