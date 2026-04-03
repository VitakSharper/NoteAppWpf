using NoteApp.Data.Entities;
using NoteApp.Domain.Functional;
using NoteApp.Domain.Models;
using NoteApp.Domain.ValueObjects;

namespace NoteApp.Services.Mapping;

public static class NoteMapper
{
    public static Result<Note, AppError> ToDomain(NoteEntity entity) =>
        NoteTitle.From(entity.Title)
            .Bind(title =>
            {
                var blocks = entity.Blocks
                    .OrderBy(b => b.SortOrder)
                    .Select(MapBlock)
                    .ToList();

                var errors = blocks.Where(r => r.IsFailure).ToList();
                if (errors.Count > 0)
                    return Result<Note, AppError>.Fail(
                        errors.First() is Result<NoteBlock, AppError>.Failure f
                            ? f.Error
                            : AppError.Validation("Failed to map note blocks."));

                var domainBlocks = blocks
                    .Where(r => r.IsSuccess)
                    .Select(r => ((Result<NoteBlock, AppError>.Success)r).Value)
                    .ToList();

                var tags = entity.NoteTags
                    .Select(nt => TagMapper.ToDomain(nt.Tag))
                    .ToList();

                return Result<Note, AppError>.Ok(new Note(
                    new NoteId(entity.Id),
                    title,
                    domainBlocks,
                    tags,
                    entity.IsEncrypted,
                    entity.CreatedAt,
                    entity.UpdatedAt));
            });

    public static NoteEntity ToEntity(Note note) => new()
    {
        Id = note.Id.Value,
        Title = note.Title.Value,
        CreatedAt = note.CreatedAt,
        UpdatedAt = note.UpdatedAt,
        Blocks = note.Blocks.Select((block, index) => MapBlockToEntity(block, note.Id.Value, index)).ToList()
    };

    private static NoteBlockEntity MapBlockToEntity(NoteBlock block, Guid noteId, int sortOrder) =>
        block.Match(
            text: t => new NoteBlockEntity
            {
                Id = block.Id,
                NoteId = noteId,
                BlockType = BlockType.Text,
                SortOrder = sortOrder,
                TextContent = t.RichText
            },
            file: f => new NoteBlockEntity
            {
                Id = block.Id,
                NoteId = noteId,
                BlockType = BlockType.File,
                SortOrder = sortOrder,
                FileData = f.Data,
                FileName = f.FileName,
                FileExtension = f.Extension,
                FileSizeBytes = f.SizeBytes
            },
            link: l => new NoteBlockEntity
            {
                Id = block.Id,
                NoteId = noteId,
                BlockType = BlockType.Link,
                SortOrder = sortOrder,
                LinkUrl = l.Url.Value.ToString(),
                LinkDescription = l.Description
            });

    private static Result<NoteBlock, AppError> MapBlock(NoteBlockEntity entity) =>
        entity.BlockType switch
        {
            BlockType.Text => Result<NoteBlock, AppError>.Ok(
                new NoteBlock.Text(entity.TextContent ?? string.Empty)
                    { Id = entity.Id, SortOrder = entity.SortOrder }),

            BlockType.File => Result<NoteBlock, AppError>.Ok(
                new NoteBlock.File(
                    entity.FileData ?? [],
                    entity.FileName ?? "unknown",
                    entity.FileExtension ?? "",
                    entity.FileSizeBytes ?? 0)
                    { Id = entity.Id, SortOrder = entity.SortOrder }),

            BlockType.Link => LinkUrl.From(entity.LinkUrl)
                .Map<NoteBlock>(url => new NoteBlock.Link(url, entity.LinkDescription ?? string.Empty)
                    { Id = entity.Id, SortOrder = entity.SortOrder }),

            _ => Result<NoteBlock, AppError>.Fail(
                AppError.Validation($"Unknown block type: {entity.BlockType}"))
        };
}

public static class TagMapper
{
    public static Tag ToDomain(TagEntity entity) =>
        new(entity.Id, TagName.From(entity.Name).Match(
            success: name => name,
            failure: _ => TagName.From("unknown").Match(n => n, _ => throw new InvalidOperationException())));

    public static TagEntity ToEntity(Tag tag) =>
        new() { Id = tag.Id, Name = tag.Name.Value };
}
