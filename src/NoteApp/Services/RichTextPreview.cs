using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Markup;

namespace NoteApp.Services;

// Plain-text view of a rich text block. Since the PlainText column exists the
// editor stores the plain text next to the rich payload, so the list only has
// to decode a XamlPackage for rows saved before that (and only once per load).
public static partial class RichTextPreview
{
    private const int MaxLength = 120;

    public static string Snippet(string? plainText, string? richText)
    {
        var plain = plainText ?? (string.IsNullOrEmpty(richText) ? string.Empty : ExtractPlainText(richText));
        plain = WhitespaceRun().Replace(plain, " ").Trim();
        return plain.Length > MaxLength ? plain[..MaxLength] + "…" : plain;
    }

    // Formats seen in the wild, newest first: Base64 XamlPackage (image support),
    // legacy FlowDocument XAML, and plain text as the last resort.
    public static string ExtractPlainText(string content)
    {
        try
        {
            var bytes = Convert.FromBase64String(content);
            using var ms = new MemoryStream(bytes);
            var doc = new FlowDocument();
            new TextRange(doc.ContentStart, doc.ContentEnd).Load(ms, DataFormats.XamlPackage);
            return new TextRange(doc.ContentStart, doc.ContentEnd).Text;
        }
        catch { /* not XamlPackage */ }

        try
        {
            if (XamlReader.Parse(content) is FlowDocument parsed)
                return new TextRange(parsed.ContentStart, parsed.ContentEnd).Text;
        }
        catch { /* not XAML */ }

        return content;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRun();
}
