using NoteApp.Services;

namespace NoteApp.Tests.Services;

public class LineShortcutsTests
{
    [Theory]
    [InlineData("# ", LineShortcut.Heading1)]
    [InlineData("## ", LineShortcut.Heading2)]
    [InlineData("### ", LineShortcut.Heading3)]
    [InlineData("- ", LineShortcut.Bullets)]
    [InlineData("* ", LineShortcut.Bullets)]
    [InlineData("1. ", LineShortcut.Numbering)]
    [InlineData("[] ", LineShortcut.Checklist)]
    [InlineData("[ ] ", LineShortcut.Checklist)]
    public void A_line_starter_and_a_space_format_the_line(string line, LineShortcut expected) =>
        Assert.Equal(expected, LineShortcuts.For(line));

    // Only the whole line so far: anything before or after the starter keeps it text.
    [Theory]
    [InlineData("")]
    [InlineData("#")]
    [InlineData("#### ")]
    [InlineData("a # ")]
    [InlineData(" # ")]
    [InlineData("#tag ")]
    [InlineData("2. ")]
    [InlineData("-- ")]
    public void Anything_else_stays_text(string line) =>
        Assert.Equal(LineShortcut.None, LineShortcuts.For(line));

    [Fact]
    public void Heading_starters_give_their_level() =>
        Assert.Equal([1, 2, 3, 0], new[] { LineShortcut.Heading1, LineShortcut.Heading2, LineShortcut.Heading3, LineShortcut.Bullets }
            .Select(LineShortcuts.HeadingLevel));
}
