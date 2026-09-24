using NoteApp.Domain.ValueObjects;

namespace NoteApp.Domain.Models;

// Something to be reminded of: a whole note (ItemText null) or one checklist item, at DueUtc.
public sealed record Reminder(NoteId NoteId, string NoteTitle, string? ItemText, DateTime DueUtc)
{
    public bool IsForItem => ItemText is not null;
    public string Label => ItemText is null ? NoteTitle : $"{ItemText} — {NoteTitle}";
}
