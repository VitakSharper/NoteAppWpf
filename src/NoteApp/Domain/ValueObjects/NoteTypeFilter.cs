using NoteApp.Domain.Models;

namespace NoteApp.Domain.ValueObjects;

public sealed record NoteTypeFilter(string Label, BlockType? Value)
{
    public static readonly NoteTypeFilter All = new("All", null);
    public static readonly NoteTypeFilter TextOnly = new("Text", BlockType.Text);
    public static readonly NoteTypeFilter FileOnly = new("File", BlockType.File);
    public static readonly NoteTypeFilter LinkOnly = new("Link", BlockType.Link);
    public static readonly NoteTypeFilter ChecklistOnly = new("Tasks", BlockType.Checklist);

    public static IReadOnlyList<NoteTypeFilter> AllFilters =>
        [All, TextOnly, FileOnly, LinkOnly, ChecklistOnly];
}
