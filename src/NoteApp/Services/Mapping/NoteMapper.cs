using NoteApp.Data.Entities;
using NoteApp.Data.Queries;
using NoteApp.Domain.Functional;
using NoteApp.Domain.Models;
using NoteApp.Domain.ValueObjects;
using NoteApp.Services;

namespace NoteApp.Services.Mapping;

public static class NoteMapper
{
    public static Result<Note, AppError> ToDomain(NoteEntity entity) =>
        NoteTitle.From(entity.Title)
            .Bind(title => MapBlocks(entity.Blocks)
                .Map(blocks => new Note(
                    new NoteId(entity.Id),
                    title,
                    blocks,
                    entity.NoteTags.Select(nt => TagMapper.ToDomain(nt.Tag)).ToList(),
                    entity.IsEncrypted,
                    entity.CreatedAt,
                    entity.UpdatedAt)));

    public static Result<NoteSummary, AppError> ToSummary(NoteSummaryRow row, string preview) =>
        NoteTitle.From(row.Title)
            .Map(title => new NoteSummary(
                new NoteId(row.Id),
                title,
                preview,
                row.Tags.Select(TagMapper.ToDomain).ToList(),
                row.IsEncrypted,
                row.HasText,
                row.HasFiles,
                row.HasLinks,
                row.HasChecklists,
                row.CreatedAt,
                row.UpdatedAt,
                row.DeletedAt,
                row.IsPinned,
                row.HasSecrets,
                row.HasCode,
                row.ArchivedAt,
                row.RemindAt is { } remind ? DateTime.SpecifyKind(remind, DateTimeKind.Utc) : null));

    // Rows whose title would not make a NoteTitle (a hand-edited database) are left out.
    public static IReadOnlyList<NoteRef> ToRefs(IEnumerable<NoteRow> rows) =>
        rows.Select(r => NoteTitle.From(r.Title).Match(t => new NoteRef(new NoteId(r.Id), t), _ => null))
            .OfType<NoteRef>()
            .ToList();

    public static NoteEntity ToEntity(Note note) => new()
    {
        Id = note.Id.Value,
        Title = note.Title.Value,
        CreatedAt = note.CreatedAt,
        UpdatedAt = note.UpdatedAt,
        Blocks = ToBlockEntities(note)
    };

    // Storage order is the list order: SortOrder is reassigned from the index.
    public static List<NoteBlockEntity> ToBlockEntities(Note note) =>
        note.Blocks.Select((block, index) => ToBlockEntity(block, note.Id.Value, index)).ToList();

    private static NoteBlockEntity ToBlockEntity(NoteBlock block, Guid noteId, int sortOrder) =>
        block.Match(
            text: t => new NoteBlockEntity
            {
                Id = block.Id,
                NoteId = noteId,
                BlockType = BlockType.Text,
                SortOrder = sortOrder,
                TextContent = t.RichText,
                PlainText = t.PlainText,
                LinkedNoteIds = NoteLinks.Join(t.NoteLinks)
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
            },
            // PlainText is filled here too: it is what search and the preview read.
            checklist: c => new NoteBlockEntity
            {
                Id = block.Id,
                NoteId = noteId,
                BlockType = BlockType.Checklist,
                SortOrder = sortOrder,
                ChecklistJson = ChecklistJson.Serialize(c.Items),
                PlainText = c.PlainText
            },
            secret: s => new NoteBlockEntity
            {
                Id = block.Id,
                NoteId = noteId,
                BlockType = BlockType.Secret,
                SortOrder = sortOrder,
                SecretJson = SecretJson.Serialize(s),
                PlainText = s.PlainText
            },
            code: c => new NoteBlockEntity
            {
                Id = block.Id,
                NoteId = noteId,
                BlockType = BlockType.Code,
                SortOrder = sortOrder,
                PlainText = c.Content
            });

    // First failure wins; blocks come out ordered by SortOrder.
    public static Result<IReadOnlyList<NoteBlock>, AppError> MapBlocks(IEnumerable<NoteBlockEntity> entities)
    {
        var blocks = new List<NoteBlock>();
        foreach (var entity in entities.OrderBy(b => b.SortOrder))
        {
            if (!MapBlock(entity).TryGet(out var block, out var error))
                return Result<IReadOnlyList<NoteBlock>, AppError>.Fail(error);
            blocks.Add(block);
        }
        return Result<IReadOnlyList<NoteBlock>, AppError>.Ok(blocks);
    }

    private static Result<NoteBlock, AppError> MapBlock(NoteBlockEntity entity) =>
        entity.BlockType switch
        {
            BlockType.Text => Result<NoteBlock, AppError>.Ok(
                new NoteBlock.Text(entity.TextContent ?? string.Empty, entity.PlainText ?? string.Empty)
                    { Id = entity.Id, SortOrder = entity.SortOrder, NoteLinks = NoteLinks.Split(entity.LinkedNoteIds) }),

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

            BlockType.Checklist => Result<NoteBlock, AppError>.Ok(
                new NoteBlock.Checklist(ChecklistJson.Deserialize(entity.ChecklistJson))
                    { Id = entity.Id, SortOrder = entity.SortOrder }),

            BlockType.Secret => Result<NoteBlock, AppError>.Ok(
                SecretJson.Deserialize(entity.SecretJson) with { Id = entity.Id, SortOrder = entity.SortOrder }),

            BlockType.Code => Result<NoteBlock, AppError>.Ok(
                new NoteBlock.Code(entity.PlainText ?? string.Empty) { Id = entity.Id, SortOrder = entity.SortOrder }),

            _ => Result<NoteBlock, AppError>.Fail(
                AppError.Validation($"Unknown block type: {entity.BlockType}"))
        };
}

public static class TagMapper
{
    public static Tag ToDomain(TagEntity entity) =>
        new(entity.Id,
            TagName.From(entity.Name).Match(
                success: name => name,
                failure: _ => TagName.From("unknown").Match(n => n, _ => throw new InvalidOperationException())),
            ColorOf(entity.Color));

    public static TagEntity ToEntity(Tag tag) =>
        new() { Id = tag.Id, Name = tag.Name.Value, Color = tag.Color.ToString() };

    // Null (a tag older than colours) and anything unrecognised read as the old violet.
    public static TagColor ColorOf(string? stored) =>
        Enum.TryParse<TagColor>(stored, ignoreCase: true, out var color) && Enum.IsDefined(color) ? color : TagColor.Violet;
}
