using NoteApp.Domain.ValueObjects;

namespace NoteApp.Domain.Models;

// Due: when to be reminded of the item (UTC), if ever.
public sealed record ChecklistItem(string Text, bool IsDone, DateTime? Due = null);

public abstract record NoteBlock
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public int SortOrder { get; init; }

    // PlainText is the searchable / previewable form of RichText (a Base64
    // XamlPackage). It is computed by the editor, which has the document.
    // NoteLinks: the notes its text links to (Services/NoteLinks), computed by the editor
    // like PlainText, and what the other notes' "Linked from" is looked up by.
    public sealed record Text(string RichText, string PlainText = "") : NoteBlock
    {
        public IReadOnlyList<NoteId> NoteLinks { get; init; } = [];
    }

    public sealed record File(
        byte[] Data,
        string FileName,
        string Extension,
        long SizeBytes) : NoteBlock;

    public sealed record Link(
        LinkUrl Url,
        string Description) : NoteBlock;

    public sealed record Checklist(IReadOnlyList<ChecklistItem> Items) : NoteBlock
    {
        public int DoneCount => Items.Count(i => i.IsDone);

        // Same role as Text.PlainText: this is what search and the list preview read,
        // so a checklist is findable by the words in its items.
        public string PlainText => string.Join("\n", Items.Select(i => i.Text));
    }

    // A login: what it is for, the user name, the password and, optionally, where it is
    // used. The password is never searched, never previewed and never exported; the rest
    // is PlainText, so a secret is found by its label, its user name or its address.
    public sealed record Secret(string Label, string UserName, string Password, string Url = "") : NoteBlock
    {
        public string PlainText => string.Join("\n", new[] { Label, UserName, Url }.Where(s => s.Length > 0));
    }

    // Monospace text kept exactly as typed — commands, SQL, configuration — never turned
    // into links or reformatted. Its text is its PlainText, so search finds it.
    public sealed record Code(string Content) : NoteBlock;

    public BlockType Type => this switch
    {
        Text => BlockType.Text,
        File => BlockType.File,
        Link => BlockType.Link,
        Checklist => BlockType.Checklist,
        Secret => BlockType.Secret,
        Code => BlockType.Code,
        _ => throw new InvalidOperationException($"Unknown block type: {GetType().Name}")
    };

    public TResult Match<TResult>(
        Func<Text, TResult> text,
        Func<File, TResult> file,
        Func<Link, TResult> link,
        Func<Checklist, TResult> checklist,
        Func<Secret, TResult> secret,
        Func<Code, TResult> code) =>
        this switch
        {
            Text t => text(t),
            File f => file(f),
            Link l => link(l),
            Checklist c => checklist(c),
            Secret s => secret(s),
            Code c => code(c),
            _ => throw new InvalidOperationException($"Unknown block type: {GetType().Name}")
        };
}

public enum BlockType
{
    Text = 0,
    File = 1,
    Link = 2,
    Checklist = 3,
    Secret = 4,
    Code = 5
}
