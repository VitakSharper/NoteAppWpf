using System.Globalization;
using System.Text.RegularExpressions;

namespace NoteApp.Services;

// The editor's status line: how many words, and when the note was last saved.
public static partial class NoteStatus
{
    // Letters and digits, with the apostrophes and hyphens inside words ("l'été", "e-mail").
    public static int Words(string? text) => string.IsNullOrWhiteSpace(text) ? 0 : Word().Count(text);

    public static string WordLabel(int words) => words == 1 ? "1 word" : $"{words.ToString("N0", CultureInfo.CurrentCulture)} words";

    // Local time; the day spelled out only when it is not today.
    public static string SavedLabel(DateTime? savedUtc, DateTime nowLocal)
    {
        if (savedUtc is not { } utc)
            return "Not saved yet";

        var local = DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToLocalTime();
        var time = local.ToString("t", CultureInfo.CurrentCulture);
        return (nowLocal.Date - local.Date).Days switch
        {
            0 => $"Saved at {time}",
            1 => $"Saved yesterday at {time}",
            _ when local.Year == nowLocal.Year => $"Saved {local.ToString("d MMM", CultureInfo.CurrentCulture)} at {time}",
            _ => $"Saved {local.ToString("d MMM yyyy", CultureInfo.CurrentCulture)}"
        };
    }

    [GeneratedRegex(@"[\p{L}\p{N}]+(?:['’\-][\p{L}\p{N}]+)*")]
    private static partial Regex Word();
}
