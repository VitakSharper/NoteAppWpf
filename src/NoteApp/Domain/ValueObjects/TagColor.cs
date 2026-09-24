namespace NoteApp.Domain.ValueObjects;

// A tag's chip colour, from a fixed palette (Theme/TagPalette) rather than any colour:
// every entry is a background/text pair legible on the light and on the dark theme.
// Violet is the colour every tag had before there was a choice.
public enum TagColor
{
    Violet,
    Blue,
    Teal,
    Green,
    Amber,
    Orange,
    Red,
    Pink,
    Grey
}
