using NoteApp.Data.Queries;
using NoteApp.Domain.Functional;
using NoteApp.Domain.Models;
using NoteApp.Domain.ValueObjects;

namespace NoteApp.Data.Repositories;

public interface INoteRepository
{
    Task<Result<Note, AppError>> GetByIdAsync(NoteId id);
    Task<Result<Note, AppError>> CreateAsync(Note note, byte[]? encryptedContent = null);
    Task<Result<Note, AppError>> UpdateAsync(Note note, byte[]? encryptedContent = null);
    Task<Result<Unit, AppError>> DeleteAsync(NoteId id);
    Task<Result<IReadOnlyList<NoteSummaryRow>, AppError>> SearchSummariesAsync(
        string? searchText,
        IReadOnlyList<Guid>? tagIds,
        BlockType? blockType);
    Task<Result<byte[]?, AppError>> GetEncryptedContentAsync(NoteId id);
}
