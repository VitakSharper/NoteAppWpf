namespace NoteApp.Domain.Models;

// Which notes a query looks at. AllLive is every note that is not in the trash, archived or
// not — what a link picker or the quick switcher offers.
public enum NoteShelf
{
    Active,
    Archived,
    Trash,
    AllLive
}

// What the list (or a picker) asks storage for: the search box once parsed (SearchQuery),
// the tag chips and the type chip. Every term, tag name and block type has to match; the
// tag chips keep their "any of these" meaning.
public sealed record NoteQuery(
    NoteShelf Shelf = NoteShelf.Active,
    IReadOnlyList<string>? Terms = null,
    IReadOnlyList<string>? TagNames = null,
    IReadOnlyList<Guid>? AnyOfTagIds = null,
    IReadOnlyList<BlockType>? MustHave = null,
    bool PinnedOnly = false,
    bool EncryptedOnly = false)
{
    public IReadOnlyList<string> AllTerms => Terms ?? [];
    public IReadOnlyList<string> AllTagNames => TagNames ?? [];
    public IReadOnlyList<Guid> AllAnyOfTagIds => AnyOfTagIds ?? [];
    public IReadOnlyList<BlockType> AllMustHave => MustHave ?? [];
}
