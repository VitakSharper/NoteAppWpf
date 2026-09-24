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
    Task<Result<IReadOnlyList<NoteSummaryRow>, AppError>> SearchSummariesAsync(NoteQuery query);
    // Only the flag, like the pin: UpdatedAt stays.
    Task<Result<Unit, AppError>> SetArchivedAsync(NoteId id, bool isArchived);
    Task<Result<byte[]?, AppError>> GetEncryptedContentAsync(NoteId id);
    // The live notes whose text links to this one, by title. Encrypted notes store no blocks,
    // so their links do not count.
    Task<Result<IReadOnlyList<NoteRow>, AppError>> LinkedFromAsync(NoteId id);

    // A copy under a new id, encrypted payload, blocks and tags included; not pinned.
    Task<Result<NoteId, AppError>> DuplicateAsync(NoteId id, string title);

    // Templates: notes kept to start new ones from, invisible to everything above.
    Task<Result<IReadOnlyList<NoteRow>, AppError>> TemplatesAsync();
    Task<Result<Note, AppError>> GetTemplateAsync(NoteId id);
    // Replaces any template of the same title.
    Task<Result<Unit, AppError>> SaveTemplateAsync(Note template);
    Task<Result<Unit, AppError>> DeleteTemplateAsync(NoteId id);

    // Every save keeps what it replaces (UpdateAsync); newest first, without their content.
    Task<Result<IReadOnlyList<NoteVersionRow>, AppError>> VersionsAsync(NoteId id);
    Task<Result<NoteVersionRow, AppError>> GetVersionAsync(Guid versionId);
}
