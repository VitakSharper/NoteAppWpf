using Microsoft.EntityFrameworkCore;
using NoteApp.Domain.Functional;
using NoteApp.Domain.Models;
using NoteApp.Services.Mapping;

namespace NoteApp.Data.Repositories;

// One short-lived DbContext per operation — see NoteRepository.
public sealed class TagRepository(IDbContextFactory<NoteDbContext> contextFactory) : ITagRepository
{
    public async Task<Result<IReadOnlyList<Tag>, AppError>> GetAllAsync()
    {
        try
        {
            await using var context = await contextFactory.CreateDbContextAsync();
            var entities = await context.Tags
                .OrderBy(t => t.Name)
                .AsNoTracking()
                .ToListAsync();

            IReadOnlyList<Tag> tags = entities.Select(TagMapper.ToDomain).ToList();
            return Result<IReadOnlyList<Tag>, AppError>.Ok(tags);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<Tag>, AppError>.Fail(AppError.Database(ex.Message));
        }
    }

    public async Task<Result<Tag, AppError>> CreateAsync(Tag tag)
    {
        try
        {
            await using var context = await contextFactory.CreateDbContextAsync();
            var exists = await context.Tags.AnyAsync(t => t.Name == tag.Name.Value);
            if (exists)
                return Result<Tag, AppError>.Fail(AppError.Validation($"Tag '{tag.Name}' already exists."));

            context.Tags.Add(TagMapper.ToEntity(tag));
            await context.SaveChangesAsync();
            return Result<Tag, AppError>.Ok(tag);
        }
        catch (Exception ex)
        {
            return Result<Tag, AppError>.Fail(AppError.Database(ex.Message));
        }
    }

    public async Task<Result<Tag, AppError>> UpdateAsync(Tag tag)
    {
        try
        {
            await using var context = await contextFactory.CreateDbContextAsync();
            var entity = await context.Tags.FindAsync(tag.Id);
            if (entity is null)
                return Result<Tag, AppError>.Fail(AppError.NotFound($"Tag with ID {tag.Id} not found."));

            var duplicate = await context.Tags.AnyAsync(t => t.Name == tag.Name.Value && t.Id != tag.Id);
            if (duplicate)
                return Result<Tag, AppError>.Fail(AppError.Validation($"Tag '{tag.Name}' already exists."));

            entity.Name = tag.Name.Value;
            await context.SaveChangesAsync();
            return Result<Tag, AppError>.Ok(tag);
        }
        catch (Exception ex)
        {
            return Result<Tag, AppError>.Fail(AppError.Database(ex.Message));
        }
    }

    public async Task<Result<Unit, AppError>> DeleteAsync(Guid id)
    {
        try
        {
            await using var context = await contextFactory.CreateDbContextAsync();
            var entity = await context.Tags.FindAsync(id);
            if (entity is null)
                return Result<Unit, AppError>.Fail(AppError.NotFound($"Tag with ID {id} not found."));

            context.Tags.Remove(entity);
            await context.SaveChangesAsync();
            return Result<Unit, AppError>.Ok(Unit.Value);
        }
        catch (Exception ex)
        {
            return Result<Unit, AppError>.Fail(AppError.Database(ex.Message));
        }
    }
}
