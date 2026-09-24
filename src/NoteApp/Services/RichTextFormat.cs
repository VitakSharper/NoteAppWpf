using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace NoteApp.Services;

// The formatting a text block offers beyond bold / italic / underline, and how each one is
// written into the FlowDocument — the one place the editor and the exports agree on it,
// since a XamlPackage keeps plain properties and nothing else (no styles, no tags):
// - a heading is a paragraph with one of HeadingSizes as its own FontSize, and bold;
// - the highlighter is the Background Highlighter, on a run or a span;
// - inline code is the CodeFont family.
public static class RichTextFormat
{
    // H1, H2, H3. Body text is the box's own size (12–14), so these never collide with it.
    public static readonly IReadOnlyList<double> HeadingSizes = [26, 21, 17];

    // Translucent amber: readable under dark text on the light theme and under light text
    // on the dark one. Frozen, so it can be compared and shared across documents.
    public static readonly SolidColorBrush Highlighter = Frozen(Color.FromArgb(0x80, 0xFF, 0xD5, 0x4F));

    public static readonly FontFamily CodeFont = new("Consolas");

    // 0 for body text. A loaded paragraph always carries the document's size as its own,
    // hence the exact sizes and the weight rather than "has a local FontSize".
    public static int HeadingLevel(Paragraph paragraph)
    {
        if (paragraph.FontWeight != FontWeights.Bold)
            return 0;

        for (var i = 0; i < HeadingSizes.Count; i++)
        {
            if (Math.Abs(paragraph.FontSize - HeadingSizes[i]) < 0.01)
                return i + 1;
        }

        return 0;
    }

    // Level 0 turns the paragraph back into body text. Sizes set on its runs (pasted text)
    // would override the heading's, so they go.
    public static void SetHeading(Paragraph paragraph, int level)
    {
        foreach (var inline in Descendants(paragraph.Inlines))
            inline.ClearValue(TextElement.FontSizeProperty);

        if (level is < 1 or > 3)
        {
            paragraph.ClearValue(TextElement.FontSizeProperty);
            paragraph.ClearValue(TextElement.FontWeightProperty);
            return;
        }

        paragraph.FontSize = HeadingSizes[level - 1];
        paragraph.FontWeight = FontWeights.Bold;
    }

    public static bool IsHighlighter(object? background) =>
        background is SolidColorBrush brush && brush.Color == Highlighter.Color;

    public static bool IsCode(TextElement element) =>
        element.FontFamily.Source.Equals(CodeFont.Source, StringComparison.OrdinalIgnoreCase);

    // The highlighter of the text a run sits in: on the run itself, or on a span around it.
    public static bool IsHighlighted(TextElement element)
    {
        for (DependencyObject? node = element; node is Inline inline; node = inline.Parent)
        {
            if (IsHighlighter(inline.ReadLocalValue(TextElement.BackgroundProperty)))
                return true;
        }

        return false;
    }

    public static bool HasDecoration(Inline inline, TextDecorationLocation location) =>
        inline.TextDecorations?.Any(d => d.Location == location) == true;

    // Adds the decoration to the selection, or takes it away when all of it has it already —
    // keeping the other decorations (underline and strikethrough live in one collection).
    public static void ToggleDecoration(TextRange range, TextDecorationLocation location)
    {
        var current = range.GetPropertyValue(Inline.TextDecorationsProperty) as TextDecorationCollection;
        var has = current?.Any(d => d.Location == location) == true;

        var next = new TextDecorationCollection(current?.Where(d => d.Location != location) ?? []);
        if (!has)
            next.Add(location == TextDecorationLocation.Strikethrough ? TextDecorations.Strikethrough[0] : TextDecorations.Underline[0]);

        range.ApplyPropertyValue(Inline.TextDecorationsProperty, next.Count == 0 ? null : next);
    }

    public static IEnumerable<Inline> InlinesOf(FlowDocument document) =>
        RichTextLinks.ParagraphsOf(document).SelectMany(p => Descendants(p.Inlines));

    private static IEnumerable<Inline> Descendants(InlineCollection inlines) =>
        inlines.SelectMany(i => i is Span span ? [i, .. Descendants(span.Inlines)] : new[] { i });

    private static SolidColorBrush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
