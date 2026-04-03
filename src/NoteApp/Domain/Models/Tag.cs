using NoteApp.Domain.ValueObjects;

namespace NoteApp.Domain.Models;

public sealed record Tag(Guid Id, TagName Name)
{
    public static Tag Create(TagName name) => new(Guid.NewGuid(), name);
}
