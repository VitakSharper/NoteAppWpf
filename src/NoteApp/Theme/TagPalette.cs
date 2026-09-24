using System.Windows.Media;
using NoteApp.Domain.ValueObjects;

namespace NoteApp.Theme;

public sealed record TagSwatch(TagColor Color, SolidColorBrush Background, SolidColorBrush Foreground, SolidColorBrush Border)
{
    public string Name => Color.ToString();
}

// Static pastel chips with dark text, the way the violet one always was: they read the
// same on the light and the dark card surfaces, so they do not follow the theme.
public static class TagPalette
{
    public static IReadOnlyList<TagSwatch> All { get; } =
    [
        Swatch(TagColor.Violet, "#E6E8FF", "#3A3F8F", "#C5CAF5"),
        Swatch(TagColor.Blue, "#DCEBFF", "#1D4E89", "#B5D3F7"),
        Swatch(TagColor.Teal, "#D5F3EF", "#0F5C55", "#A8E3DA"),
        Swatch(TagColor.Green, "#DDF3DC", "#2E6B2A", "#B9E2B6"),
        Swatch(TagColor.Amber, "#FFF0CC", "#7A5200", "#F2D68A"),
        Swatch(TagColor.Orange, "#FFE3D1", "#8A3B0B", "#F5C2A1"),
        Swatch(TagColor.Red, "#FDDCDC", "#8E1F1F", "#F2B3B3"),
        Swatch(TagColor.Pink, "#FBDDF0", "#83205F", "#F0B5D9"),
        Swatch(TagColor.Grey, "#E8E9EC", "#3F4550", "#CDD0D6")
    ];

    public static TagSwatch For(TagColor color) => All.FirstOrDefault(s => s.Color == color) ?? All[0];

    private static TagSwatch Swatch(TagColor color, string background, string foreground, string border) =>
        new(color, Brush(background), Brush(foreground), Brush(border));

    private static SolidColorBrush Brush(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }
}
