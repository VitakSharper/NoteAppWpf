using System.Text.Json;
using System.Text.Json.Serialization;
using NoteApp.Domain.Models;

namespace NoteApp.Services;

// Checklist items are stored as one JSON string, in the NoteBlocks.ChecklistJson
// column and inside the encrypted blob alike — one format, one place to change.
// Not a child table: saving a note already replaces all of its blocks wholesale,
// so items in their own table would only add joins and cascade rules.
public static class ChecklistJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        // Unchecked items are the common case; leaving "d": false (and "p": false) out
        // keeps the column small on long lists.
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault
    };

    public static string Serialize(IReadOnlyList<ChecklistItem> items) =>
        JsonSerializer.Serialize(items.Select(i => new ItemDto
        {
            Text = i.Text,
            Note = i.Note.Length > 0 ? i.Note : null,
            IsDone = i.State == ChecklistItemState.Done,
            InProgress = i.State == ChecklistItemState.InProgress,
            Due = i.Due
        }), Options);

    // Anything unreadable — a hand-edited row, a truncated column — yields an empty
    // checklist rather than breaking the whole note.
    public static IReadOnlyList<ChecklistItem> Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            var dtos = JsonSerializer.Deserialize<List<ItemDto>>(json, Options) ?? [];
            return dtos.Select(d => new ChecklistItem(d.Text ?? string.Empty, StateOf(d), d.Due is { } due ? DateTime.SpecifyKind(due, DateTimeKind.Utc) : null, d.Note ?? string.Empty)).ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    // Done wins should a hand-edited row claim both.
    private static ChecklistItemState StateOf(ItemDto dto) =>
        dto.IsDone ? ChecklistItemState.Done : dto.InProgress ? ChecklistItemState.InProgress : ChecklistItemState.Todo;

    private sealed class ItemDto
    {
        [JsonPropertyName("t")] public string? Text { get; init; }
        // The item's note; left out when there is none.
        [JsonPropertyName("n")] public string? Note { get; init; }
        [JsonPropertyName("d")] public bool IsDone { get; init; }
        // Started, not finished. A key of its own rather than a state number, so the rows
        // written before it read unchanged — and an older release reads it as not done.
        [JsonPropertyName("p")] public bool InProgress { get; init; }
        // A reminder, UTC; left out when there is none.
        [JsonPropertyName("r")] public DateTime? Due { get; init; }
    }

    // What the reminder query looks for in the column before parsing it.
    public const string DueMarker = "\"r\":";
}
