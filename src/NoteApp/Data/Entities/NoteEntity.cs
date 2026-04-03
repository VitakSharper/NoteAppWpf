using NoteApp.Domain.Models;

namespace NoteApp.Data.Entities;

public class NoteEntity
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public bool IsEncrypted { get; set; }
    public byte[]? EncryptedContent { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<NoteBlockEntity> Blocks { get; set; } = [];
    public ICollection<NoteTagEntity> NoteTags { get; set; } = [];
}
