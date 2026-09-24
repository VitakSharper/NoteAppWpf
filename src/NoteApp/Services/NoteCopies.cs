namespace NoteApp.Services;

// The title a duplicate gets: "<title> (copy)", cut back to what a title may hold.
public static class NoteCopies
{
    public const string Suffix = " (copy)";
    private const int MaxTitleLength = 500;

    public static string TitleFor(string title)
    {
        var trimmed = title.Trim();
        var room = MaxTitleLength - Suffix.Length;
        return (trimmed.Length > room ? trimmed[..room].TrimEnd() : trimmed) + Suffix;
    }
}
