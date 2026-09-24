using System.Text;

namespace NoteApp.Domain.Models;

// The search box, read the way search boxes are: words and "quoted phrases" that must all
// match, and a few operators — tag:work (tag:"two words"), has:file / link / checklist /
// code / secret / text, is:pinned, is:encrypted. An operator it does not know is just text.
public sealed record SearchQuery(
    IReadOnlyList<string> Terms,
    IReadOnlyList<string> TagNames,
    IReadOnlyList<BlockType> Has,
    bool PinnedOnly,
    bool EncryptedOnly)
{
    public static readonly SearchQuery Empty = new([], [], [], false, false);

    public bool IsEmpty => Terms.Count == 0 && TagNames.Count == 0 && Has.Count == 0 && !PinnedOnly && !EncryptedOnly;

    public static SearchQuery Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Empty;

        var terms = new List<string>();
        var tags = new List<string>();
        var has = new List<BlockType>();
        var pinned = false;
        var encrypted = false;

        foreach (var token in Tokens(text))
        {
            var colon = token.Quoted ? -1 : token.Text.IndexOf(':');
            var key = colon > 0 ? token.Text[..colon].ToLowerInvariant() : null;
            var value = colon > 0 ? token.Text[(colon + 1)..] : null;

            switch (key)
            {
                case "tag" when value is { Length: > 0 }:
                    tags.Add(value);
                    continue;
                case "has" when BlockTypeOf(value) is { } type:
                    if (!has.Contains(type))
                        has.Add(type);
                    continue;
                case "is" when value?.ToLowerInvariant() == "pinned":
                    pinned = true;
                    continue;
                case "is" when value?.ToLowerInvariant() is "encrypted" or "locked":
                    encrypted = true;
                    continue;
            }

            terms.Add(token.Text);
        }

        return new SearchQuery(terms, tags, has, pinned, encrypted);
    }

    private static BlockType? BlockTypeOf(string? value) => value?.ToLowerInvariant() switch
    {
        "text" => BlockType.Text,
        "file" or "files" or "attachment" or "attachments" => BlockType.File,
        "link" or "links" => BlockType.Link,
        "checklist" or "checklists" or "task" or "tasks" or "todo" => BlockType.Checklist,
        "secret" or "secrets" or "password" or "passwords" => BlockType.Secret,
        "code" => BlockType.Code,
        _ => null
    };

    // Whitespace separates tokens; double quotes group a phrase, also as an operator's value
    // (tag:"two words"). An unclosed quote runs to the end.
    private static IEnumerable<(string Text, bool Quoted)> Tokens(string text)
    {
        var current = new StringBuilder();
        var inQuotes = false;
        var quoted = false;

        foreach (var ch in text)
        {
            if (ch == '"')
            {
                inQuotes = !inQuotes;
                quoted |= current.Length == 0;
                continue;
            }

            if (char.IsWhiteSpace(ch) && !inQuotes)
            {
                if (current.Length > 0)
                    yield return (current.ToString(), quoted);
                current.Clear();
                quoted = false;
                continue;
            }

            current.Append(ch);
        }

        if (current.Length > 0)
            yield return (current.ToString().Trim(), quoted);
    }
}
