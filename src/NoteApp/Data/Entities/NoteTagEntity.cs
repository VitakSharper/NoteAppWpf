namespace NoteApp.Data.Entities;

public class NoteTagEntity
{
    public Guid NoteId { get; set; }
    public NoteEntity Note { get; set; } = null!;

    public Guid TagId { get; set; }
    public TagEntity Tag { get; set; } = null!;
}
