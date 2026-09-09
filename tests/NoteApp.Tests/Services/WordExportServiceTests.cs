using System.IO;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using NoteApp.Services.Export;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;

namespace NoteApp.Tests.Services;

// Render is the WPF-free half of the export: it takes the plain DocElement model and
// writes a .docx, which the SDK reads straight back for the assertions.
public class WordExportServiceTests
{
    // 1×1 transparent PNG: a real header for the size parser and a real image part.
    private static readonly byte[] TinyPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");

    [Fact]
    public void Renders_every_element_kind_into_a_readable_document()
    {
        DocElement[] elements =
        [
            new DocParagraph([
                new DocText("Plain ", false, false, false),
                new DocText("bold", true, false, false),
                new DocLineBreak(),
                new DocText("under", false, false, true)]),
            new DocList(DocListMarker.Decimal, [[new DocText("first", false, false, false)], [new DocText("second", false, false, false)]]),
            new DocParagraph([new DocImage(TinyPng)]),
            new DocLink("https://example.com/page", "Example"),
            new DocAttachment("report.pdf", 2048)
        ];
        using var stream = new MemoryStream();

        WordExportService.Render("My note", elements, stream);

        stream.Position = 0;
        using var doc = WordprocessingDocument.Open(stream, isEditable: false);
        var main = doc.MainDocumentPart!;
        var body = main.Document!.Body!;

        // title + paragraph + 2 list items + image + link + attachment
        Assert.Equal(7, body.Elements<Paragraph>().Count());
        Assert.Equal("My note", body.Elements<Paragraph>().First().InnerText);

        var boldRun = body.Descendants<Run>().Single(r => r.InnerText == "bold");
        Assert.NotNull(boldRun.RunProperties?.Bold);
        var underlinedRun = body.Descendants<Run>().Single(r => r.InnerText == "under");
        Assert.NotNull(underlinedRun.RunProperties?.Underline);
        Assert.Single(body.Descendants<Break>());

        var text = body.InnerText;
        Assert.Contains("1. first", text);
        Assert.Contains("2. second", text);
        Assert.Contains("Attachment: report.pdf (2 KB)", text);

        Assert.Single(main.ImageParts);

        var link = Assert.Single(body.Descendants<Hyperlink>());
        Assert.Equal("Example", link.InnerText);
        Assert.Equal(new Uri("https://example.com/page"), main.HyperlinkRelationships.Single(r => r.Id == link.Id).Uri);
        Assert.Contains("https://example.com/page", text);
    }

    [Fact]
    public void A_link_that_is_not_an_absolute_url_is_written_as_plain_text()
    {
        using var stream = new MemoryStream();

        WordExportService.Render("Note", [new DocLink("not a url", "")], stream);

        stream.Position = 0;
        using var doc = WordprocessingDocument.Open(stream, isEditable: false);
        var body = doc.MainDocumentPart!.Document!.Body!;
        Assert.Empty(body.Descendants<Hyperlink>());
        Assert.Contains("not a url", body.InnerText);
    }

    // Only the IHDR bytes matter to the size parser: a 5000×1000 header must come out
    // scaled to the text column (A4 minus 2 cm margins = 6 120 130 EMU) with its ratio kept.
    [Fact]
    public void Wide_images_are_scaled_down_to_the_text_width()
    {
        var wide = new byte[24];
        wide[16] = 0x00; wide[17] = 0x00; wide[18] = 0x13; wide[19] = 0x88; // 5000
        wide[20] = 0x00; wide[21] = 0x00; wide[22] = 0x03; wide[23] = 0xE8; // 1000
        using var stream = new MemoryStream();

        WordExportService.Render("Note", [new DocParagraph([new DocImage(wide)])], stream);

        stream.Position = 0;
        using var doc = WordprocessingDocument.Open(stream, isEditable: false);
        var extent = Assert.Single(doc.MainDocumentPart!.Document!.Body!.Descendants<DW.Extent>());
        Assert.Equal(6_120_130L, extent.Cx!.Value);
        Assert.Equal(1_224_026L, extent.Cy!.Value);
    }

    // A ballot-box character rather than a Word content control: it reads the same in
    // any editor and survives a copy-paste. A ticked item is struck through.
    [Fact]
    public void Renders_a_checklist_as_ballot_boxes_and_strikes_what_is_done()
    {
        using var stream = new MemoryStream();

        WordExportService.Render("Note",
            [new DocChecklist([new DocChecklistItem("buy milk", true), new DocChecklistItem("call the bank", false)])],
            stream);

        stream.Position = 0;
        using var doc = WordprocessingDocument.Open(stream, isEditable: false);
        var paragraphs = doc.MainDocumentPart!.Document!.Body!.Elements<Paragraph>().ToList();

        Assert.Equal(3, paragraphs.Count); // title + one per item
        Assert.Equal("☑ buy milk", paragraphs[1].InnerText);
        Assert.Equal("☐ call the bank", paragraphs[2].InnerText);
        Assert.NotNull(paragraphs[1].Descendants<Run>().Single().RunProperties?.Strike);
        Assert.Null(paragraphs[2].Descendants<Run>().Single().RunProperties?.Strike);
    }

    [Fact]
    public void List_markers_match_the_pdf_export()
    {
        Assert.Equal("• ", DocListMarkers.Text(DocListMarker.Bullet, 4));
        Assert.Equal("3. ", DocListMarkers.Text(DocListMarker.Decimal, 2));
        Assert.Equal("c. ", DocListMarkers.Text(DocListMarker.LowerLatin, 2));
        Assert.Equal("C. ", DocListMarkers.Text(DocListMarker.UpperLatin, 2));
    }
}
