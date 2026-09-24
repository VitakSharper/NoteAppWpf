namespace NoteApp.Data.Entities;

// What a note held before a save replaced it. Content is the blocks as JSON (the format of
// the encrypted payload, EncryptionService.BlocksToJson) for a plain note, and the encrypted
// payload itself for an encrypted one — a version is never stored more readable than the note.
public class NoteVersionEntity
{
    public Guid Id { get; set; }
    public Guid NoteId { get; set; }
    // When this content was saved (the note's UpdatedAt at the time), not when it was replaced.
    public DateTime SavedAt { get; set; }
    public string Title { get; set; } = string.Empty;
    public bool IsEncrypted { get; set; }
    public byte[] Content { get; set; } = [];
    public long SizeBytes { get; set; }
}
