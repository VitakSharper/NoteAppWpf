using System.Windows;
using System.Windows.Documents;
using NoteApp.Services;
using static NoteApp.UiTests.Notes;

namespace NoteApp.UiTests;

// Linkify on documents shaped the way real notes are: an address is often cut into
// several Runs — a paste, a bit of formatting, a search highlight that split the text.
public class RichTextLinksUiTests
{
    private const string Address = "http://10.105.78.100/Support/Lists/Demande%20de%20nouvelle%20version/AllItems.aspx";

    [Fact]
    public void An_address_split_across_runs_is_linked_whole() => Wpf.Run(() =>
    {
        var document = Document(new Run("http://10.105.78.100/Support/Lists/De"), new Run("mande%20de%20nouvelle%20version/AllItems.aspx"));

        RichTextLinks.Linkify(document);

        Assert.Equal([Address], Hyperlinks(document).Select(TextOf));
    });

    [Fact]
    public void An_address_partly_in_bold_is_linked_whole_and_keeps_its_bold() => Wpf.Run(() =>
    {
        var document = Document(new Run("see https://example.com/"), new Bold(new Run("important")), new Run("/page ok"));

        RichTextLinks.Linkify(document);

        var link = Assert.Single(Hyperlinks(document));
        Assert.Equal("https://example.com/important/page", TextOf(link));
        Assert.Contains(Inlines(document.Blocks).OfType<Run>(), r => r.Text == "important" && r.FontWeight == FontWeights.Bold);
    });

    // What notes linked before this fix look like: the link stops where a Run did.
    [Fact]
    public void A_link_cut_short_earlier_is_repaired() => Wpf.Run(() =>
    {
        var document = Document(new Hyperlink(new Run("http://10.105.78.100/Support/Lists/De")), new Run("mande%20de%20nouvelle%20version/AllItems.aspx"));

        RichTextLinks.Linkify(document);

        Assert.Equal([Address], Hyperlinks(document).Select(TextOf));
    });

    [Fact]
    public void An_automatic_link_that_is_no_longer_an_address_goes_back_to_text() => Wpf.Run(() =>
    {
        var document = Document(new Run("before "), new Hyperlink(new Run("just words")), new Run(" after"));

        RichTextLinks.Linkify(document);

        Assert.Empty(Hyperlinks(document));
        Assert.Equal("before just words after", AllText(document));
    });

    // A pasted link has its own target: it is never merged, cut or unwrapped.
    [Fact]
    public void A_pasted_link_is_left_alone() => Wpf.Run(() =>
    {
        var document = Document(new Hyperlink(new Run("docs")) { NavigateUri = new Uri("https://docs.example.com/") }, new Run("/more"));

        RichTextLinks.Linkify(document);

        var link = Assert.Single(Hyperlinks(document));
        Assert.Equal("docs", TextOf(link));
        Assert.Equal(new Uri("https://docs.example.com/"), link.NavigateUri);
    });

    [Fact]
    public void A_line_break_ends_an_address() => Wpf.Run(() =>
    {
        var document = Document(new Run("https://a.com"), new LineBreak(), new Run("next line"));

        RichTextLinks.Linkify(document);

        Assert.Equal(["https://a.com"], Hyperlinks(document).Select(TextOf));
    });

    [Fact]
    public void Linking_twice_changes_nothing() => Wpf.Run(() =>
    {
        var document = Document(new Run("a https://x.com b "), new Run("www.y.com"));
        RichTextLinks.Linkify(document);
        var first = Hyperlinks(document);

        RichTextLinks.Linkify(document);

        Assert.Equal(first, Hyperlinks(document));
    });

    // Repairing a link moves its text out of the old Hyperlink: the caret must stay put.
    [Fact]
    public void The_caret_stays_where_it_was_when_a_link_is_repaired() => Wpf.Run(() =>
    {
        using var editor = new OpenEditor(With(Text("start")));
        var box = editor.RichText();
        box.Document.Blocks.Clear();
        box.Document.Blocks.Add(new Paragraph());
        var paragraph = (Paragraph)box.Document.Blocks.FirstBlock;
        paragraph.Inlines.Add(new Hyperlink(new Run("https://ex")));
        paragraph.Inlines.Add(new Run("ample.com and more"));
        var inside = ((Run)((Hyperlink)paragraph.Inlines.FirstInline).Inlines.FirstInline).ContentStart.GetPositionAtOffset(4)!;
        box.CaretPosition = inside;

        RichTextLinks.Linkify(box.Document, box.Selection);

        Assert.Equal("https://example.com", TextOf(Assert.Single(Hyperlinks(box.Document))));
        Assert.Equal("http", new TextRange(paragraph.ContentStart, box.CaretPosition).Text);
    });

    private static FlowDocument Document(params Inline[] inlines)
    {
        var paragraph = new Paragraph();
        paragraph.Inlines.AddRange(inlines);
        return new FlowDocument(paragraph);
    }

    private static string AllText(FlowDocument document) =>
        new TextRange(document.ContentStart, document.ContentEnd).Text.TrimEnd('\r', '\n');
}
