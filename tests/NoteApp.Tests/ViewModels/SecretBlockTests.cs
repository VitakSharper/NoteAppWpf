using NoteApp.Data.Queries;
using NoteApp.Data.Repositories;
using NoteApp.Domain.Functional;
using NoteApp.Domain.Models;
using NoteApp.Domain.ValueObjects;
using NoteApp.Services;
using NoteApp.ViewModels;

namespace NoteApp.Tests.ViewModels;

// The editor side of a secret block: what a save accepts, and that no draft of it is kept.
public class SecretBlockTests
{
    [Fact]
    public async Task An_empty_secret_is_refused_on_save()
    {
        var vm = Editor();
        vm.Title = "Logins";
        vm.AddSecretBlockCommand.Execute(null);

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal("Secret block #1 is empty.", vm.ErrorMessage);
        Assert.True(vm.IsDirty);
    }

    [Fact]
    public async Task A_secret_address_has_to_be_a_web_address()
    {
        var vm = Editor();
        vm.Title = "Logins";
        vm.AddSecretBlockCommand.Execute(null);
        vm.Blocks[0].SecretPassword = "hunter2";
        vm.Blocks[0].SecretUrl = "ftp://files.example.com";

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.StartsWith("Secret block #1: ", vm.ErrorMessage);
    }

    [Fact]
    public async Task A_secret_is_saved_with_its_password_exactly_as_typed()
    {
        var repo = new CapturingRepository();
        var vm = new NoteEditorViewModel(new NoteService(repo), new NoTags(), []) { Title = "Logins" };
        vm.AddSecretBlockCommand.Execute(null);
        var block = vm.Blocks[0];
        block.SecretLabel = "  Support site ";
        block.SecretUserName = " vbanard ";
        block.SecretPassword = " p@ss ";
        block.SecretUrl = " https://support.example.com/ ";

        await vm.SaveCommand.ExecuteAsync(null);

        var secret = Assert.IsType<NoteBlock.Secret>(Assert.Single(repo.Created!.Blocks));
        Assert.Equal(("Support site", "vbanard", " p@ss ", "https://support.example.com/"),
            (secret.Label, secret.UserName, secret.Password, secret.Url));
    }

    [Fact]
    public void No_draft_is_kept_of_a_note_holding_a_secret()
    {
        var vm = Editor();
        vm.AddTextBlockCommand.Execute(null);
        Assert.True(vm.CanKeepDraft);

        vm.AddSecretBlockCommand.Execute(null);

        Assert.False(vm.CanKeepDraft);
    }

    [Fact]
    public void Editing_any_field_of_a_secret_marks_the_note_modified()
    {
        var note = Note.Create(Title("Logins"), [new NoteBlock.Secret("Wifi", "", "hunter2")], []).Unwrap();
        var vm = new NoteEditorViewModel(new NoteService(new CapturingRepository()), new NoTags(), [], note);
        Assert.False(vm.IsDirty);

        vm.Blocks[0].SecretPassword = "hunter3";

        Assert.True(vm.IsDirty);
    }

    private static NoteEditorViewModel Editor() => new(new NoteService(new CapturingRepository()), new NoTags(), []);

    private sealed class CapturingRepository : INoteRepository
    {
        public Note? Created { get; private set; }

        public Task<Result<Note, AppError>> CreateAsync(Note note, byte[]? encryptedContent = null)
        {
            Created = note;
            return Task.FromResult(Result<Note, AppError>.Ok(note));
        }

        public Task<Result<Note, AppError>> GetByIdAsync(NoteId id) => throw new NotSupportedException();
        public Task<Result<Note, AppError>> UpdateAsync(Note note, byte[]? encryptedContent = null) => throw new NotSupportedException();
        public Task<Result<Unit, AppError>> DeleteAsync(NoteId id) => throw new NotSupportedException();
        public Task<Result<Unit, AppError>> RestoreAsync(NoteId id) => throw new NotSupportedException();
        public Task<Result<Unit, AppError>> SetPinnedAsync(NoteId id, bool isPinned) => throw new NotSupportedException();
        public Task<Result<Unit, AppError>> PurgeAsync(NoteId id) => throw new NotSupportedException();
        public Task<Result<int, AppError>> PurgeAllDeletedAsync() => throw new NotSupportedException();
        public Task<Result<IReadOnlyList<NoteSummaryRow>, AppError>> SearchSummariesAsync(string? searchText, IReadOnlyList<Guid>? tagIds, BlockType? blockType, bool deletedOnly = false) => throw new NotSupportedException();
        public Task<Result<byte[]?, AppError>> GetEncryptedContentAsync(NoteId id) => throw new NotSupportedException();
    }

    private sealed class NoTags : ITagRepository
    {
        public Task<Result<IReadOnlyList<Tag>, AppError>> GetAllAsync() => throw new NotSupportedException();
        public Task<Result<Tag, AppError>> CreateAsync(Tag tag) => throw new NotSupportedException();
        public Task<Result<Tag, AppError>> UpdateAsync(Tag tag) => throw new NotSupportedException();
        public Task<Result<Unit, AppError>> DeleteAsync(Guid id) => throw new NotSupportedException();
    }
}
