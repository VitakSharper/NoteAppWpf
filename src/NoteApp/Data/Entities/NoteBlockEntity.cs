using NoteApp.Domain.Models;

namespace NoteApp.Data.Entities;

public class NoteBlockEntity
{
    public Guid Id { get; set; }
    public Guid NoteId { get; set; }
    public BlockType BlockType { get; set; }
    public int SortOrder { get; set; }

    // Text block — PlainText is null for rows saved before the column existed.
    // A checklist fills PlainText too (its item texts), so search and the list
    // preview need no special case.
    public string? TextContent { get; set; }
    public string? PlainText { get; set; }

    // File block
    public byte[]? FileData { get; set; }
    public string? FileName { get; set; }
    public string? FileExtension { get; set; }
    public long? FileSizeBytes { get; set; }

    // Link block
    public string? LinkUrl { get; set; }
    public string? LinkDescription { get; set; }

    // Checklist block — see Services/ChecklistJson.cs for the shape
    public string? ChecklistJson { get; set; }

    public NoteEntity Note { get; set; } = null!;
}
