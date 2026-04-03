using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace NoteApp;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Icon = CreateAppIcon();
    }

    private static BitmapSource CreateAppIcon()
    {
        const int size = 128;
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            var scale = size / 24.0;

            // Background rounded square
            dc.DrawRoundedRectangle(
                new SolidColorBrush(Color.FromRgb(63, 81, 181)),
                null,
                new Rect(0, 0, size, size),
                size * 0.18, size * 0.18);

            dc.PushTransform(new ScaleTransform(scale, scale));

            // Note paper
            dc.DrawGeometry(Brushes.White, null,
                Geometry.Parse("M5,2 L15,2 L20,7 L20,22 L5,22 Z"));

            // Fold
            dc.DrawGeometry(new SolidColorBrush(Color.FromArgb(120, 63, 81, 181)), null,
                Geometry.Parse("M15,2 L15,7 L20,7 Z"));

            // Text lines
            var lineBrush = new SolidColorBrush(Color.FromArgb(90, 63, 81, 181));
            dc.DrawRoundedRectangle(lineBrush, null, new Rect(7.5, 10, 9, 1.4), 0.7, 0.7);
            dc.DrawRoundedRectangle(lineBrush, null, new Rect(7.5, 13, 6.5, 1.4), 0.7, 0.7);
            dc.DrawRoundedRectangle(lineBrush, null, new Rect(7.5, 16, 8, 1.4), 0.7, 0.7);

            // Pencil
            dc.DrawGeometry(new SolidColorBrush(Color.FromRgb(255, 193, 7)),
                null, Geometry.Parse("M18.5,14 L21.5,11 L22.5,12 L19.5,15 Z"));
            dc.DrawGeometry(new SolidColorBrush(Color.FromRgb(255, 152, 0)),
                null, Geometry.Parse("M18.5,14 L18,15.8 L19.5,15 Z"));

            dc.Pop();
        }

        var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }
}