namespace NoteApp.Services.Export;

// What the editor hands an exporter, in note order. Text blocks carry the stored
// rich payload (Base64 XamlPackage); RichTextDocument turns it into DocElements.
public abstract record ExportBlock;
public sealed record TextExportBlock(string RichTextPayload) : ExportBlock;
public sealed record LinkExportBlock(string Url, string Description) : ExportBlock;
public sealed record FileExportBlock(string FileName, long SizeBytes) : ExportBlock;

// Everything the editor can produce, as plain data: extracted once from the WPF
// FlowDocument (UI thread) and rendered by every exporter without touching WPF.
public abstract record DocElement;
public sealed record DocParagraph(IReadOnlyList<DocInline> Inlines) : DocElement;
public sealed record DocList(DocListMarker Marker, IReadOnlyList<IReadOnlyList<DocInline>> Items) : DocElement;
public sealed record DocLink(string Url, string Description) : DocElement;
public sealed record DocAttachment(string FileName, long SizeBytes) : DocElement;

public enum DocListMarker
{
    Bullet,
    Decimal,
    LowerLatin,
    UpperLatin
}

public abstract record DocInline;
public sealed record DocText(string Text, bool IsBold, bool IsItalic, bool IsUnderline) : DocInline;
public sealed record DocImage(byte[] Png) : DocInline;
public sealed record DocLineBreak : DocInline;

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
