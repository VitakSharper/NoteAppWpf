namespace NoteApp.Data.Entities;

public class TagEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    // A TagColor name; null for tags created before colours existed, which are violet.
    public string? Color { get; set; }

    public ICollection<NoteTagEntity> NoteTags { get; set; } = [];
}
