using System.IO;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media.Imaging;
using WpfImage = System.Windows.Controls.Image;

namespace NoteApp.Services.Export;

// The WPF half shared by every export: FlowDocument in, plain DocElements out.
// Must run on the UI thread — a FlowDocument is a DispatcherObject.
public static class RichTextDocument
{
    public static IReadOnlyList<DocElement> Extract(IEnumerable<ExportBlock> blocks)
    {
        var elements = new List<DocElement>();
        foreach (var block in blocks)
        {
            switch (block)
            {
                case TextExportBlock { RichTextPayload: var payload } when !string.IsNullOrWhiteSpace(payload):
                    elements.AddRange(Extract(payload));
                    break;

                case LinkExportBlock link:
                    elements.Add(new DocLink(link.Url, link.Description));
                    break;

                case FileExportBlock file:
                    elements.Add(new DocAttachment(file.FileName, file.SizeBytes));
                    break;
            }
        }

        return elements;
    }

    public static IReadOnlyList<DocElement> Extract(string richTextPayload)
    {
        var elements = new List<DocElement>();
        ExtractBlocks(Deserialize(richTextPayload).Blocks, elements);
        return elements;
    }

    // Same three-way fallback as the editor: XamlPackage (Base64), legacy XAML, plain text.
    private static FlowDocument Deserialize(string content)
    {
        var doc = new FlowDocument();

        try
        {
            var bytes = Convert.FromBase64String(content);
            using var ms = new MemoryStream(bytes);
            var range = new TextRange(doc.ContentStart, doc.ContentEnd);
            range.Load(ms, DataFormats.XamlPackage);
            return doc;
        }
        catch { /* Not Base64/XamlPackage */ }

        try
        {
            if (XamlReader.Parse(content) is FlowDocument parsed)
                return parsed;
        }
        catch { /* Not valid XAML either */ }

        doc.Blocks.Clear();
        doc.Blocks.Add(new Paragraph(new Run(content)));
        return doc;
    }

    private static void ExtractBlocks(BlockCollection blocks, List<DocElement> elements)
    {
        foreach (var block in blocks)
        {
            switch (block)
            {
                case Paragraph paragraph:
                    elements.Add(new DocParagraph(ExtractInlines(paragraph.Inlines)));
                    break;

                case List list:
                    var items = new List<IReadOnlyList<DocInline>>();
                    foreach (ListItem item in list.ListItems)
                    {
                        var itemInlines = new List<DocInline>();
                        foreach (var itemBlock in item.Blocks)
                        {
                            if (itemBlock is Paragraph p)
                                itemInlines.AddRange(ExtractInlines(p.Inlines));
                        }
                        items.Add(itemInlines);
                    }
                    elements.Add(new DocList(ToMarker(list.MarkerStyle), items));
                    break;

                case Section section:
                    ExtractBlocks(section.Blocks, elements);
                    break;

                case BlockUIContainer { Child: WpfImage img } when img.Source is BitmapSource bmp:
                    elements.Add(new DocParagraph([new DocImage(ToPng(bmp))]));
                    break;
            }
        }
    }

    private static List<DocInline> ExtractInlines(InlineCollection inlines)
    {
        var result = new List<DocInline>();

        foreach (var inline in inlines)
        {
            switch (inline)
            {
                case Run run when !string.IsNullOrEmpty(run.Text):
                    result.Add(new DocText(
                        run.Text,
                        run.FontWeight == FontWeights.Bold,
                        run.FontStyle == FontStyles.Italic,
                        run.TextDecorations?.Contains(TextDecorations.Underline[0]) == true));
                    break;

                case Span span:
                    result.AddRange(ExtractInlines(span.Inlines));
                    break;

                case InlineUIContainer { Child: WpfImage img } when img.Source is BitmapSource bmp:
                    result.Add(new DocImage(ToPng(bmp)));
                    break;

                case LineBreak:
                    result.Add(new DocLineBreak());
                    break;
            }
        }

        return result;
    }

    private static DocListMarker ToMarker(TextMarkerStyle style) => style switch
    {
        TextMarkerStyle.Decimal => DocListMarker.Decimal,
        TextMarkerStyle.LowerLatin => DocListMarker.LowerLatin,
        TextMarkerStyle.UpperLatin => DocListMarker.UpperLatin,
        _ => DocListMarker.Bullet
    };

    private static byte[] ToPng(BitmapSource source)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(source));
        using var ms = new MemoryStream();
        encoder.Save(ms);
        return ms.ToArray();
    }
}
