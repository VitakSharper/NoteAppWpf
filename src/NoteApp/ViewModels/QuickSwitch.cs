using NoteApp.Domain.ValueObjects;

namespace NoteApp.ViewModels;

// What Ctrl+K offers: notes to go to and commands to run, ranked against what was typed.
public abstract record QuickSwitchEntry(string Label, string Kind);

public sealed record NoteEntry(NoteId Id, string Title, bool IsEncrypted, bool IsArchived)
    : QuickSwitchEntry(Title, IsArchived ? "Archived" : IsEncrypted ? "Encrypted" : "Note");

public sealed record CommandEntry(string Label, Func<Task> Run) : QuickSwitchEntry(Label, "Command");

public static class QuickSwitch
{
    public const int MaxResults = 100;

    // Best first; ties keep the order given (recent notes first, then the commands). Every
    // word typed has to be found in the label: at its start, at a word's start, anywhere, or
    // as letters in that order (the fuzzy match of code editors), in decreasing value.
    public static IReadOnlyList<T> Rank<T>(string query, IEnumerable<T> entries, Func<T, string> labelOf)
    {
        var words = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (words.Length == 0)
            return entries.Take(MaxResults).ToList();

        return entries
            .Select((entry, order) => (entry, order, score: Score(words, query.Trim(), labelOf(entry))))
            .Where(x => x.score > 0)
            .OrderByDescending(x => x.score)
            .ThenBy(x => x.order)
            .Take(MaxResults)
            .Select(x => x.entry)
            .ToList();
    }

    public static int Score(IReadOnlyList<string> words, string whole, string label)
    {
        var total = label.StartsWith(whole, StringComparison.CurrentCultureIgnoreCase) ? 1000 : 0;
        foreach (var word in words)
        {
            var score = WordScore(word, label);
            if (score == 0)
                return 0;
            total += score;
        }

        return total;
    }

    private static int WordScore(string word, string label)
    {
        if (label.StartsWith(word, StringComparison.CurrentCultureIgnoreCase))
            return 400;

        var at = label.IndexOf(word, StringComparison.CurrentCultureIgnoreCase);
        if (at > 0 && !char.IsLetterOrDigit(label[at - 1]))
            return 300;
        if (at > 0)
            return 200;

        var next = 0;
        foreach (var ch in word)
        {
            next = label.IndexOf(ch.ToString(), next, StringComparison.CurrentCultureIgnoreCase) + 1;
            if (next == 0)
                return 0;
        }

        return 100;
    }
}
