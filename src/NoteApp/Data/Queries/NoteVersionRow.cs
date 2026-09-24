namespace NoteApp.Data.Queries;

// A version as the history lists it; Content only when one version is opened.
public sealed class NoteVersionRow
{
    public Guid Id { get; init; }
    public DateTime SavedAt { get; init; }
    public string Title { get; init; } = string.Empty;
    public bool IsEncrypted { get; init; }
    public long SizeBytes { get; init; }
    public byte[]? Content { get; init; }
}
