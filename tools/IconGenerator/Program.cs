using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace IconGenerator;

// dotnet run --project tools/IconGenerator [-- [<output.ico>] [--preview <sheet.png>]]
// Without an output path it replaces src/NoteApp/Resources/app.ico of the repository it runs in.
// --preview also draws every size at its real pixel count, on a light and on a dark background.
//
// The sizes are the ones Windows asks for: 16 to 48 for lists, the taskbar and the tray, 64 and
// 256 for Explorer's large views. All are 32-bit with alpha; up to 48 they are plain DIBs, which
// every reader takes, 64 and 256 are PNG, as the format has allowed since Vista.
internal static class Program
{
    private static readonly int[] Sizes = [16, 20, 24, 32, 40, 48, 64, 256];

    [STAThread]
    private static int Main(string[] args)
    {
        string? output = null, preview = null;
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "--preview" && i + 1 < args.Length)
                preview = args[++i];
            else
                output = args[i];
        }

        output ??= DefaultOutput();
        if (output is null)
        {
            Console.Error.WriteLine("NoteApp.slnx not found above the current folder: give the .ico path.");
            return 1;
        }

        var frames = Sizes.Select(size => (Size: size, Bitmap: Render(size))).ToList();
        WriteIco(output, frames);
        Console.WriteLine($"{Path.GetFullPath(output)}: {string.Join(", ", Sizes)} px");

        if (preview is not null)
        {
            WritePreview(preview, frames);
            Console.WriteLine($"{Path.GetFullPath(preview)}: preview");
        }

        return 0;
    }

    private static string? DefaultOutput()
    {
        for (var folder = new DirectoryInfo(Directory.GetCurrentDirectory()); folder is not null; folder = folder.Parent)
        {
            if (File.Exists(Path.Combine(folder.FullName, "NoteApp.slnx")))
                return Path.Combine(folder.FullName, "src", "NoteApp", "Resources", "app.ico");
        }

        return null;
    }

    private static BitmapSource Render(int size)
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.PushTransform(new ScaleTransform(size / Logo.Box, size / Logo.Box));
            Logo.Draw(dc, size);
            dc.Pop();
        }

        var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        return bitmap;
    }

    // ICONDIR, one ICONDIRENTRY per size (0 stands for 256), then the images in the same order.
    private static void WriteIco(string path, IReadOnlyList<(int Size, BitmapSource Bitmap)> frames)
    {
        var images = frames.Select(f => (f.Size, Data: f.Size >= 64 ? Png(f.Bitmap) : Dib(f.Bitmap, f.Size))).ToList();

        using var file = File.Create(path);
        using var writer = new BinaryWriter(file);
        writer.Write((short)0);
        writer.Write((short)1);
        writer.Write((short)images.Count);

        var offset = 6 + 16 * images.Count;
        foreach (var (size, data) in images)
        {
            writer.Write((byte)(size >= 256 ? 0 : size));
            writer.Write((byte)(size >= 256 ? 0 : size));
            writer.Write((byte)0); // no palette
            writer.Write((byte)0);
            writer.Write((short)1); // planes
            writer.Write((short)32); // bits per pixel
            writer.Write(data.Length);
            writer.Write(offset);
            offset += data.Length;
        }

        foreach (var (_, data) in images)
            writer.Write(data);
    }

    private static byte[] Png(BitmapSource bitmap)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }

    // BITMAPINFOHEADER with the height doubled (image + mask), BGRA rows bottom-up, then an
    // all-clear AND mask: with 32 bits the alpha channel decides.
    private static byte[] Dib(BitmapSource bitmap, int size)
    {
        var stride = size * 4;
        var pixels = new byte[stride * size];
        new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0).CopyPixels(pixels, stride, 0);
        var maskStride = (size + 31) / 32 * 4;

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(40);
        writer.Write(size);
        writer.Write(size * 2);
        writer.Write((short)1);
        writer.Write((short)32);
        writer.Write(0); // BI_RGB
        writer.Write(stride * size + maskStride * size);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);
        for (var y = size - 1; y >= 0; y--)
            writer.Write(pixels, y * stride, stride);
        writer.Write(new byte[maskStride * size]);
        return stream.ToArray();
    }

    private static void WritePreview(string path, IReadOnlyList<(int Size, BitmapSource Bitmap)> frames)
    {
        const int gap = 16;
        var width = frames.Sum(f => f.Size) + gap * (frames.Count + 1);
        const int band = 256 + 2 * gap;

        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            var top = 0;
            foreach (var background in new[] { Color.FromRgb(0xF3, 0xF3, 0xF3), Color.FromRgb(0x1F, 0x1F, 0x1F) })
            {
                dc.DrawRectangle(new SolidColorBrush(background), null, new Rect(0, top, width, band));
                var x = gap;
                foreach (var (size, bitmap) in frames)
                {
                    dc.DrawImage(bitmap, new Rect(x, top + gap + 256 - size, size, size));
                    x += size + gap;
                }
                top += band;
            }
        }

        var sheet = new RenderTargetBitmap(width, 2 * band, 96, 96, PixelFormats.Pbgra32);
        sheet.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(sheet));
        using var file = File.Create(path);
        encoder.Save(file);
    }
}
