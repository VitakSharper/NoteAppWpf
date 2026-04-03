using NoteApp.Domain.Functional;
using NoteApp.Domain.ValueObjects;

namespace NoteApp.Domain.Models;

public sealed record Note(
    NoteId Id,
    NoteTitle Title,
    IReadOnlyList<NoteBlock> Blocks,
    IReadOnlyList<Tag> Tags,
    bool IsEncrypted,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    public static Result<Note, AppError> Create(
        NoteTitle title,
        IReadOnlyList<NoteBlock> blocks,
        IReadOnlyList<Tag> tags,
        bool isEncrypted = false) =>
        blocks.Count == 0
            ? Result<Note, AppError>.Fail(AppError.Validation("A note must have at least one content block."))
            : Result<Note, AppError>.Ok(new Note(
                NoteId.New(),
                title,
                blocks,
                tags,
                isEncrypted,
                DateTime.UtcNow,
                DateTime.UtcNow));

    public Note WithTitle(NoteTitle title) =>
        this with { Title = title, UpdatedAt = DateTime.UtcNow };

    public Note WithBlocks(IReadOnlyList<NoteBlock> blocks) =>
        this with { Blocks = blocks, UpdatedAt = DateTime.UtcNow };

    public Note WithTags(IReadOnlyList<Tag> tags) =>
        this with { Tags = tags, UpdatedAt = DateTime.UtcNow };

    public bool HasText => Blocks.Any(b => b is NoteBlock.Text);
    public bool HasFiles => Blocks.Any(b => b is NoteBlock.File);
    public bool HasLinks => Blocks.Any(b => b is NoteBlock.Link);
}
