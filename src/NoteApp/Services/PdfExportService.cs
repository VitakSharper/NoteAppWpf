using System.IO;
using System.Windows;
using WpfImage = System.Windows.Controls.Image;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media.Imaging;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace NoteApp.Services;

public static class PdfExportService
{
    public static void Export(string noteTitle, IReadOnlyList<string> textBlockContents, string outputPath)
    {
        // Step 1: Extract content from FlowDocuments (must run on UI thread)
        var extractedBlocks = textBlockContents
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(DeserializeContent)
            .SelectMany(ExtractElements)
            .ToList();

        // Step 2: Generate PDF (no WPF dependencies from here)
        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(12));

                page.Header()
                    .PaddingBottom(15)
                    .BorderBottom(1)
                    .BorderColor(Colors.Grey.Medium)
                    .Text(noteTitle)
                    .SemiBold()
                    .FontSize(22)
                    .FontColor(Colors.Blue.Darken2);

                page.Content()
                    .PaddingVertical(10)
                    .Column(column =>
                    {
                        column.Spacing(4);
                        RenderElements(column, extractedBlocks);
                    });

                page.Footer()
                    .AlignCenter()
                    .Text(text =>
                    {
                        text.DefaultTextStyle(x => x.FontSize(9).FontColor(Colors.Grey.Medium));
                        text.Span("Page ");
                        text.CurrentPageNumber();
                        text.Span(" of ");
                        text.TotalPages();
                    });
            });
        }).GeneratePdf(outputPath);
    }

    // --- Content extraction (WPF-dependent, must run on UI thread) ---

    private static FlowDocument DeserializeContent(string content)
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
        catch { /* Not valid XAML */ }

        doc.Blocks.Clear();
        doc.Blocks.Add(new Paragraph(new Run(content)));
        return doc;
    }

    private static List<PdfElement> ExtractElements(FlowDocument doc)
    {
        var elements = new List<PdfElement>();
        ExtractBlockElements(doc.Blocks, elements);
        return elements;
    }

    private static void ExtractBlockElements(BlockCollection blocks, List<PdfElement> elements)
    {
        foreach (var block in blocks)
        {
            switch (block)
            {
                case Paragraph paragraph:
                    elements.Add(new PdfParagraph(ExtractInlines(paragraph.Inlines)));
                    break;

                case List list:
                    var items = new List<List<PdfInline>>();
                    foreach (ListItem item in list.ListItems)
                    {
                        var itemInlines = new List<PdfInline>();
                        foreach (var itemBlock in item.Blocks)
                        {
                            if (itemBlock is Paragraph p)
                                itemInlines.AddRange(ExtractInlines(p.Inlines));
                        }
                        items.Add(itemInlines);
                    }
                    elements.Add(new PdfListElement(list.MarkerStyle, items));
                    break;

                case Section section:
                    ExtractBlockElements(section.Blocks, elements);
                    break;

                case BlockUIContainer { Child: WpfImage img } when img.Source is BitmapSource bmp:
                    elements.Add(new PdfParagraph([new PdfImageInline(BitmapSourceToBytes(bmp))]));
                    break;
            }
        }
    }

    private static List<PdfInline> ExtractInlines(InlineCollection inlines)
    {
        var result = new List<PdfInline>();

        foreach (var inline in inlines)
        {
            switch (inline)
            {
                case Run run when !string.IsNullOrEmpty(run.Text):
                    result.Add(new PdfText(
                        run.Text,
                        run.FontWeight == FontWeights.Bold,
                        run.FontStyle == FontStyles.Italic,
                        run.TextDecorations?.Contains(TextDecorations.Underline[0]) == true));
                    break;

                case Span span:
                    result.AddRange(ExtractInlines(span.Inlines));
                    break;

                case InlineUIContainer { Child: WpfImage img } when img.Source is BitmapSource bmp:
                    result.Add(new PdfImageInline(BitmapSourceToBytes(bmp)));
                    break;

                case LineBreak:
                    result.Add(new PdfLineBreakInline());
                    break;
            }
        }

        return result;
    }

    private static byte[] BitmapSourceToBytes(BitmapSource source)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(source));
        using var ms = new MemoryStream();
        encoder.Save(ms);
        return ms.ToArray();
    }

    // --- PDF rendering (no WPF dependencies) ---

    private static void RenderElements(ColumnDescriptor column, List<PdfElement> elements)
    {
        foreach (var element in elements)
        {
            switch (element)
            {
                case PdfParagraph para:
                    RenderParagraphInlines(column, para.Inlines);
                    break;

                case PdfListElement list:
                    RenderListElement(column, list);
                    break;
            }
        }
    }

    private static void RenderParagraphInlines(ColumnDescriptor column, List<PdfInline> inlines)
    {
        if (inlines.Count == 0)
        {
            column.Item().Text(" ").FontSize(6);
            return;
        }

        var currentTextGroup = new List<PdfInline>();

        foreach (var inline in inlines)
        {
            if (inline is PdfImageInline imgInline)
            {
                if (currentTextGroup.Count > 0)
                {
                    RenderTextSegments(column, currentTextGroup);
                    currentTextGroup.Clear();
                }
                column.Item().Image(imgInline.Data);
            }
            else
            {
                currentTextGroup.Add(inline);
            }
        }

        if (currentTextGroup.Count > 0)
            RenderTextSegments(column, currentTextGroup);
    }

    private static void RenderTextSegments(ColumnDescriptor column, List<PdfInline> segments)
    {
        column.Item().Text(text =>
        {
            foreach (var seg in segments)
            {
                switch (seg)
                {
                    case PdfText t:
                        var span = text.Span(t.Content);
                        if (t.IsBold) span.Bold();
                        if (t.IsItalic) span.Italic();
                        if (t.IsUnderline) span.Underline();
                        break;

                    case PdfLineBreakInline:
                        text.Span("\n");
                        break;
                }
            }
        });
    }

    private static void RenderListElement(ColumnDescriptor column, PdfListElement list)
    {
        for (var i = 0; i < list.Items.Count; i++)
        {
            var marker = list.MarkerStyle switch
            {
                TextMarkerStyle.Decimal => $"{i + 1}. ",
                TextMarkerStyle.LowerLatin => $"{(char)('a' + i)}. ",
                TextMarkerStyle.UpperLatin => $"{(char)('A' + i)}. ",
                _ => "\u2022 "
            };

            var itemInlines = new List<PdfInline>();
            itemInlines.Add(new PdfText(marker, false, false, false));
            itemInlines.AddRange(list.Items[i]);
            RenderParagraphInlines(column, itemInlines);
        }
    }

    // --- Data types ---

    private abstract record PdfElement;
    private sealed record PdfParagraph(List<PdfInline> Inlines) : PdfElement;
    private sealed record PdfListElement(TextMarkerStyle MarkerStyle, List<List<PdfInline>> Items) : PdfElement;

    private abstract record PdfInline;
    private sealed record PdfText(string Content, bool IsBold, bool IsItalic, bool IsUnderline) : PdfInline;
    private sealed record PdfImageInline(byte[] Data) : PdfInline;
    private sealed record PdfLineBreakInline() : PdfInline;
}
