using System.Globalization;

namespace NoteApp.Services;

// What a quick note is called when nothing was typed in its title: its first line, or when.
public static class QuickNotes
{
    public const int MaxTitleFromText = 80;

    public static string TitleFor(string title, string text, DateTime now)
    {
        if (!string.IsNullOrWhiteSpace(title))
            return title.Trim();

        var firstLine = text.ReplaceLineEndings("\n").Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0);
        if (firstLine is null)
            return $"Quick note {now.ToString("g", CultureInfo.CurrentCulture)}";

        return firstLine.Length <= MaxTitleFromText ? firstLine : firstLine[..MaxTitleFromText].TrimEnd() + "…";
    }
}
