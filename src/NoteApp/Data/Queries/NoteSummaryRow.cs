using NoteApp.Data.Entities;

namespace NoteApp.Data.Queries;

// Flat row EF Core materializes for the note list: no block payloads, no file
// bytes. FirstTextRich is only filled for rows saved before PlainText existed,
// so the preview can still be derived from the rich payload for them.
public sealed class NoteSummaryRow
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public bool IsEncrypted { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public bool HasText { get; init; }
    public bool HasFiles { get; init; }
    public bool HasLinks { get; init; }
    public string? FirstTextPlain { get; init; }
    public string? FirstTextRich { get; init; }
    public List<TagEntity> Tags { get; init; } = [];
}
