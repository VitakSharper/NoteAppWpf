using System.IO;
using System.Windows;
using System.Windows.Documents;

namespace NoteApp.Services;

// A text block's stored form (a Base64 XamlPackage) built from a FlowDocument or from plain
// text — for notes that do not come from the editor: a quick note, an imported file.
// WPF: on the UI thread.
public static class RichTextPayload
{
    public static string From(FlowDocument document)
    {
        RichTextLinks.Linkify(document);
        using var stream = new MemoryStream();
        new TextRange(document.ContentStart, document.ContentEnd).Save(stream, DataFormats.XamlPackage);
        return Convert.ToBase64String(stream.ToArray());
    }

    // One paragraph per line; addresses become links, as they would once typed.
    public static string FromPlainText(string text)
    {
        var document = new FlowDocument();
        foreach (var line in text.ReplaceLineEndings("\n").Split('\n'))
            document.Blocks.Add(new Paragraph(new Run(line)));
        return From(document);
    }
}
