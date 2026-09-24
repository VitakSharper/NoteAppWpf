namespace NoteApp.Services.Export;

// What the editor hands an exporter, in note order. Text blocks carry the stored
// rich payload (Base64 XamlPackage); RichTextDocument turns it into DocElements.
public abstract record ExportBlock;
public sealed record TextExportBlock(string RichTextPayload) : ExportBlock;
public sealed record LinkExportBlock(string Url, string Description) : ExportBlock;
public sealed record FileExportBlock(string FileName, long SizeBytes) : ExportBlock;
public sealed record ChecklistExportBlock(IReadOnlyList<DocChecklistItem> Items) : ExportBlock;
// No password: the editor never hands it to an exporter.
public sealed record SecretExportBlock(string Label, string UserName, string Url) : ExportBlock;

// Everything the editor can produce, as plain data: extracted once from the WPF
// FlowDocument (UI thread) and rendered by every exporter without touching WPF.
public abstract record DocElement;
// HeadingLevel: 1 to 3 for a heading (RichTextFormat), 0 for body text.
public sealed record DocParagraph(IReadOnlyList<DocInline> Inlines, int HeadingLevel = 0) : DocElement;
public sealed record DocList(DocListMarker Marker, IReadOnlyList<IReadOnlyList<DocInline>> Items) : DocElement;
public sealed record DocLink(string Url, string Description) : DocElement;
public sealed record DocAttachment(string FileName, long SizeBytes) : DocElement
{
    // "2 KB": how every exporter writes the size of an attachment it lists by name.
    public static string Size(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:0.#} KB",
        _ => $"{bytes / (1024.0 * 1024.0):0.#} MB"
    };
}
public sealed record DocChecklist(IReadOnlyList<DocChecklistItem> Items) : DocElement;

// A secret on paper: what it is for, then one line per field — the password only as a
// placeholder. Every exporter writes these same lines.
public sealed record DocSecret(string Label, string UserName, string Url) : DocElement
{
    public const string PasswordPlaceholder = "(not exported)";

    public string Heading => string.IsNullOrWhiteSpace(Label) ? "Secret" : Label;

    // Link: the address the value leads to, for the address line.
    public IReadOnlyList<(string Name, string Value, string? Link)> Fields =>
    [
        .. UserName.Length > 0 ? [("User name", UserName, (string?)null)] : Array.Empty<(string, string, string?)>(),
        ("Password", PasswordPlaceholder, null),
        .. Url.Length > 0 ? [("Address", Url, (string?)Url)] : Array.Empty<(string, string, string?)>()
    ];
}

public sealed record DocChecklistItem(string Text, bool IsDone);

public enum DocListMarker
{
    Bullet,
    Decimal,
    LowerLatin,
    UpperLatin
}

public abstract record DocInline;
// Link: where the text leads when it sits in a hyperlink (an absolute HTTP/HTTPS address).
// IsHighlight: the editor's highlighter; IsCode: inline code (a monospace run).
public sealed record DocText(string Text, bool IsBold, bool IsItalic, bool IsUnderline, string? Link = null,
    bool IsStrike = false, bool IsHighlight = false, bool IsCode = false) : DocInline;
public sealed record DocImage(byte[] Png) : DocInline;
public sealed record DocLineBreak : DocInline;

// Plain text cut around the web addresses it mentions (TextLinks), for the parts of a
// note that are not rich text — checklist items: each piece carries its link, if any.
public static class DocTextLinks
{
    public static IReadOnlyList<(string Text, string? Link)> Split(string text)
    {
        var pieces = new List<(string, string?)>();
        var position = 0;
        foreach (var link in TextLinks.Find(text))
        {
            if (link.Start > position)
                pieces.Add((text[position..link.Start], null));
            pieces.Add((text.Substring(link.Start, link.Length), link.Url.Value.AbsoluteUri));
            position = link.Start + link.Length;
        }

        if (position < text.Length)
            pieces.Add((text[position..], null));
        return pieces;
    }
}

public static class DocListMarkers
{
    // Both exporters write the marker as text — the "1. " / "a. " / "• " the PDF
    // always had — rather than driving each format's own numbering machinery.
    public static string Text(DocListMarker marker, int index) => marker switch
    {
        DocListMarker.Decimal => $"{index + 1}. ",
        DocListMarker.LowerLatin => $"{(char)('a' + index)}. ",
        DocListMarker.UpperLatin => $"{(char)('A' + index)}. ",
        _ => "• "
    };
}
