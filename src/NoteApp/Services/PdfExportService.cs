using NoteApp.Services.Export;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace NoteApp.Services;

// PDF through QuestPDF. The WPF half — FlowDocument to plain DocElements — is
// shared with the Word export (RichTextDocument, UI thread); everything below
// renders data and has no WPF dependency.
public static class PdfExportService
{
    public static void Export(string noteTitle, IReadOnlyList<string> textBlockContents, string outputPath)
    {
        var elements = RichTextDocument.Extract(textBlockContents.Select(c => new TextExportBlock(c)));

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
                        RenderElements(column, elements);
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

    // Links and attachments never reach the PDF: it is only ever given text blocks.
    private static void RenderElements(ColumnDescriptor column, IReadOnlyList<DocElement> elements)
    {
        foreach (var element in elements)
        {
            switch (element)
            {
                case DocParagraph paragraph:
                    RenderParagraphInlines(column, paragraph.Inlines);
                    break;

                case DocList list:
                    RenderList(column, list);
                    break;
            }
        }
    }

    // Images break the text flow: a paragraph becomes text / image / text items.
    private static void RenderParagraphInlines(ColumnDescriptor column, IReadOnlyList<DocInline> inlines)
    {
        if (inlines.Count == 0)
        {
            column.Item().Text(" ").FontSize(6);
            return;
        }

        var textGroup = new List<DocInline>();

        foreach (var inline in inlines)
        {
            if (inline is DocImage image)
            {
                if (textGroup.Count > 0)
                {
                    RenderTextSegments(column, textGroup);
                    textGroup.Clear();
                }
                column.Item().Image(image.Png);
            }
            else
            {
                textGroup.Add(inline);
            }
        }

        if (textGroup.Count > 0)
            RenderTextSegments(column, textGroup);
    }

    private static void RenderTextSegments(ColumnDescriptor column, List<DocInline> segments)
    {
        column.Item().Text(text =>
        {
            foreach (var segment in segments)
            {
                switch (segment)
                {
                    case DocText t:
                        var span = text.Span(t.Text);
                        if (t.IsBold) span.Bold();
                        if (t.IsItalic) span.Italic();
                        if (t.IsUnderline) span.Underline();
                        break;

                    case DocLineBreak:
                        text.Span("\n");
                        break;
                }
            }
        });
    }

    private static void RenderList(ColumnDescriptor column, DocList list)
    {
        for (var i = 0; i < list.Items.Count; i++)
        {
            var itemInlines = new List<DocInline> { new DocText(DocListMarkers.Text(list.Marker, i), false, false, false) };
            itemInlines.AddRange(list.Items[i]);
            RenderParagraphInlines(column, itemInlines);
        }
    }
}
