namespace NoteApp.Services;

public enum LineShortcut
{
    None,
    Heading1,
    Heading2,
    Heading3,
    Bullets,
    Numbering,
    Checklist
}

// Markdown's line starters, the way Markdown-aware editors take them: typed at the very
// start of a line of a text block and followed by a space, they format the line instead
// of staying as text. lineUpToCaret is the line's text up to the caret, space included.
public static class LineShortcuts
{
    public static LineShortcut For(string lineUpToCaret) => lineUpToCaret switch
    {
        "# " => LineShortcut.Heading1,
        "## " => LineShortcut.Heading2,
        "### " => LineShortcut.Heading3,
        "- " or "* " => LineShortcut.Bullets,
        "1. " => LineShortcut.Numbering,
        "[] " or "[ ] " => LineShortcut.Checklist,
        _ => LineShortcut.None
    };

    public static int HeadingLevel(LineShortcut shortcut) => shortcut switch
    {
        LineShortcut.Heading1 => 1,
        LineShortcut.Heading2 => 2,
        LineShortcut.Heading3 => 3,
        _ => 0
    };
}
