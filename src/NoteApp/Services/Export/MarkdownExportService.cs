using System.IO;
using System.Text;

namespace NoteApp.Services.Export;

public sealed record MarkdownImage(string FileName, byte[] Png);

// The rendered note: the .md text, and the images it references by relative path.
public sealed record MarkdownDocument(string Text, IReadOnlyList<MarkdownImage> Images);

// CommonMark, the dialect GitHub, Obsidian and VS Code read. Markdown has no underline, so
// it is written as <u>, and no lettered lists, so "a." lists come out numbered. Images
// cannot live inside the text: they are written next to it, in "<name>_files".
public static class MarkdownExportService
{
    public static void Export(string noteTitle, IReadOnlyList<ExportBlock> blocks, string outputPath)
    {
        var imageFolder = Path.GetFileNameWithoutExtension(outputPath) + "_files";
        var document = Render(noteTitle, RichTextDocument.Extract(blocks), imageFolder);

        File.WriteAllText(outputPath, document.Text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        if (document.Images.Count == 0)
            return;

        var folder = Path.Combine(Path.GetDirectoryName(outputPath) ?? ".", imageFolder);
        Directory.CreateDirectory(folder);
        foreach (var image in document.Images)
            File.WriteAllBytes(Path.Combine(folder, image.FileName), image.Png);
    }

    public static MarkdownDocument Render(string noteTitle, IReadOnlyList<DocElement> elements, string imageFolder)
    {
        var markdown = new StringBuilder();
        var images = new List<MarkdownImage>();

        markdown.Append("# ").Append(Escape(noteTitle)).Append("\n\n");

        foreach (var element in elements)
        {
            switch (element)
            {
                case DocParagraph paragraph:
                    markdown.Append(Inlines(paragraph.Inlines, images, imageFolder)).Append("\n\n");
                    break;

                case DocList list:
                    var ordered = list.Marker != DocListMarker.Bullet;
                    for (var i = 0; i < list.Items.Count; i++)
                        markdown.Append(ordered ? $"{i + 1}. " : "- ").Append(Inlines(list.Items[i], images, imageFolder)).Append('\n');
                    markdown.Append('\n');
                    break;

                case DocChecklist checklist:
                    foreach (var item in checklist.Items)
                        markdown.Append(item.IsDone ? "- [x] " : "- [ ] ").Append(TextWithLinks(item.Text)).Append('\n');
                    markdown.Append('\n');
                    break;

                case DocLink link:
                    markdown.Append(string.IsNullOrWhiteSpace(link.Description)
                        ? $"<{link.Url}>"
                        : $"[{Escape(link.Description)}]({Destination(link.Url)})").Append("\n\n");
                    break;

                case DocAttachment attachment:
                    markdown.Append($"*Attachment: {Escape(attachment.FileName)} ({DocAttachment.Size(attachment.SizeBytes)})*\n\n");
                    break;
            }
        }

        return new MarkdownDocument(markdown.ToString().TrimEnd('\n') + "\n", images);
    }

    private static string Inlines(IReadOnlyList<DocInline> inlines, List<MarkdownImage> images, string imageFolder)
    {
        var text = new StringBuilder();
        foreach (var inline in inlines)
        {
            switch (inline)
            {
                case DocText t:
                    text.Append(Text(t));
                    break;

                // CommonMark's hard line break: a backslash at the end of the line.
                case DocLineBreak:
                    text.Append("\\\n");
                    break;

                case DocImage image:
                    var name = $"image{images.Count + 1}.png";
                    images.Add(new MarkdownImage(name, image.Png));
                    text.Append($"![]({Destination($"{imageFolder}/{name}")})");
                    break;
            }
        }

        return EscapeLineStart(text.ToString());
    }

    // Emphasis markers must hug the text, so surrounding spaces stay outside them.
    private static string Text(DocText t)
    {
        var core = t.Text.Trim();
        if (core.Length == 0)
            return t.Text;

        var leading = t.Text[..t.Text.IndexOf(core, StringComparison.Ordinal)];
        var trailing = t.Text[(leading.Length + core.Length)..];

        var body = Escape(core);
        if (t.IsBold) body = $"**{body}**";
        if (t.IsItalic) body = $"*{body}*";
        if (t.IsUnderline && t.Link is null) body = $"<u>{body}</u>";
        if (t.Link is not null)
            body = t.Link.TrimEnd('/') == core.TrimEnd('/') && !t.IsBold && !t.IsItalic
                ? $"<{t.Link}>"
                : $"[{body}]({Destination(t.Link)})";

        return leading + body + trailing;
    }

    private static string TextWithLinks(string text) =>
        EscapeLineStart(string.Concat(DocTextLinks.Split(text).Select(piece =>
            piece.Link is null ? Escape(piece.Text) : $"[{Escape(piece.Text)}]({Destination(piece.Link)})")));

    // Characters Markdown would read as formatting.
    private static string Escape(string text)
    {
        var escaped = new StringBuilder(text.Length);
        foreach (var ch in text)
        {
            if (@"\`*_[]<>|".Contains(ch))
                escaped.Append('\\');
            escaped.Append(ch);
        }

        return escaped.ToString();
    }

    // A paragraph that happens to start like a heading, a quote, a list item or a numbered
    // item ("1. ") would turn into one.
    private static string EscapeLineStart(string text)
    {
        if (text.Length == 0)
            return text;

        if (text[0] is '#' or '>' or '-' or '+')
            return "\\" + text;

        var digits = 0;
        while (digits < text.Length && char.IsAsciiDigit(text[digits]))
            digits++;

        return digits > 0 && digits < text.Length && text[digits] is '.' or ')'
            ? text[..digits] + "\\" + text[digits..]
            : text;
    }

    // Spaces and parentheses would end a link destination early.
    private static string Destination(string url) =>
        url.Replace(" ", "%20").Replace("(", "%28").Replace(")", "%29");
}
