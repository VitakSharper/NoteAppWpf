namespace NoteApp.Data.Queries;

// The least there is to say about a note: its id and its title.
public sealed class NoteRow
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
}
