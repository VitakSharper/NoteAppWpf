using Microsoft.EntityFrameworkCore;
using NoteApp.Data.Entities;
using NoteApp.Data.Queries;
using NoteApp.Domain.Functional;
using NoteApp.Domain.Models;
using NoteApp.Domain.ValueObjects;
using NoteApp.Services.Mapping;

namespace NoteApp.Data.Repositories;

// One short-lived DbContext per operation. Everything in the app is resolved
// from the root provider, so a scoped context would live as long as the app
// and fail as soon as two async operations overlapped.
public sealed class NoteRepository(IDbContextFactory<NoteDbContext> contextFactory) : INoteRepository
{
    public async Task<Result<Note, AppError>> GetByIdAsync(NoteId id)
    {
        try
        {
            await using var context = await contextFactory.CreateDbContextAsync();
            var entity = await QueryNotes(context)
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

            await using (var context = await contextFactory.CreateDbContextAsync())
            {
                context.Notes.Add(entity);
                await context.SaveChangesAsync();
            }

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
            await using (var context = await contextFactory.CreateDbContextAsync())
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
                    existing.Blocks = NoteMapper.ToBlockEntities(note);
                }

                // Replace tags
                existing.NoteTags.Clear();
                foreach (var tag in note.Tags)
                    existing.NoteTags.Add(new NoteTagEntity { NoteId = note.Id.Value, TagId = tag.Id });

                await context.SaveChangesAsync();
            }

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
            await using var context = await contextFactory.CreateDbContextAsync();
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
            await using var context = await contextFactory.CreateDbContextAsync();
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

    public async Task<Result<IReadOnlyList<NoteSummaryRow>, AppError>> SearchSummariesAsync(
        string? searchText,
        IReadOnlyList<Guid>? tagIds,
        BlockType? blockType)
    {
        try
        {
            await using var context = await contextFactory.CreateDbContextAsync();
            var query = context.Notes.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(searchText))
            {
                // No ToLower(): SQL Server's default collation is already
                // case-insensitive and lower-casing both sides defeats indexes.
                // Text blocks match on PlainText; rows saved before that column
                // existed fall back to the raw rich payload.
                var term = searchText.Trim();
                query = query.Where(n =>
                    n.Title.Contains(term) ||
                    n.Blocks.Any(b =>
                        (b.PlainText != null ? b.PlainText.Contains(term) : b.TextContent != null && b.TextContent.Contains(term)) ||
                        (b.LinkDescription != null && b.LinkDescription.Contains(term)) ||
                        (b.FileName != null && b.FileName.Contains(term))));
            }

            if (tagIds is { Count: > 0 })
                query = query.Where(n => n.NoteTags.Any(nt => tagIds.Contains(nt.TagId)));

            if (blockType.HasValue)
                query = query.Where(n => n.Blocks.Any(b => b.BlockType == blockType.Value));

            var rows = await query
                .OrderByDescending(n => n.UpdatedAt)
                .Select(n => new NoteSummaryRow
                {
                    Id = n.Id,
                    Title = n.Title,
                    IsEncrypted = n.IsEncrypted,
                    CreatedAt = n.CreatedAt,
                    UpdatedAt = n.UpdatedAt,
                    HasText = n.Blocks.Any(b => b.BlockType == BlockType.Text),
                    HasFiles = n.Blocks.Any(b => b.BlockType == BlockType.File),
                    HasLinks = n.Blocks.Any(b => b.BlockType == BlockType.Link),
                    FirstTextPlain = n.Blocks
                        .Where(b => b.BlockType == BlockType.Text)
                        .OrderBy(b => b.SortOrder)
                        .Select(b => b.PlainText)
                        .FirstOrDefault(),
                    // Only pay for the rich payload when there is no PlainText yet.
                    FirstTextRich = n.Blocks
                        .Where(b => b.BlockType == BlockType.Text)
                        .OrderBy(b => b.SortOrder)
                        .Select(b => b.PlainText == null ? b.TextContent : null)
                        .FirstOrDefault(),
                    Tags = n.NoteTags.Select(nt => nt.Tag).OrderBy(t => t.Name).ToList()
                })
                .ToListAsync();

            return Result<IReadOnlyList<NoteSummaryRow>, AppError>.Ok(rows);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<NoteSummaryRow>, AppError>.Fail(AppError.Database(ex.Message));
        }
    }

    private static IQueryable<NoteEntity> QueryNotes(NoteDbContext context) =>
        context.Notes
            .Include(n => n.Blocks.OrderBy(b => b.SortOrder))
            .Include(n => n.NoteTags).ThenInclude(nt => nt.Tag)
            .AsNoTracking();
}
