using System.Security.Cryptography;
using NoteApp.Data.Repositories;
using NoteApp.Domain.Extensions;
using NoteApp.Domain.Functional;
using NoteApp.Domain.Models;
using NoteApp.Domain.ValueObjects;

namespace NoteApp.Services;

public sealed class NoteService(INoteRepository noteRepository)
{
    public Task<Result<IReadOnlyList<Note>, AppError>> GetAllNotesAsync() =>
        noteRepository.GetAllAsync();

    public Task<Result<Note, AppError>> GetNoteByIdAsync(NoteId id) =>
        noteRepository.GetByIdAsync(id);

    public async Task<Result<Note, AppError>> CreateNoteAsync(
        string title, IReadOnlyList<NoteBlock> blocks, IReadOnlyList<Tag> tags, string? password = null)
    {
        var titleResult = NoteTitle.From(title);
        if (titleResult.IsFailure)
            return Result<Note, AppError>.Fail(((Result<NoteTitle, AppError>.Failure)titleResult).Error);

        var noteTitle = ((Result<NoteTitle, AppError>.Success)titleResult).Value;
        var isEncrypted = password is not null;
        var noteResult = Note.Create(noteTitle, blocks, tags, isEncrypted);
        if (noteResult.IsFailure)
            return noteResult;

        var note = ((Result<Note, AppError>.Success)noteResult).Value;
        byte[]? encryptedContent = null;

        if (password is not null)
            encryptedContent = EncryptionService.EncryptBlocks(blocks, password);

        return await noteRepository.CreateAsync(note, encryptedContent);
    }

    public async Task<Result<Note, AppError>> UpdateNoteAsync(Note note, string? password = null)
    {
        byte[]? encryptedContent = null;

        if (note.IsEncrypted && password is not null)
            encryptedContent = EncryptionService.EncryptBlocks(note.Blocks, password);

        return await noteRepository.UpdateAsync(note, encryptedContent);
    }

    public async Task<Result<Note, AppError>> UnlockNoteAsync(NoteId id, string password)
    {
        try
        {
            var noteResult = await noteRepository.GetByIdAsync(id);
            if (noteResult.IsFailure)
                return noteResult;

            var note = ((Result<Note, AppError>.Success)noteResult).Value;
            if (!note.IsEncrypted)
                return noteResult;

            var contentResult = await noteRepository.GetEncryptedContentAsync(id);
            if (contentResult.IsFailure)
                return Result<Note, AppError>.Fail(((Result<byte[]?, AppError>.Failure)contentResult).Error);

            var encryptedContent = ((Result<byte[]?, AppError>.Success)contentResult).Value;
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

    public Task<Result<Unit, AppError>> DeleteNoteAsync(NoteId id) =>
        noteRepository.DeleteAsync(id);

    public Task<Result<IReadOnlyList<Note>, AppError>> SearchAsync(
        string? searchText, IReadOnlyList<Guid>? tagIds, BlockType? blockType) =>
        noteRepository.SearchAsync(searchText, tagIds, blockType);
}
