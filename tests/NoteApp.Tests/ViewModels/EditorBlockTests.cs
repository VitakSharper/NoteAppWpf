using System.IO;
using NoteApp.Data.Queries;
using NoteApp.Data.Repositories;
using NoteApp.Domain.Functional;
using NoteApp.Domain.Models;
using NoteApp.Domain.ValueObjects;
using NoteApp.Services;
using NoteApp.ViewModels;

namespace NoteApp.Tests.ViewModels;

// Files dropped from Explorer and blocks dragged by their grip end up here.
public class EditorBlockTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "NoteApp.Tests", $"drop-{Guid.NewGuid()}");

    [Fact]
    public void Dropped_files_become_file_blocks_in_order()
    {
        var a = File("a.txt", [1, 2, 3]);
        var b = File("b.pdf", [4]);
        var vm = Editor();

        vm.AddFileBlocks([a, b]);

        Assert.Equal(["a.txt", "b.pdf"], vm.Blocks.Select(x => x.FileName));
        Assert.Equal([3L, 1L], vm.Blocks.Select(x => x.FileSize));
        Assert.Equal(".pdf", vm.Blocks[1].FileExtension);
        Assert.True(vm.IsDirty);
        Assert.Empty(vm.ErrorMessage);
    }

    [Fact]
    public void A_dropped_folder_is_skipped_and_reported()
    {
        var file = File("a.txt", [1]);
        var folder = Directory.CreateDirectory(Path.Combine(_folder, "sub")).FullName;
        var vm = Editor();

        vm.AddFileBlocks([folder, file]);

        Assert.Equal(["a.txt"], vm.Blocks.Select(x => x.FileName));
        Assert.Contains("sub", vm.ErrorMessage);
    }

    [Theory]
    [InlineData(0, 2, new[] { "b", "c", "a" })]
    [InlineData(2, 0, new[] { "c", "a", "b" })]
    [InlineData(1, 99, new[] { "a", "c", "b" })]
    [InlineData(1, 1, new[] { "a", "b", "c" })]
    public void A_block_moves_to_the_index_it_is_dropped_at(int from, int to, string[] expected)
    {
        var vm = Editor();
        foreach (var name in new[] { "a", "b", "c" })
            vm.Blocks.Add(new BlockViewModel { BlockType = BlockType.Link, LinkUrlText = name });

        vm.MoveBlock(vm.Blocks[from], to);

        Assert.Equal(expected, vm.Blocks.Select(x => x.LinkUrlText));
    }

    private string File(string name, byte[] bytes)
    {
        Directory.CreateDirectory(_folder);
        var path = Path.Combine(_folder, name);
        System.IO.File.WriteAllBytes(path, bytes);
        return path;
    }

    private static NoteEditorViewModel Editor() => new(new NoteService(new NoRepository()), new NoTags(), []);

    public void Dispose()
    {
        if (Directory.Exists(_folder))
            Directory.Delete(_folder, recursive: true);
    }

    private sealed class NoRepository : INoteRepository
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

    private sealed class NoTags : ITagRepository
    {
        public Task<Result<IReadOnlyList<Tag>, AppError>> GetAllAsync() => throw new NotSupportedException();
        public Task<Result<Tag, AppError>> CreateAsync(Tag tag) => throw new NotSupportedException();
        public Task<Result<Tag, AppError>> UpdateAsync(Tag tag) => throw new NotSupportedException();
        public Task<Result<Unit, AppError>> DeleteAsync(Guid id) => throw new NotSupportedException();
    }
}
