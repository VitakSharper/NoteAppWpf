using System.Text.Json;
using System.Text.Json.Serialization;
using NoteApp.Domain.Models;

namespace NoteApp.Services;

// A secret block as one JSON string — in the NoteBlocks.SecretJson column and inside the
// encrypted blob alike, the way ChecklistJson does it for checklists. Short keys, empty
// fields left out.
public static class SecretJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string Serialize(NoteBlock.Secret secret) =>
        JsonSerializer.Serialize(new SecretDto
        {
            Label = NullIfEmpty(secret.Label),
            UserName = NullIfEmpty(secret.UserName),
            Password = NullIfEmpty(secret.Password),
            Url = NullIfEmpty(secret.Url)
        }, Options);

    // Unreadable content yields an empty secret rather than breaking the whole note.
    public static NoteBlock.Secret Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new NoteBlock.Secret("", "", "");

        try
        {
            var dto = JsonSerializer.Deserialize<SecretDto>(json, Options) ?? new SecretDto();
            return new NoteBlock.Secret(dto.Label ?? "", dto.UserName ?? "", dto.Password ?? "", dto.Url ?? "");
        }
        catch (JsonException)
        {
            return new NoteBlock.Secret("", "", "");
        }
    }

    private static string? NullIfEmpty(string value) => value.Length == 0 ? null : value;

    private sealed class SecretDto
    {
        [JsonPropertyName("l")] public string? Label { get; init; }
        [JsonPropertyName("u")] public string? UserName { get; init; }
        [JsonPropertyName("p")] public string? Password { get; init; }
        [JsonPropertyName("w")] public string? Url { get; init; }
    }
}
