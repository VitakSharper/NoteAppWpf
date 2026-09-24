using NoteApp.Services.Export;

namespace NoteApp.Tests.Services;

// The .md text is the whole contract, so it is compared as text.
public class MarkdownExportServiceTests
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47];

    [Fact]
    public void Renders_every_element_kind_as_commonmark()
    {
        DocElement[] elements =
        [
            new DocParagraph([
                new DocText("Plain ", false, false, false),
                new DocText("bold", true, false, false),
                new DocText(" and ", false, false, false),
                new DocText("under", false, false, true),
                new DocLineBreak(),
                new DocText("see ", false, false, false),
                new DocText("the docs", false, true, false, Link: "https://docs.example.com/"),
                new DocText(" or ", false, false, false),
                new DocText("https://auto.example.com", false, false, true, Link: "https://auto.example.com/")]),
            new DocList(DocListMarker.Bullet, [[new DocText("one", false, false, false)], [new DocText("two", false, false, false)]]),
            new DocList(DocListMarker.LowerLatin, [[new DocText("first", false, false, false)]]),
            new DocChecklist([new DocChecklistItem("buy milk", true), new DocChecklistItem("read www.x.com", false)]),
            new DocLink("https://example.com/page", "Example"),
            new DocLink("https://example.com/bare", ""),
            new DocAttachment("report.pdf", 2048)
        ];

        var document = MarkdownExportService.Render("My note", elements, "note_files");

        Assert.Equal(
            """
            # My note

            Plain **bold** and <u>under</u>\
            see [*the docs*](https://docs.example.com/) or <https://auto.example.com/>

            - one
            - two

            1. first

            - [x] buy milk
            - [ ] read [www.x.com](https://www.x.com/)

            [Example](https://example.com/page)

            <https://example.com/bare>

            *Attachment: report.pdf (2 KB)*

            """.ReplaceLineEndings("\n"),
            document.Text);
        Assert.Empty(document.Images);
    }

    [Fact]
    public void Images_are_written_beside_the_file_and_referenced_relatively()
    {
        var document = MarkdownExportService.Render("N",
            [new DocParagraph([new DocImage(Png)]), new DocParagraph([new DocText("x", false, false, false), new DocImage(Png)])],
            "My note_files");

        Assert.Equal(["image1.png", "image2.png"], document.Images.Select(i => i.FileName));
        Assert.Contains("![](My%20note_files/image1.png)", document.Text);
        Assert.Contains("x![](My%20note_files/image2.png)", document.Text);
    }

    // The fields on their own lines; the password only as a placeholder.
    [Fact]
    public void A_secret_is_written_without_its_password()
    {
        var document = MarkdownExportService.Render("N",
            [new DocSecret("Support *site*", "vbanard", "https://support.example.com/")], "f");

        Assert.Equal(
            "# N\n\n**Support \\*site\\***\\\nUser name: vbanard\\\nPassword: *(not exported)*\\\nAddress: <https://support.example.com/>\n",
            document.Text);
    }

    // Text that looks like Markdown stays text.
    [Theory]
    [InlineData("a *b* [c] <d>", @"a \*b\* \[c\] \<d\>")]
    [InlineData("# not a heading", @"\# not a heading")]
    [InlineData("- not a list", @"\- not a list")]
    [InlineData("1. not numbered", @"1\. not numbered")]
    [InlineData("2026 was a year", "2026 was a year")]
    public void Markdown_characters_in_text_are_escaped(string text, string expected)
    {
        var document = MarkdownExportService.Render("N", [new DocParagraph([new DocText(text, false, false, false)])], "f");

        Assert.Equal($"# N\n\n{expected}\n", document.Text);
    }
}
