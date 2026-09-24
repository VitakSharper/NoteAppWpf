using System.IO;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using NoteApp.Services.Export;
using WpfImage = System.Windows.Controls.Image;

namespace NoteApp.UiTests;

// The export entry points start from the stored rich payload, which only decodes on
// a WPF thread — hence here rather than next to the renderer tests.
public class ExportUiTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "NoteApp.UiTests", $"export-{Guid.NewGuid()}");

    [Fact]
    public void Markdown_export_writes_the_text_and_its_images_beside_it() => Wpf.Run(() =>
    {
        Directory.CreateDirectory(_folder);
        var path = Path.Combine(_folder, "My note.md");

        MarkdownExportService.Export("My note", [new TextExportBlock(PayloadWithImage("look https://example.com"))], path);

        var text = File.ReadAllText(path);
        Assert.StartsWith("# My note\n\nlook https://example.com", text);
        Assert.Contains("![](My%20note_files/image1.png)", text);
        Assert.True(File.Exists(Path.Combine(_folder, "My note_files", "image1.png")));
    });

    private static string PayloadWithImage(string text)
    {
        var pixel = BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgra32, null, new byte[4], 4);
        var document = new FlowDocument(new Paragraph(new Run(text)));
        ((Paragraph)document.Blocks.FirstBlock).Inlines.Add(new InlineUIContainer(new WpfImage { Source = pixel }));

        using var stream = new MemoryStream();
        new TextRange(document.ContentStart, document.ContentEnd).Save(stream, DataFormats.XamlPackage);
        return Convert.ToBase64String(stream.ToArray());
    }

    public void Dispose()
    {
        if (Directory.Exists(_folder))
            Directory.Delete(_folder, recursive: true);
    }
}
