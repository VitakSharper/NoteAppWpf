using NoteApp.Data.Repositories;
using NoteApp.Domain.Functional;
using NoteApp.Domain.Models;

namespace NoteApp.Services;

public sealed class SearchService(INoteRepository noteRepository, ITagRepository tagRepository)
{
    public Task<Result<IReadOnlyList<Note>, AppError>> SearchNotesAsync(
        string? searchText = null,
        IReadOnlyList<Guid>? tagIds = null,
        BlockType? blockType = null) =>
        noteRepository.SearchAsync(searchText, tagIds, blockType);

    public Task<Result<IReadOnlyList<Tag>, AppError>> GetAllTagsAsync() =>
        tagRepository.GetAllAsync();
}
