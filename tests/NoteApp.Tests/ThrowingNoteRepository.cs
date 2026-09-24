using NoteApp.Data.Queries;
using NoteApp.Data.Repositories;
using NoteApp.Domain.Functional;
using NoteApp.Domain.Models;
using NoteApp.Domain.ValueObjects;

namespace NoteApp.Tests;

// Every call throws: a test overrides what it expects to reach storage, so anything else
// that does is a bug in the test. One place to extend when the interface grows.
internal class ThrowingNoteRepository : INoteRepository
{
    public virtual Task<Result<Note, AppError>> GetByIdAsync(NoteId id) => throw new NotSupportedException();
    public virtual Task<Result<Note, AppError>> CreateAsync(Note note, byte[]? encryptedContent = null) => throw new NotSupportedException();
    public virtual Task<Result<Note, AppError>> UpdateAsync(Note note, byte[]? encryptedContent = null) => throw new NotSupportedException();
    public virtual Task<Result<Unit, AppError>> DeleteAsync(NoteId id) => throw new NotSupportedException();
    public virtual Task<Result<Unit, AppError>> RestoreAsync(NoteId id) => throw new NotSupportedException();
    public virtual Task<Result<Unit, AppError>> SetPinnedAsync(NoteId id, bool isPinned) => throw new NotSupportedException();
    public virtual Task<Result<Unit, AppError>> PurgeAsync(NoteId id) => throw new NotSupportedException();
    public virtual Task<Result<int, AppError>> PurgeAllDeletedAsync() => throw new NotSupportedException();
    public virtual Task<Result<IReadOnlyList<NoteSummaryRow>, AppError>> SearchSummariesAsync(NoteQuery query) => throw new NotSupportedException();
    public virtual Task<Result<Unit, AppError>> SetArchivedAsync(NoteId id, bool isArchived) => throw new NotSupportedException();
    public virtual Task<Result<byte[]?, AppError>> GetEncryptedContentAsync(NoteId id) => throw new NotSupportedException();
    public virtual Task<Result<IReadOnlyList<NoteRow>, AppError>> LinkedFromAsync(NoteId id) => throw new NotSupportedException();
    public virtual Task<Result<NoteId, AppError>> DuplicateAsync(NoteId id, string title) => throw new NotSupportedException();
    public virtual Task<Result<IReadOnlyList<NoteRow>, AppError>> TemplatesAsync() => throw new NotSupportedException();
    public virtual Task<Result<Note, AppError>> GetTemplateAsync(NoteId id) => throw new NotSupportedException();
    public virtual Task<Result<Unit, AppError>> SaveTemplateAsync(Note template) => throw new NotSupportedException();
    public virtual Task<Result<Unit, AppError>> DeleteTemplateAsync(NoteId id) => throw new NotSupportedException();
    public virtual Task<Result<IReadOnlyList<NoteVersionRow>, AppError>> VersionsAsync(NoteId id) => throw new NotSupportedException();
    public virtual Task<Result<NoteVersionRow, AppError>> GetVersionAsync(Guid versionId) => throw new NotSupportedException();
}
