using System.Text;
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
    // Addresses are found in each paragraph's whole text, not Run by Run: WPF cuts text
    // into Runs for its own reasons (a paste, some formatting, a search highlight), and an
    // address cut in two was linked only up to the cut. An automatic link that no longer
    // matches an address exactly is taken apart and linked again, which also repairs the
    // links cut short that way before. Pass the box's selection so the caret stays put.
    public static void Linkify(FlowDocument document, TextSelection? selection = null)
    {
        foreach (var paragraph in Paragraphs(document.Blocks).ToList())
            Linkify(paragraph, selection);
    }

    private static void Linkify(Paragraph paragraph, TextSelection? selection)
    {
        var map = CharMap.Of(paragraph);
        var links = TextLinks.Find(map.Text);

        var stale = map.AutoLinks().Where(link => !links.Any(l => map.Covers(link, l))).ToList();
        if (stale.Count > 0)
        {
            // Moving a link's Runs out of it collapses any position inside them: keep the
            // selection as character indices, which unwrapping does not change.
            (int Start, int End)? kept = selection is not null && selection.Start.Paragraph == paragraph
                ? (map.IndexOf(selection.Start), map.IndexOf(selection.End))
                : null;

            foreach (var link in stale)
                Unwrap(link);

            map = CharMap.Of(paragraph);
            if (kept is { } k)
                selection!.Select(map.PositionAt(k.Start), map.PositionAt(k.End));
        }

        // Last first: wrapping splits Runs, which leaves the positions before it alone.
        for (var i = links.Count - 1; i >= 0; i--)
        {
            var link = links[i];
            if (map.OwnerAt(link.Start) is not null)
                continue; // already linked, exactly (anything else was unwrapped above)

            _ = new Hyperlink(map.PositionAt(link.Start), map.PositionAt(link.Start + link.Length));
        }
    }

    // The link a click landed on. GetPositionFromPoint puts the position inside the Run
    // of the character under the pointer, so the Hyperlink is one of its ancestors.
    public static Option<LinkUrl> LinkAt(TextPointer position) =>
        EnclosingHyperlink(position) is { } hyperlink ? Target(hyperlink) : Option<LinkUrl>.Empty();

    // Same rule as an address in the text (LinkUrl): a pasted hyperlink can point at
    // file: or anything else the shell would run, and those never open. The exporters
    // use it too, so a link leads to the same place in Word as in the editor.
    public static Option<LinkUrl> Target(Hyperlink hyperlink) =>
        hyperlink.NavigateUri is { } uri && LinkUrl.From(uri.OriginalString).TryGet(out var url, out _)
            ? new Option<LinkUrl>.Some(url)
            : TextLinks.First(new TextRange(hyperlink.ContentStart, hyperlink.ContentEnd).Text);

    private static Hyperlink? EnclosingHyperlink(TextPointer position)
    {
        for (var element = position.Parent as TextElement; element is not null; element = element.Parent as TextElement)
        {
            if (element is Hyperlink hyperlink)
                return hyperlink;
        }

        return null;
    }

    private static void Unwrap(Hyperlink link)
    {
        var siblings = link.Parent switch
        {
            Paragraph paragraph => paragraph.Inlines,
            Span span => span.Inlines,
            _ => null
        };
        if (siblings is null)
            return;

        foreach (var child in link.Inlines.ToList())
        {
            link.Inlines.Remove(child);
            siblings.InsertBefore(link, child);
        }

        siblings.Remove(link);
    }

    // Every paragraph of the document: in lists, sections and table cells too.
    public static IEnumerable<Paragraph> ParagraphsOf(FlowDocument document) => Paragraphs(document.Blocks);

    private static IEnumerable<Paragraph> Paragraphs(IEnumerable<Block> blocks) =>
        blocks.SelectMany(block => block switch
        {
            Paragraph paragraph => [paragraph],
            Section section => Paragraphs(section.Blocks),
            List list => list.ListItems.SelectMany(item => Paragraphs(item.Blocks)),
            Table table => table.RowGroups.SelectMany(g => g.Rows).SelectMany(r => r.Cells).SelectMany(c => Paragraphs(c.Blocks)),
            _ => []
        });

    // A paragraph's text with a position for each character, and the automatic link (no
    // NavigateUri) each one sits in. A pasted link's text, a line break and an image read
    // as spaces, so no address is ever found running through them.
    private sealed class CharMap
    {
        private readonly List<TextPointer?> _positions = [];
        private readonly List<Hyperlink?> _owners = [];
        private readonly StringBuilder _text = new();
        private TextPointer _end = null!;

        public string Text => _text.ToString();

        public static CharMap Of(Paragraph paragraph)
        {
            var map = new CharMap { _end = paragraph.ContentEnd };
            var position = paragraph.ContentStart;

            while (position.CompareTo(paragraph.ContentEnd) < 0)
            {
                switch (position.GetPointerContext(LogicalDirection.Forward))
                {
                    case TextPointerContext.Text:
                        var text = position.GetTextInRun(LogicalDirection.Forward);
                        var owner = EnclosingHyperlink(position);
                        var pasted = owner is { NavigateUri: not null };
                        for (var i = 0; i < text.Length; i++)
                        {
                            if (pasted)
                                map.Separator();
                            else
                                map.Add(text[i], position.GetPositionAtOffset(i)!, owner);
                        }

                        position = position.GetPositionAtOffset(text.Length)!;
                        continue;

                    case TextPointerContext.EmbeddedElement:
                        map.Separator();
                        break;

                    case TextPointerContext.ElementStart when position.GetAdjacentElement(LogicalDirection.Forward) is LineBreak:
                        map.Separator();
                        break;
                }

                position = position.GetNextContextPosition(LogicalDirection.Forward)!;
            }

            return map;
        }

        private void Add(char ch, TextPointer position, Hyperlink? owner)
        {
            _text.Append(ch);
            _positions.Add(position);
            _owners.Add(owner);
        }

        private void Separator()
        {
            _text.Append(' ');
            _positions.Add(null);
            _owners.Add(null);
        }

        public Hyperlink? OwnerAt(int index) => _owners[index];

        public IEnumerable<Hyperlink> AutoLinks() => _owners.OfType<Hyperlink>().Distinct();

        // The link holds exactly the address's characters, no more, no less.
        public bool Covers(Hyperlink link, TextLink address)
        {
            var first = _owners.IndexOf(link);
            var last = _owners.LastIndexOf(link);
            return first == address.Start && last == address.Start + address.Length - 1;
        }

        // Before the character at index, or the end of the paragraph's text.
        public TextPointer PositionAt(int index)
        {
            if (index < _positions.Count && _positions[index] is { } position)
                return position;

            for (var i = Math.Min(index, _positions.Count) - 1; i >= 0; i--)
            {
                if (_positions[i] is { } previous)
                    return previous.GetPositionAtOffset(1)!;
            }

            return _end;
        }

        // The number of characters before the position.
        public int IndexOf(TextPointer position)
        {
            for (var i = 0; i < _positions.Count; i++)
            {
                if (_positions[i] is { } p && p.CompareTo(position) >= 0)
                    return i;
            }

            return _positions.Count;
        }
    }
}
