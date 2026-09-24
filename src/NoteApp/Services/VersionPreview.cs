using System.Globalization;
using NoteApp.Domain.Models;
using NoteApp.Services.Export;

namespace NoteApp.Services;

// A version of a note as plain lines, for the history window to show before a restore:
// what each block said, the password of a secret never.
public static class VersionPreview
{
    public static IReadOnlyList<string> Lines(IReadOnlyList<NoteBlock> blocks) =>
        blocks.SelectMany(block => block.Match<IEnumerable<string>>(
            text: t => Paragraphs(t.PlainText.Length > 0 || t.RichText.Length == 0 ? t.PlainText : RichTextPreview.ExtractPlainText(t.RichText)),
            file: f => [$"📎 {f.FileName} ({DocAttachment.Size(f.SizeBytes)})"],
            link: l => [string.IsNullOrWhiteSpace(l.Description) ? $"🔗 {l.Url}" : $"🔗 {l.Description} — {l.Url}"],
            checklist: c => c.Items.Select(i => $"{(i.IsDone ? "☑" : "☐")} {i.Text}"),
            secret: s => [$"🔑 {string.Join(" · ", new[] { s.Label, s.UserName, s.Url }.Where(x => x.Length > 0))} · password hidden"],
            code: c => Paragraphs(c.Content).Select(line => "  " + line)))
        .ToList();

    public static string When(DateTime savedUtc) =>
        DateTime.SpecifyKind(savedUtc, DateTimeKind.Utc).ToLocalTime().ToString("ddd d MMM yyyy, HH:mm", CultureInfo.CurrentCulture);

    private static IEnumerable<string> Paragraphs(string text) =>
        text.ReplaceLineEndings("\n").Split('\n').Select(line => line.TrimEnd()).Where(line => line.Length > 0);
}
