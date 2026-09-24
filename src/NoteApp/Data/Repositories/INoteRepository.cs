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
    Task<Result<Unit, AppError>> SetPinnedAsync(NoteId id, bool isPinned);
    // Permanent. Trash only: a live note has to be deleted (trashed) first.
    Task<Result<Unit, AppError>> PurgeAsync(NoteId id);
    Task<Result<int, AppError>> PurgeAllDeletedAsync();
    Task<Result<IReadOnlyList<NoteSummaryRow>, AppError>> SearchSummariesAsync(
        string? searchText,
        IReadOnlyList<Guid>? tagIds,
        BlockType? blockType,
        bool deletedOnly = false);
    Task<Result<byte[]?, AppError>> GetEncryptedContentAsync(NoteId id);
    // The live notes whose text links to this one, by title. Encrypted notes store no blocks,
    // so their links do not count.
    Task<Result<IReadOnlyList<NoteRow>, AppError>> LinkedFromAsync(NoteId id);
}
