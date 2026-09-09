using System.Security.Cryptography;
using NoteApp.Data.Repositories;
using NoteApp.Domain.Functional;
using NoteApp.Domain.Models;
using NoteApp.Domain.ValueObjects;
using NoteApp.Services.Mapping;

namespace NoteApp.Services;

public sealed class NoteService(INoteRepository noteRepository)
{
    public Task<Result<Note, AppError>> GetNoteByIdAsync(NoteId id) =>
        noteRepository.GetByIdAsync(id);

    public async Task<Result<Note, AppError>> CreateNoteAsync(
        string title, IReadOnlyList<NoteBlock> blocks, IReadOnlyList<Tag> tags, string? password = null)
    {
        if (!NoteTitle.From(title).TryGet(out var noteTitle, out var titleError))
            return Result<Note, AppError>.Fail(titleError);

        if (!Note.Create(noteTitle, blocks, tags, isEncrypted: password is not null).TryGet(out var note, out var noteError))
            return Result<Note, AppError>.Fail(noteError);

        var encryptedContent = password is null ? null : EncryptionService.EncryptBlocks(blocks, password);
        return await noteRepository.CreateAsync(note, encryptedContent);
    }

    public Task<Result<Note, AppError>> UpdateNoteAsync(Note note, string? password = null)
    {
        if (!note.IsEncrypted)
            return noteRepository.UpdateAsync(note);

        // Never let an encrypted note fall through to the plaintext path: without
        // the password the repository would store the blocks in clear.
        if (password is null)
            return Task.FromResult(Result<Note, AppError>.Fail(
                AppError.Validation("A password is required to save an encrypted note.")));

        return noteRepository.UpdateAsync(note, EncryptionService.EncryptBlocks(note.Blocks, password));
    }

    public async Task<Result<Note, AppError>> UnlockNoteAsync(NoteId id, string password)
    {
        try
        {
            if (!(await noteRepository.GetByIdAsync(id)).TryGet(out var note, out var noteError))
                return Result<Note, AppError>.Fail(noteError);

            if (!note.IsEncrypted)
                return Result<Note, AppError>.Ok(note);

            if (!(await noteRepository.GetEncryptedContentAsync(id)).TryGet(out var encryptedContent, out var contentError))
                return Result<Note, AppError>.Fail(contentError);

            if (encryptedContent is null)
                return Result<Note, AppError>.Fail(AppError.Validation("No encrypted content found."));

            var blocks = EncryptionService.DecryptBlocks(encryptedContent, password);
            return Result<Note, AppError>.Ok(note with { Blocks = blocks });
        }
        catch (CryptographicException)
        {
            return Result<Note, AppError>.Fail(AppError.Validation("Wrong password."));
        }
        catch (Exception ex)
        {
            return Result<Note, AppError>.Fail(AppError.Database(ex.Message));
        }
    }

    // Soft: the note goes to the trash, where it can be restored or purged.
    public Task<Result<Unit, AppError>> DeleteNoteAsync(NoteId id) =>
        noteRepository.DeleteAsync(id);

    public Task<Result<Unit, AppError>> RestoreNoteAsync(NoteId id) =>
        noteRepository.RestoreAsync(id);

    public Task<Result<Unit, AppError>> PurgeNoteAsync(NoteId id) =>
        noteRepository.PurgeAsync(id);

    public Task<Result<int, AppError>> EmptyTrashAsync() =>
        noteRepository.PurgeAllDeletedAsync();

    // The list never needs full notes: rows come back without block payloads,
    // and the preview is derived once here rather than per row at render time.
    public async Task<Result<IReadOnlyList<NoteSummary>, AppError>> SearchAsync(
        string? searchText, IReadOnlyList<Guid>? tagIds, BlockType? blockType, bool deletedOnly = false)
    {
        if (!(await noteRepository.SearchSummariesAsync(searchText, tagIds, blockType, deletedOnly)).TryGet(out var rows, out var error))
            return Result<IReadOnlyList<NoteSummary>, AppError>.Fail(error);

        var summaries = new List<NoteSummary>(rows.Count);
        foreach (var row in rows)
        {
            var preview = row.IsEncrypted ? string.Empty : RichTextPreview.Snippet(row.FirstTextPlain, row.FirstTextRich);
            if (!NoteMapper.ToSummary(row, preview).TryGet(out var summary, out var mapError))
                return Result<IReadOnlyList<NoteSummary>, AppError>.Fail(mapError);
            summaries.Add(summary);
        }

        return Result<IReadOnlyList<NoteSummary>, AppError>.Ok(summaries);
    }
}
