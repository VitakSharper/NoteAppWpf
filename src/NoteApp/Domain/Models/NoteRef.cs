using NoteApp.Domain.ValueObjects;

namespace NoteApp.Domain.Models;

// Another note, as far as a link to it needs: where it is and what it is called.
public sealed record NoteRef(NoteId Id, NoteTitle Title);
