using System.IO;
using System.Text;
using NoteApp.Services;
using NoteApp.Services.Export;

namespace NoteApp.Tests.Services;

// QuestPDF checks every glyph against its font and throws on a missing one, so the
// cheapest useful test is that each element kind renders at all — links included.
public class PdfExportServiceTests
{
    static PdfExportServiceTests() =>
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

    [Fact]
    public void Renders_every_element_kind_with_their_links()
    {
        DocElement[] elements =
        [
            new DocParagraph([
                new DocText("see ", false, false, false),
                new DocText("the docs", true, false, true, Link: "https://docs.example.com/"),
                new DocLineBreak()]),
            new DocList(DocListMarker.Decimal, [[new DocText("first", false, false, false)]]),
            new DocChecklist([
                new DocChecklistItem("read https://mid.example.com/a later", IsDone: true),
                new DocChecklistItem("buy milk", IsDone: false)]),
            new DocLink("https://example.com/page", "Example"),
            new DocAttachment("report.pdf", 2048)
        ];
        using var stream = new MemoryStream();

        PdfExportService.Render("My note", elements, stream);

        var bytes = stream.ToArray();
        Assert.StartsWith("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));

        // Link annotations carry their target as a literal URI string.
        var raw = Encoding.Latin1.GetString(bytes);
        Assert.Contains("https://docs.example.com/", raw);
        Assert.Contains("https://mid.example.com/a", raw);
    }
}
