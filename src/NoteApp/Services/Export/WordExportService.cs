using System.IO;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;

namespace NoteApp.Services.Export;

// .docx through the Open XML SDK. No styles part: the formatting is written on the
// runs themselves, so the file looks the same in Word, LibreOffice and Google Docs.
// Export runs the WPF extraction (UI thread); Render is plain data in, bytes out,
// which is what the tests exercise.
public static class WordExportService
{
    // A4 with 2 cm margins, in twips — the page the PDF export uses.
    private const uint PageWidthTwips = 11906;
    private const uint PageHeightTwips = 16838;
    private const uint MarginTwips = 1134;

    private const long EmuPerTwip = 635;
    private const long EmuPerPixel = 9525; // 96 dpi
    private const long TextWidthEmu = (PageWidthTwips - 2 * MarginTwips) * EmuPerTwip;

    public static void Export(string noteTitle, IReadOnlyList<ExportBlock> blocks, string outputPath)
    {
        var elements = RichTextDocument.Extract(blocks);
        using var stream = File.Create(outputPath);
        Render(noteTitle, elements, stream);
    }

    public static void Render(string noteTitle, IReadOnlyList<DocElement> elements, Stream output)
    {
        using var document = WordprocessingDocument.Create(output, WordprocessingDocumentType.Document);
        var main = document.AddMainDocumentPart();
        var body = new Body();
        main.Document = new Document(body);

        body.Append(TitleParagraph(noteTitle));

        var images = 0;
        foreach (var element in elements)
        {
            switch (element)
            {
                case DocParagraph paragraph:
                    body.Append(BuildParagraph(paragraph.Inlines, main, ref images, indented: false));
                    break;

                case DocList list:
                    for (var i = 0; i < list.Items.Count; i++)
                    {
                        var inlines = new List<DocInline> { new DocText(DocListMarkers.Text(list.Marker, i), false, false, false) };
                        inlines.AddRange(list.Items[i]);
                        body.Append(BuildParagraph(inlines, main, ref images, indented: true));
                    }
                    break;

                case DocLink link:
                    body.Append(LinkParagraph(link, main));
                    break;

                case DocAttachment attachment:
                    body.Append(AttachmentParagraph(attachment));
                    break;

                case DocChecklist checklist:
                    foreach (var item in checklist.Items)
                        body.Append(ChecklistParagraph(item));
                    break;
            }
        }

        body.Append(new SectionProperties(
            new PageSize { Width = PageWidthTwips, Height = PageHeightTwips },
            new PageMargin
            {
                Top = (int)MarginTwips, Bottom = (int)MarginTwips,
                Left = MarginTwips, Right = MarginTwips,
                Header = 708U, Footer = 708U, Gutter = 0U
            }));
    }

    private static Paragraph TitleParagraph(string title) =>
        new(new ParagraphProperties(new SpacingBetweenLines { After = "240" }),
            new Run(
                new RunProperties(new Bold(), new Color { Val = "1F3A93" }, new FontSize { Val = "40" }), // half-points: 20 pt
                PreservedText(title)));

    private static Paragraph BuildParagraph(IReadOnlyList<DocInline> inlines, MainDocumentPart main, ref int images, bool indented)
    {
        var paragraph = new Paragraph();
        if (indented)
            paragraph.Append(new ParagraphProperties(new Indentation { Left = "720" }));

        foreach (var inline in inlines)
        {
            switch (inline)
            {
                case DocText text:
                    paragraph.Append(TextRun(text));
                    break;

                case DocLineBreak:
                    paragraph.Append(new Run(new Break()));
                    break;

                case DocImage image:
                    images++;
                    paragraph.Append(new Run(ImageDrawing(main, image.Png, images)));
                    break;
            }
        }

        return paragraph;
    }

    private static Run TextRun(DocText text)
    {
        var properties = new RunProperties();
        if (text.IsBold) properties.Append(new Bold());
        if (text.IsItalic) properties.Append(new Italic());
        if (text.IsUnderline) properties.Append(new Underline { Val = UnderlineValues.Single });

        var run = new Run();
        if (properties.HasChildren)
            run.Append(properties);
        run.Append(PreservedText(text.Text));
        return run;
    }

