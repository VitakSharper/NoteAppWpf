using NoteApp.Domain.ValueObjects;

namespace NoteApp.Domain.Models;

// Read model for the note list: everything the list shows and nothing more —
// no block payloads, no file bytes. Opening a note loads the full Note.
public sealed record NoteSummary(
    NoteId Id,
    NoteTitle Title,
    string Preview,
    IReadOnlyList<Tag> Tags,
    bool IsEncrypted,
    bool HasText,
    bool HasFiles,
    bool HasLinks,
    DateTime CreatedAt,
    DateTime UpdatedAt);
