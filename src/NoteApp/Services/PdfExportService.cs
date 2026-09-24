using System.IO;
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
    public static void Export(string noteTitle, IReadOnlyList<ExportBlock> blocks, string outputPath)
    {
        var elements = RichTextDocument.Extract(blocks);
        using var stream = File.Create(outputPath);
        Render(noteTitle, elements, stream);
    }

    // Plain data in, bytes out — what the tests exercise, like WordExportService.Render.
    public static void Render(string noteTitle, IReadOnlyList<DocElement> elements, Stream output)
    {
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
        }).GeneratePdf(output);
    }

    private static void RenderElements(ColumnDescriptor column, IReadOnlyList<DocElement> elements)
    {
        foreach (var element in elements)
        {
            switch (element)
            {
                case DocParagraph { HeadingLevel: > 0 } heading:
                    column.Item().PaddingTop(6).Text(string.Concat(heading.Inlines.OfType<DocText>().Select(t => t.Text)))
                        .Bold().FontSize(heading.HeadingLevel switch { 1 => 18, 2 => 16, _ => 14 });
                    break;

                case DocParagraph paragraph:
                    RenderParagraphInlines(column, paragraph.Inlines);
                    break;

                case DocList list:
                    RenderList(column, list);
                    break;

                case DocChecklist checklist:
                    RenderChecklist(column, checklist);
                    break;

                case DocSecret secret:
                    RenderSecret(column, secret);
                    break;

                case DocLink link:
                    RenderLink(column, link);
                    break;

                case DocAttachment attachment:
                    column.Item().Text($"Attachment: {attachment.FileName} ({DocAttachment.Size(attachment.SizeBytes)})")
                        .Italic().FontColor(Colors.Grey.Darken1);
                    break;
            }
        }
    }

    // ASCII boxes, not ☐/☑: QuestPDF verifies every glyph against the font and throws
    // on a missing one, and the bundled Lato has no ballot-box characters.
    private static void RenderChecklist(ColumnDescriptor column, DocChecklist checklist)
    {
        foreach (var item in checklist.Items)
        {
            column.Item().PaddingLeft(12).Text(text =>
            {
                Done(text.Span(item.IsDone ? "[x] " : "[ ] "), item.IsDone);

                foreach (var (piece, link) in DocTextLinks.Split(item.Text))
                    Done(link is null ? text.Span(piece) : LinkSpan(text, piece, link), item.IsDone);
            });
        }
    }

    private static void RenderSecret(ColumnDescriptor column, DocSecret secret)
    {
        column.Item().Text(secret.Heading).SemiBold();
        foreach (var (name, value, link) in secret.Fields)
        {
            column.Item().PaddingLeft(12).Text(text =>
            {
                text.Span($"{name}: ").FontColor(Colors.Grey.Darken1);
                if (link is null)
                    text.Span(value);
                else
                    LinkSpan(text, value, link);
            });
        }
    }

    private static void Done(TextSpanDescriptor span, bool isDone)
    {
        if (isDone)
            span.Strikethrough().FontColor(Colors.Grey.Darken1);
    }

    private static TextSpanDescriptor LinkSpan(TextDescriptor text, string label, string url) =>
        text.Hyperlink(label, url).Underline().FontColor(Colors.Blue.Medium);

    private static void RenderLink(ColumnDescriptor column, DocLink link)
    {
        var label = string.IsNullOrWhiteSpace(link.Description) ? link.Url : link.Description;
        column.Item().Hyperlink(link.Url).Text(label).Underline().FontColor(Colors.Blue.Medium);

        // Keep the address readable on paper when the label is a description.
        if (label != link.Url)
            column.Item().Text(link.Url).FontSize(9).FontColor(Colors.Grey.Darken1);
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
                        var span = t.Link is null ? text.Span(t.Text) : LinkSpan(text, t.Text, t.Link);
                        if (t.IsBold) span.Bold();
                        if (t.IsItalic) span.Italic();
                        if (t.IsUnderline) span.Underline();
                        if (t.IsStrike) span.Strikethrough();
                        if (t.IsHighlight) span.BackgroundColor(Colors.Yellow.Lighten3);
                        if (t.IsCode) span.FontFamily(Fonts.Consolas);
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