    private static Paragraph LinkParagraph(DocLink link, MainDocumentPart main)
    {
        var paragraph = new Paragraph();

        // The editor validates URLs on save, but an old row could still hold anything:
        // a value Word could not follow is written as plain text rather than refused.
        if (!Uri.TryCreate(link.Url, UriKind.Absolute, out var uri))
        {
            paragraph.Append(TextRun(new DocText(link.Url, false, false, false)));
            return paragraph;
        }

        var relationship = main.AddHyperlinkRelationship(uri, isExternal: true);
        var label = string.IsNullOrWhiteSpace(link.Description) ? link.Url : link.Description;

        paragraph.Append(new Hyperlink(
            new Run(
                new RunProperties(new Color { Val = "0563C1" }, new Underline { Val = UnderlineValues.Single }),
                PreservedText(label)))
        { Id = relationship.Id });

        // Keep the address readable on paper when the label is a description.
        if (label != link.Url)
            paragraph.Append(new Run(new RunProperties(new Color { Val = "808080" }), PreservedText($"  {link.Url}")));

        return paragraph;
    }

    // Real Word checkboxes are content controls (or a legacy form field): a ballot-box
    // character reads the same everywhere and survives a copy-paste into any editor.
    // A ticked item is struck through, the way the editor shows it.
    private static Paragraph ChecklistParagraph(DocChecklistItem item)
    {
        var properties = new RunProperties();
        if (item.IsDone)
        {
            properties.Append(new Strike());
            properties.Append(new Color { Val = "595959" });
        }

        var run = new Run();
        if (properties.HasChildren)
            run.Append(properties);
        run.Append(PreservedText($"{(item.IsDone ? "☑" : "☐")} {item.Text}"));

        return new Paragraph(new ParagraphProperties(new Indentation { Left = "360" }), run);
    }

    private static Paragraph AttachmentParagraph(DocAttachment attachment) =>
        new(new Run(
            new RunProperties(new Italic(), new Color { Val = "595959" }),
            PreservedText($"Attachment: {attachment.FileName} ({FormatSize(attachment.SizeBytes)})")));

    // Leading and trailing spaces are dropped by Word unless the text says otherwise.
    private static Text PreservedText(string value) => new(value) { Space = SpaceProcessingModeValues.Preserve };

    private static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:0.#} KB",
        _ => $"{bytes / (1024.0 * 1024.0):0.#} MB"
    };

    // The inline-picture markup from the SDK documentation, sized from the PNG header.
    private static Drawing ImageDrawing(MainDocumentPart main, byte[] png, int index)
    {
        var part = main.AddImagePart(ImagePartType.Png);
        using (var stream = new MemoryStream(png))
            part.FeedData(stream);
        var relationshipId = main.GetIdOfPart(part);

        var (cx, cy) = ImageExtent(png);
        var id = (uint)index;

        return new Drawing(
            new DW.Inline(
                new DW.Extent { Cx = cx, Cy = cy },
                new DW.EffectExtent { LeftEdge = 0L, TopEdge = 0L, RightEdge = 0L, BottomEdge = 0L },
                new DW.DocProperties { Id = id, Name = $"Picture {index}" },
                new DW.NonVisualGraphicFrameDrawingProperties(new A.GraphicFrameLocks { NoChangeAspect = true }),
                new A.Graphic(
                    new A.GraphicData(
                        new PIC.Picture(
                            new PIC.NonVisualPictureProperties(
                                new PIC.NonVisualDrawingProperties { Id = 0U, Name = $"image{index}.png" },
                                new PIC.NonVisualPictureDrawingProperties()),
                            new PIC.BlipFill(
                                new A.Blip { Embed = relationshipId, CompressionState = A.BlipCompressionValues.Print },
                                new A.Stretch(new A.FillRectangle())),
                            new PIC.ShapeProperties(
                                new A.Transform2D(new A.Offset { X = 0L, Y = 0L }, new A.Extents { Cx = cx, Cy = cy }),
                                new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle })))
                    { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" }))
            { DistanceFromTop = 0U, DistanceFromBottom = 0U, DistanceFromLeft = 0U, DistanceFromRight = 0U });
    }

    // PNG IHDR: width then height, big-endian, at bytes 16-23. Pixels at 96 dpi become
    // EMUs, and anything wider than the text column is scaled down to fit it.
    private static (long Cx, long Cy) ImageExtent(byte[] png)
    {
        var fallback = (TextWidthEmu / 2, TextWidthEmu / 2);
        if (png.Length < 24)
            return fallback;

        var width = ((long)png[16] << 24) | ((long)png[17] << 16) | ((long)png[18] << 8) | png[19];
        var height = ((long)png[20] << 24) | ((long)png[21] << 16) | ((long)png[22] << 8) | png[23];
        if (width <= 0 || height <= 0)
            return fallback;

        var cx = width * EmuPerPixel;
        var cy = height * EmuPerPixel;
        if (cx > TextWidthEmu)
        {
            cy = cy * TextWidthEmu / cx;
            cx = TextWidthEmu;
        }

        return (cx, cy);
    }
}
