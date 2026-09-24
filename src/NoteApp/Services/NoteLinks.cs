using NoteApp.Domain.Functional;
using NoteApp.Domain.ValueObjects;

namespace NoteApp.Services;

// A link from one note to another, as the text block's Hyperlink holds it: its NavigateUri is
// noteapp://note/<id>. The XamlPackage keeps NavigateUri, so the link survives the storage;
// the ids a note links to are also stored with its text blocks (NoteBlocks.LinkedNoteIds),
// which is what "Linked from" is looked up by.
public static class NoteLinks
{
    public const string Scheme = "noteapp";
    private const string Host = "note";

    public static Uri UriFor(NoteId id) => new($"{Scheme}://{Host}/{id.Value:D}");

    public static Option<NoteId> Parse(Uri? uri) =>
        uri is { IsAbsoluteUri: true } && uri.Scheme == Scheme && uri.Host == Host
        && Guid.TryParse(uri.AbsolutePath.Trim('/'), out var id) && id != Guid.Empty
            ? new Option<NoteId>.Some(new NoteId(id))
            : Option<NoteId>.Empty();

    // The column form: ids separated by commas, null when there are none.
    public static string? Join(IReadOnlyList<NoteId> ids) =>
        ids.Count == 0 ? null : string.Join(",", ids.Select(i => i.Value.ToString("D")));

    public static IReadOnlyList<NoteId> Split(string? stored) =>
        string.IsNullOrWhiteSpace(stored)
            ? []
            : stored.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => Guid.TryParse(s, out var id) ? id : Guid.Empty)
                .Where(id => id != Guid.Empty)
                .Distinct()
                .Select(id => new NoteId(id))
                .ToList();
}
