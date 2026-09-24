namespace NoteApp.Domain.Models;

// An earlier state of a note, as the history window lists it.
public sealed record NoteVersion(Guid Id, DateTime SavedAt, string Title, bool IsEncrypted, long SizeBytes);

// An earlier state opened: the title and the blocks it had.
public sealed record NoteVersionContent(NoteVersion Version, string Title, IReadOnlyList<NoteBlock> Blocks);
