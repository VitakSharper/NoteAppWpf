namespace NoteApp.Data.Entities;

public class TagEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;

    public ICollection<NoteTagEntity> NoteTags { get; set; } = [];
}
