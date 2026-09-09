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
    // Soft delete: the note goes to the trash and disappears from every other query.
    Task<Result<Unit, AppError>> DeleteAsync(NoteId id);
    Task<Result<Unit, AppError>> RestoreAsync(NoteId id);
    // Permanent. Trash only: a live note has to be deleted (trashed) first.
    Task<Result<Unit, AppError>> PurgeAsync(NoteId id);
    Task<Result<int, AppError>> PurgeAllDeletedAsync();
    Task<Result<IReadOnlyList<NoteSummaryRow>, AppError>> SearchSummariesAsync(
        string? searchText,
        IReadOnlyList<Guid>? tagIds,
        BlockType? blockType,
        bool deletedOnly = false);
    Task<Result<byte[]?, AppError>> GetEncryptedContentAsync(NoteId id);
}
