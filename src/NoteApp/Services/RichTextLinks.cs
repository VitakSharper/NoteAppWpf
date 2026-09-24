using System.Windows.Documents;
using NoteApp.Domain.Functional;
using NoteApp.Domain.ValueObjects;
namespace NoteApp.Services;

// The FlowDocument half of clickable links in a text block; TextLinks finds them.
// Addresses become Hyperlinks, stored with the note like any other formatting. The ones
// made here carry no NavigateUri: their text is their target, so correcting a typo in
// the address corrects the link. A pasted link keeps the target it came with.
public static class RichTextLinks
{
    // Wraps every address that is not inside a Hyperlink yet. An address is only seen
    // within one Run: one typed half in bold stays plain text.
    public static void Linkify(FlowDocument document)
    {
        var found = new List<(TextPointer Start, TextPointer End)>();
        foreach (var run in Runs(document.Blocks).Where(r => !IsInsideHyperlink(r)))
        {
            foreach (var link in TextLinks.Find(run.Text))
                found.Add((run.ContentStart.GetPositionAtOffset(link.Start)!,
                    run.ContentStart.GetPositionAtOffset(link.Start + link.Length)!));
        }

        // Last first: wrapping splits the Run, which leaves the positions before it alone.
        for (var i = found.Count - 1; i >= 0; i--)
            _ = new Hyperlink(found[i].Start, found[i].End);
    }

    // The link a click landed on. GetPositionFromPoint puts the position inside the Run
    // of the character under the pointer, so the Hyperlink is one of its ancestors.
    public static Option<LinkUrl> LinkAt(TextPointer position)
    {
        for (var element = position.Parent as TextElement; element is not null; element = element.Parent as TextElement)
        {
            if (element is Hyperlink hyperlink)
                return Target(hyperlink);
        }

        return Option<LinkUrl>.Empty();
    }

    // Same rule as an address in the text (LinkUrl): a pasted hyperlink can point at
    // file: or anything else the shell would run, and those never open. The exporters
    // use it too, so a link leads to the same place in Word as in the editor.
    public static Option<LinkUrl> Target(Hyperlink hyperlink) =>
        hyperlink.NavigateUri is { } uri && LinkUrl.From(uri.OriginalString).TryGet(out var url, out _)
            ? new Option<LinkUrl>.Some(url)
            : TextLinks.First(new TextRange(hyperlink.ContentStart, hyperlink.ContentEnd).Text);

    private static bool IsInsideHyperlink(TextElement element)
    {
        for (var parent = element.Parent as TextElement; parent is not null; parent = parent.Parent as TextElement)
        {
            if (parent is Hyperlink)
                return true;
        }

        return false;
    }

    private static IEnumerable<Run> Runs(IEnumerable<Block> blocks) =>
        blocks.SelectMany(block => block switch
        {
            Paragraph paragraph => Runs(paragraph.Inlines),
            Section section => Runs(section.Blocks),
            List list => list.ListItems.SelectMany(item => Runs(item.Blocks)),
            Table table => table.RowGroups.SelectMany(g => g.Rows).SelectMany(r => r.Cells).SelectMany(c => Runs(c.Blocks)),
            _ => []
        });

    private static IEnumerable<Run> Runs(InlineCollection inlines) =>
        inlines.SelectMany(inline => inline switch
        {
            Run run => [run],
            Span span => Runs(span.Inlines),
            _ => []
        });
}
