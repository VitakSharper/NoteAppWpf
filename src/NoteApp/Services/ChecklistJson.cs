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
        // Unchecked items are the common case; leaving "d": false out keeps the
        // column small on long lists.
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault
    };

    public static string Serialize(IReadOnlyList<ChecklistItem> items) =>
        JsonSerializer.Serialize(items.Select(i => new ItemDto { Text = i.Text, IsDone = i.IsDone }), Options);

    // Anything unreadable — a hand-edited row, a truncated column — yields an empty
    // checklist rather than breaking the whole note.
    public static IReadOnlyList<ChecklistItem> Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            var dtos = JsonSerializer.Deserialize<List<ItemDto>>(json, Options) ?? [];
            return dtos.Select(d => new ChecklistItem(d.Text ?? string.Empty, d.IsDone)).ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private sealed class ItemDto
    {
        [JsonPropertyName("t")] public string? Text { get; init; }
        [JsonPropertyName("d")] public bool IsDone { get; init; }
    }
}
