using Microsoft.EntityFrameworkCore;
using NoteApp.Data.Entities;
using NoteApp.Domain.Functional;
using NoteApp.Domain.Models;
using NoteApp.Domain.ValueObjects;
using NoteApp.Services.Mapping;

namespace NoteApp.Data.Repositories;

public sealed class NoteRepository(NoteDbContext context) : INoteRepository
{
    public async Task<Result<IReadOnlyList<Note>, AppError>> GetAllAsync()
    {
        try
        {
            var entities = await QueryNotes()
                .OrderByDescending(n => n.UpdatedAt)
                .ToListAsync();
            return MapEntities(entities);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<Note>, AppError>.Fail(AppError.Database(ex.Message));
        }
    }

    public async Task<Result<Note, AppError>> GetByIdAsync(NoteId id)
    {
        try
        {
            var entity = await QueryNotes()
                .FirstOrDefaultAsync(n => n.Id == id.Value);

            return entity is null
                ? Result<Note, AppError>.Fail(AppError.NotFound($"Note with ID {id} not found."))
                : NoteMapper.ToDomain(entity);
        }
        catch (Exception ex)
        {
            return Result<Note, AppError>.Fail(AppError.Database(ex.Message));
        }
    }

    public async Task<Result<Note, AppError>> CreateAsync(Note note, byte[]? encryptedContent = null)
    {
        try
        {
            var entity = NoteMapper.ToEntity(note);
            entity.NoteTags = note.Tags.Select(t => new NoteTagEntity
            {
                NoteId = note.Id.Value,
                TagId = t.Id
            }).ToList();

            if (encryptedContent is not null)
            {
                entity.IsEncrypted = true;
                entity.EncryptedContent = encryptedContent;
                entity.Blocks = [];
            }

            context.Notes.Add(entity);
            await context.SaveChangesAsync();
            return await GetByIdAsync(note.Id);
        }
        catch (Exception ex)
        {
            return Result<Note, AppError>.Fail(AppError.Database(ex.Message));
        }
    }

    public async Task<Result<Note, AppError>> UpdateAsync(Note note, byte[]? encryptedContent = null)
    {
        try
        {
            var existing = await context.Notes
                .Include(n => n.Blocks)
                .Include(n => n.NoteTags)
                .FirstOrDefaultAsync(n => n.Id == note.Id.Value);

            if (existing is null)
                return Result<Note, AppError>.Fail(AppError.NotFound($"Note with ID {note.Id} not found."));

            existing.Title = note.Title.Value;
            existing.UpdatedAt = note.UpdatedAt;

            // Replace all blocks
            context.NoteBlocks.RemoveRange(existing.Blocks);

            if (encryptedContent is not null)
            {
                existing.IsEncrypted = true;
                existing.EncryptedContent = encryptedContent;
                existing.Blocks = [];
            }
            else
            {
                existing.IsEncrypted = false;
                existing.EncryptedContent = null;
                existing.Blocks = note.Blocks.Select((block, index) =>
                {
                    var blockEntity = new NoteBlockEntity
                    {
                        Id = block.Id,
                        NoteId = note.Id.Value,
                        BlockType = block.Type,
                        SortOrder = index
                    };
                    block.Match<Unit>(
                        text: t => { blockEntity.TextContent = t.RichText; return Unit.Value; },
                        file: f =>
                        {
                            blockEntity.FileData = f.Data;
                            blockEntity.FileName = f.FileName;
                            blockEntity.FileExtension = f.Extension;
                            blockEntity.FileSizeBytes = f.SizeBytes;
                            return Unit.Value;
                        },
                        link: l =>
                        {
                            blockEntity.LinkUrl = l.Url.Value.ToString();
                            blockEntity.LinkDescription = l.Description;
                            return Unit.Value;
                        });
                    return blockEntity;
                }).ToList();
            }

            // Replace tags
            existing.NoteTags.Clear();
            foreach (var tag in note.Tags)
                existing.NoteTags.Add(new NoteTagEntity { NoteId = note.Id.Value, TagId = tag.Id });

            await context.SaveChangesAsync();
            return await GetByIdAsync(note.Id);
        }
        catch (Exception ex)
        {
            return Result<Note, AppError>.Fail(AppError.Database(ex.Message));
        }
    }

    public async Task<Result<byte[]?, AppError>> GetEncryptedContentAsync(NoteId id)
    {
        try
        {
            var content = await context.Notes.AsNoTracking()
                .Where(n => n.Id == id.Value)
                .Select(n => n.EncryptedContent)
                .FirstOrDefaultAsync();
            return Result<byte[]?, AppError>.Ok(content);
        }
        catch (Exception ex)
        {
            return Result<byte[]?, AppError>.Fail(AppError.Database(ex.Message));
        }
    }

    public async Task<Result<Unit, AppError>> DeleteAsync(NoteId id)
    {
        try
        {
            var entity = await context.Notes.FindAsync(id.Value);
            if (entity is null)
                return Result<Unit, AppError>.Fail(AppError.NotFound($"Note with ID {id} not found."));

            context.Notes.Remove(entity);
            await context.SaveChangesAsync();
            return Result<Unit, AppError>.Ok(Unit.Value);
        }
        catch (Exception ex)
        {
            return Result<Unit, AppError>.Fail(AppError.Database(ex.Message));
        }
    }

    public async Task<Result<IReadOnlyList<Note>, AppError>> SearchAsync(
        string? searchText,
        IReadOnlyList<Guid>? tagIds,
        BlockType? blockType)
    {
        try
        {
            var query = QueryNotes();

            if (!string.IsNullOrWhiteSpace(searchText))
            {
                var term = searchText.ToLower();
                query = query.Where(n =>
                    n.Title.ToLower().Contains(term) ||
                    n.Blocks.Any(b =>
                        (b.TextContent != null && b.TextContent.ToLower().Contains(term)) ||
                        (b.LinkDescription != null && b.LinkDescription.ToLower().Contains(term)) ||
                        (b.FileName != null && b.FileName.ToLower().Contains(term))));
            }

            if (tagIds is { Count: > 0 })
                query = query.Where(n => n.NoteTags.Any(nt => tagIds.Contains(nt.TagId)));

            if (blockType.HasValue)
                query = query.Where(n => n.Blocks.Any(b => b.BlockType == blockType.Value));

            var entities = await query
                .OrderByDescending(n => n.UpdatedAt)
                .ToListAsync();

            return MapEntities(entities);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<Note>, AppError>.Fail(AppError.Database(ex.Message));
        }
    }

    private IQueryable<NoteEntity> QueryNotes() =>
        context.Notes
            .Include(n => n.Blocks.OrderBy(b => b.SortOrder))
            .Include(n => n.NoteTags).ThenInclude(nt => nt.Tag)
            .AsNoTracking();

    private static Result<IReadOnlyList<Note>, AppError> MapEntities(List<NoteEntity> entities)
    {
        var notes = new List<Note>();
        foreach (var entity in entities)
        {
            var result = NoteMapper.ToDomain(entity);
            if (result is Result<Note, AppError>.Failure f)
                return Result<IReadOnlyList<Note>, AppError>.Fail(f.Error);
            if (result is Result<Note, AppError>.Success s)
                notes.Add(s.Value);
        }
        return Result<IReadOnlyList<Note>, AppError>.Ok(notes);
    }
}
