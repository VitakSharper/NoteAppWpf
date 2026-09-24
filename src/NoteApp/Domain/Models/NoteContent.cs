using NoteApp.Domain.ValueObjects;

namespace NoteApp.Domain.Models;

public sealed record ChecklistItem(string Text, bool IsDone);

public abstract record NoteBlock
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public int SortOrder { get; init; }

    // PlainText is the searchable / previewable form of RichText (a Base64
    // XamlPackage). It is computed by the editor, which has the document.
    public sealed record Text(string RichText, string PlainText = "") : NoteBlock;

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

    public BlockType Type => this switch
    {
        Text => BlockType.Text,
        File => BlockType.File,
        Link => BlockType.Link,
        Checklist => BlockType.Checklist,
        Secret => BlockType.Secret,
        _ => throw new InvalidOperationException($"Unknown block type: {GetType().Name}")
    };

    public TResult Match<TResult>(
        Func<Text, TResult> text,
        Func<File, TResult> file,
        Func<Link, TResult> link,
        Func<Checklist, TResult> checklist,
        Func<Secret, TResult> secret) =>
        this switch
        {
            Text t => text(t),
            File f => file(f),
            Link l => link(l),
            Checklist c => checklist(c),
            Secret s => secret(s),
            _ => throw new InvalidOperationException($"Unknown block type: {GetType().Name}")
        };
}

public enum BlockType
{
    Text = 0,
    File = 1,
    Link = 2,
    Checklist = 3,
    Secret = 4
}
