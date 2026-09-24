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

    public async Task<Result<IReadOnlyList<NoteRow>, AppError>> LinkedFromAsync(NoteId id)
    {
        try
        {
            await using var context = await contextFactory.CreateDbContextAsync();
            var key = id.Value.ToString("D");
            var rows = await context.Notes.AsNoTracking()
                .Where(n => n.Id != id.Value && n.Blocks.Any(b => b.LinkedNoteIds != null && b.LinkedNoteIds.Contains(key)))
                .OrderBy(n => n.Title)
                .Select(n => new NoteRow { Id = n.Id, Title = n.Title })
                .ToListAsync();
            return Result<IReadOnlyList<NoteRow>, AppError>.Ok(rows);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<NoteRow>, AppError>.Fail(AppError.Database(ex.Message));
        }
    }

    public async Task<Result<NoteId, AppError>> DuplicateAsync(NoteId id, string title)
    {
        try
        {
            await using var context = await contextFactory.CreateDbContextAsync();
            var source = await context.Notes.AsNoTracking()
                .Include(n => n.Blocks)
                .Include(n => n.NoteTags)
                .FirstOrDefaultAsync(n => n.Id == id.Value);
            if (source is null)
                return Result<NoteId, AppError>.Fail(AppError.NotFound($"Note with ID {id} not found."));

            var copy = CopyOf(source, title, isTemplate: false);
            context.Notes.Add(copy);
            await context.SaveChangesAsync();
            return Result<NoteId, AppError>.Ok(new NoteId(copy.Id));
        }
        catch (Exception ex)
        {
            return Result<NoteId, AppError>.Fail(AppError.Database(ex.Message));
        }
    }

    public async Task<Result<IReadOnlyList<NoteRow>, AppError>> TemplatesAsync()
    {
        try
        {
            await using var context = await contextFactory.CreateDbContextAsync();
            var rows = await Templates(context).AsNoTracking()
                .OrderBy(n => n.Title)
                .Select(n => new NoteRow { Id = n.Id, Title = n.Title })
                .ToListAsync();
            return Result<IReadOnlyList<NoteRow>, AppError>.Ok(rows);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<NoteRow>, AppError>.Fail(AppError.Database(ex.Message));
        }
    }

    public async Task<Result<Note, AppError>> GetTemplateAsync(NoteId id)
    {
        try
        {
            await using var context = await contextFactory.CreateDbContextAsync();
            var entity = await Templates(context).AsNoTracking()
                .Include(n => n.Blocks.OrderBy(b => b.SortOrder))
                .Include(n => n.NoteTags).ThenInclude(nt => nt.Tag)
                .FirstOrDefaultAsync(n => n.Id == id.Value);

            return entity is null
                ? Result<Note, AppError>.Fail(AppError.NotFound($"Template with ID {id} not found."))
                : NoteMapper.ToDomain(entity);
        }
        catch (Exception ex)
        {
            return Result<Note, AppError>.Fail(AppError.Database(ex.Message));
        }
    }

    // One transaction: the old template of that name only goes if the new one lands.
    public async Task<Result<Unit, AppError>> SaveTemplateAsync(Note template)
    {
        try
        {
            await using var context = await contextFactory.CreateDbContextAsync();
            var entity = NoteMapper.ToEntity(template);
            entity.IsTemplate = true;
            entity.NoteTags = template.Tags.Select(t => new NoteTagEntity { NoteId = entity.Id, TagId = t.Id }).ToList();

            context.Notes.RemoveRange(await Templates(context).Where(n => n.Title == entity.Title).ToListAsync());
            context.Notes.Add(entity);
            await context.SaveChangesAsync();
            return Result<Unit, AppError>.Ok(Unit.Value);
        }
        catch (Exception ex)
        {
            return Result<Unit, AppError>.Fail(AppError.Database(ex.Message));
        }
    }

    public async Task<Result<Unit, AppError>> DeleteTemplateAsync(NoteId id)
    {
        try
        {
            await using var context = await contextFactory.CreateDbContextAsync();
            var count = await Templates(context).Where(n => n.Id == id.Value).ExecuteDeleteAsync();
            return count == 0
                ? Result<Unit, AppError>.Fail(AppError.NotFound($"Template with ID {id} not found."))
                : Result<Unit, AppError>.Ok(Unit.Value);
        }
        catch (Exception ex)
        {
            return Result<Unit, AppError>.Fail(AppError.Database(ex.Message));
        }
    }

    // The global filter hides templates; these are the queries that want exactly them.
    private static IQueryable<NoteEntity> Templates(NoteDbContext context) =>
        context.Notes.IgnoreQueryFilters().Where(n => n.IsTemplate && n.DeletedAt == null);

    // Everything but the identity: new ids for the note and its blocks, fresh dates.
    private static NoteEntity CopyOf(NoteEntity source, string title, bool isTemplate)
    {
        var now = DateTime.UtcNow;
        var id = Guid.NewGuid();
        return new NoteEntity
        {
            Id = id,
            Title = title,
            IsEncrypted = source.IsEncrypted,
            EncryptedContent = source.EncryptedContent,
            CreatedAt = now,
            UpdatedAt = now,
            IsTemplate = isTemplate,
            Blocks = source.Blocks.Select(b => new NoteBlockEntity
            {
                Id = Guid.NewGuid(),
                NoteId = id,
                BlockType = b.BlockType,
                SortOrder = b.SortOrder,
                TextContent = b.TextContent,
                PlainText = b.PlainText,
                LinkedNoteIds = b.LinkedNoteIds,
                FileData = b.FileData,
                FileName = b.FileName,
                FileExtension = b.FileExtension,
                FileSizeBytes = b.FileSizeBytes,
                LinkUrl = b.LinkUrl,
                LinkDescription = b.LinkDescription,
                ChecklistJson = b.ChecklistJson,
                SecretJson = b.SecretJson
            }).ToList(),
            NoteTags = source.NoteTags.Select(t => new NoteTagEntity { NoteId = id, TagId = t.TagId }).ToList()
        };
    }

    // Soft delete: the row stays, DeletedAt is set, and the global query filter
    // hides it everywhere else. Not FindAsync — query filters do not apply to Find.
    public async Task<Result<Unit, AppError>> DeleteAsync(NoteId id)
    {
        try
        {
            await using var context = await contextFactory.CreateDbContextAsync();
            var entity = await context.Notes.FirstOrDefaultAsync(n => n.Id == id.Value);
            if (entity is null)
                return Result<Unit, AppError>.Fail(AppError.NotFound($"Note with ID {id} not found."));

            entity.DeletedAt = DateTime.UtcNow;
            await context.SaveChangesAsync();
            return Result<Unit, AppError>.Ok(Unit.Value);
        }
        catch (Exception ex)
        {
            return Result<Unit, AppError>.Fail(AppError.Database(ex.Message));
        }
    }

    // Only the flag: UpdatedAt stays, so pinning does not reorder "Updated (newest)".
    public async Task<Result<Unit, AppError>> SetPinnedAsync(NoteId id, bool isPinned)
    {
        try
        {
            await using var context = await contextFactory.CreateDbContextAsync();
            var count = await context.Notes
                .Where(n => n.Id == id.Value)
                .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsPinned, isPinned));

            return count == 0
                ? Result<Unit, AppError>.Fail(AppError.NotFound($"Note with ID {id} not found."))
                : Result<Unit, AppError>.Ok(Unit.Value);
        }
        catch (Exception ex)
        {
            return Result<Unit, AppError>.Fail(AppError.Database(ex.Message));
        }
    }

    public async Task<Result<Unit, AppError>> RestoreAsync(NoteId id)
    {
        try
        {
            await using var context = await contextFactory.CreateDbContextAsync();
            var entity = await context.Notes.IgnoreQueryFilters()
                .FirstOrDefaultAsync(n => n.Id == id.Value && n.DeletedAt != null);
            if (entity is null)
                return Result<Unit, AppError>.Fail(AppError.NotFound($"Note with ID {id} is not in the trash."));

            entity.DeletedAt = null;
            await context.SaveChangesAsync();
            return Result<Unit, AppError>.Ok(Unit.Value);
        }
        catch (Exception ex)
        {
            return Result<Unit, AppError>.Fail(AppError.Database(ex.Message));
        }
    }

    // Permanent. Only reachable for trashed rows, so a live note can never be
    // purged by accident: it has to be deleted (trashed) first.
    public async Task<Result<Unit, AppError>> PurgeAsync(NoteId id)
    {
        try
        {
            await using var context = await contextFactory.CreateDbContextAsync();
            var entity = await context.Notes.IgnoreQueryFilters()
                .FirstOrDefaultAsync(n => n.Id == id.Value && n.DeletedAt != null);
            if (entity is null)
                return Result<Unit, AppError>.Fail(AppError.NotFound($"Note with ID {id} is not in the trash."));

            context.Notes.Remove(entity);
            await context.SaveChangesAsync();
            return Result<Unit, AppError>.Ok(Unit.Value);
        }
        catch (Exception ex)
        {
            return Result<Unit, AppError>.Fail(AppError.Database(ex.Message));
        }
    }

    // Blocks and tag links follow through the ON DELETE CASCADE the migrations
    // created — the same cascade DeleteAsync relied on when it removed the row.
    public async Task<Result<int, AppError>> PurgeAllDeletedAsync()
    {
        try
        {
            await using var context = await contextFactory.CreateDbContextAsync();
            var count = await context.Notes.IgnoreQueryFilters()
                .Where(n => n.DeletedAt != null)
                .ExecuteDeleteAsync();
            return Result<int, AppError>.Ok(count);
        }
        catch (Exception ex)
        {
            return Result<int, AppError>.Fail(AppError.Database(ex.Message));
        }
    }

    public async Task<Result<Unit, AppError>> SetArchivedAsync(NoteId id, bool isArchived)
    {
        try
        {
            await using var context = await contextFactory.CreateDbContextAsync();
            DateTime? archivedAt = isArchived ? DateTime.UtcNow : null;
            var count = await context.Notes
                .Where(n => n.Id == id.Value)
                .ExecuteUpdateAsync(s => s.SetProperty(n => n.ArchivedAt, archivedAt));

            return count == 0
                ? Result<Unit, AppError>.Fail(AppError.NotFound($"Note with ID {id} not found."))
                : Result<Unit, AppError>.Ok(Unit.Value);
        }
        catch (Exception ex)
        {
            return Result<Unit, AppError>.Fail(AppError.Database(ex.Message));
        }
    }

    public async Task<Result<IReadOnlyList<NoteSummaryRow>, AppError>> SearchSummariesAsync(NoteQuery noteQuery)
    {
        try
        {
            await using var context = await contextFactory.CreateDbContextAsync();
            // The trash is the one place that wants the filtered-out rows, and only them.
            var query = noteQuery.Shelf switch
            {
                NoteShelf.Trash => context.Notes.IgnoreQueryFilters().AsNoTracking().Where(n => n.DeletedAt != null && !n.IsTemplate),
                NoteShelf.Archived => context.Notes.AsNoTracking().Where(n => n.ArchivedAt != null),
                NoteShelf.AllLive => context.Notes.AsNoTracking(),
                _ => context.Notes.AsNoTracking().Where(n => n.ArchivedAt == null)
            };

            // Every term has to match somewhere. No ToLower(): SQL Server's default collation
            // is already case-insensitive and lower-casing both sides defeats indexes. Text
            // blocks match on PlainText; rows saved before that column existed fall back to
            // the raw rich payload.
            foreach (var term in noteQuery.AllTerms.Select(t => t.Trim()).Where(t => t.Length > 0))
            {
                query = query.Where(n =>
                    n.Title.Contains(term) ||
                    n.Blocks.Any(b =>
                        (b.PlainText != null ? b.PlainText.Contains(term) : b.TextContent != null && b.TextContent.Contains(term)) ||
                        (b.LinkDescription != null && b.LinkDescription.Contains(term)) ||
                        (b.FileName != null && b.FileName.Contains(term))));
            }

            foreach (var name in noteQuery.AllTagNames)
                query = query.Where(n => n.NoteTags.Any(nt => nt.Tag.Name == name));

            var anyOfTags = noteQuery.AllAnyOfTagIds;
            if (anyOfTags.Count > 0)
                query = query.Where(n => n.NoteTags.Any(nt => anyOfTags.Contains(nt.TagId)));

            foreach (var type in noteQuery.AllMustHave.Distinct())
                query = query.Where(n => n.Blocks.Any(b => b.BlockType == type));

            if (noteQuery.PinnedOnly)
                query = query.Where(n => n.IsPinned);

            if (noteQuery.EncryptedOnly)
                query = query.Where(n => n.IsEncrypted);

            var rows = await query
                .OrderByDescending(n => n.UpdatedAt)
                .Select(n => new NoteSummaryRow
                {
                    Id = n.Id,
                    Title = n.Title,
                    IsEncrypted = n.IsEncrypted,
                    CreatedAt = n.CreatedAt,
                    UpdatedAt = n.UpdatedAt,
                    DeletedAt = n.DeletedAt,
                    ArchivedAt = n.ArchivedAt,
                    IsPinned = n.IsPinned,
                    HasText = n.Blocks.Any(b => b.BlockType == BlockType.Text),
                    HasFiles = n.Blocks.Any(b => b.BlockType == BlockType.File),
                    HasLinks = n.Blocks.Any(b => b.BlockType == BlockType.Link),
                    HasChecklists = n.Blocks.Any(b => b.BlockType == BlockType.Checklist),
                    HasSecrets = n.Blocks.Any(b => b.BlockType == BlockType.Secret),
                    HasCode = n.Blocks.Any(b => b.BlockType == BlockType.Code),
                    // Both kinds of block fill PlainText, so a note that opens with a
                    // checklist previews its items. Same Where on both sub-queries, or
                    // the two would not describe the same "first" block.
                    FirstTextPlain = n.Blocks
                        .Where(b => b.BlockType == BlockType.Text || b.BlockType == BlockType.Checklist)
                        .OrderBy(b => b.SortOrder)
                        .Select(b => b.PlainText)
                        .FirstOrDefault(),
                    // Only pay for the rich payload when there is no PlainText yet.
                    FirstTextRich = n.Blocks
                        .Where(b => b.BlockType == BlockType.Text || b.BlockType == BlockType.Checklist)
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
