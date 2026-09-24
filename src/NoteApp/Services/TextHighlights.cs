namespace NoteApp.Services;

// A line of text cut into pieces, the ones matching a search term marked — what the note list
// highlights in titles and previews. Case-insensitive; overlapping matches merge.
public static class TextHighlights
{
    public static IReadOnlyList<(string Text, bool IsMatch)> Split(string text, IReadOnlyList<string> terms)
    {
        if (text.Length == 0)
            return [];

        var marked = new bool[text.Length];
        foreach (var term in terms.Where(t => t.Length > 0))
        {
            for (var at = text.IndexOf(term, StringComparison.CurrentCultureIgnoreCase); at >= 0;
                 at = text.IndexOf(term, at + 1, StringComparison.CurrentCultureIgnoreCase))
            {
                for (var i = at; i < at + term.Length && i < text.Length; i++)
                    marked[i] = true;
                if (at + 1 >= text.Length)
                    break;
            }
        }

        var pieces = new List<(string, bool)>();
        var start = 0;
        for (var i = 1; i <= text.Length; i++)
        {
            if (i < text.Length && marked[i] == marked[start])
                continue;
            pieces.Add((text[start..i], marked[start]));
            start = i;
        }

        return pieces;
    }
}
