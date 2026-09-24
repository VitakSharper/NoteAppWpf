namespace NoteApp.Data.Queries;

// A note with something to remind of: its own RemindAt, or checklists whose items may carry
// a due time (ChecklistJson, parsed by the service).
public sealed class ReminderRow
{
    public Guid NoteId { get; init; }
    public string Title { get; init; } = string.Empty;
    public DateTime? RemindAt { get; init; }
    public List<string> ChecklistJson { get; init; } = [];
}
