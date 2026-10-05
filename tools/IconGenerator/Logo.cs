using System.Windows;
using System.Windows.Media;

namespace IconGenerator;

// NoteApp's logo: a white rounded N on a violet-to-blue tile with a folded top-right corner.
// Drawn in a 256-unit box that Program scales to each icon size — the design lives here.
internal static class Logo
{
    public const double Box = 256;

    private static readonly Rect Tile = new(8, 8, 240, 240);
    private const double TileRadius = 56;

    public static void Draw(DrawingContext dc, int size)
    {
        var tile = new RectangleGeometry(Tile, TileRadius, TileRadius);
        dc.DrawGeometry(new LinearGradientBrush(Hex("#8A5CFF"), Hex("#3F7BFF"), 45), null, tile);

        // The folded corner: the flap, then the paler fold over it, both kept inside the tile.
        dc.PushClip(tile);
        dc.DrawGeometry(new SolidColorBrush(Color.FromArgb(90, 255, 255, 255)), null, Geometry.Parse("M 188,8 L 248,8 L 248,68 Z"));
        dc.DrawGeometry(new SolidColorBrush(Color.FromArgb(160, 255, 255, 255)), null, Geometry.Parse("M 188,8 L 188,58 Q 188,68 198,68 L 248,68 Z"));
        dc.Pop();

        // Bolder under 32 px, where 34 units would make a stroke of barely two pixels.
        var pen = new Pen(Brushes.White, size >= 32 ? 34 : 40)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };
        dc.DrawGeometry(null, pen, Geometry.Parse("M 84,184 L 84,76 L 172,184 L 172,76"));
    }

    private static Color Hex(string hex) => (Color)ColorConverter.ConvertFromString(hex);
}
