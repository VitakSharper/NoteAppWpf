using NoteApp.Domain.ValueObjects;

namespace NoteApp.Domain.Models;

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

    public BlockType Type => this switch
    {
        Text => BlockType.Text,
        File => BlockType.File,
        Link => BlockType.Link,
        _ => throw new InvalidOperationException($"Unknown block type: {GetType().Name}")
    };

    public TResult Match<TResult>(
        Func<Text, TResult> text,
        Func<File, TResult> file,
        Func<Link, TResult> link) =>
        this switch
        {
            Text t => text(t),
            File f => file(f),
            Link l => link(l),
            _ => throw new InvalidOperationException($"Unknown block type: {GetType().Name}")
        };
}

public enum BlockType
{
    Text = 0,
    File = 1,
    Link = 2
}
