using NoteApp.Services;

namespace NoteApp.Tests.Services;

// Only the pure paths: decoding a XamlPackage needs WPF on an STA thread.
public class RichTextPreviewTests
{
    [Fact]
    public void Snippet_PrefersTheStoredPlainText_AndCollapsesWhitespace() =>
        Assert.Equal("hello world", RichTextPreview.Snippet("  hello\r\n\t world  ", richText: "<ignored/>"));

    [Fact]
    public void Snippet_TruncatesAt120CharactersWithAnEllipsis()
    {
        var snippet = RichTextPreview.Snippet(new string('x', 200), null);

        Assert.Equal(121, snippet.Length);
        Assert.EndsWith("…", snippet);
    }

    [Fact]
    public void Snippet_KeepsShortTextIntact() =>
        Assert.Equal(new string('x', 120), RichTextPreview.Snippet(new string('x', 120), null));

    [Theory]
    [InlineData(null, null)]
    [InlineData(null, "")]
    [InlineData("", null)]
    public void Snippet_IsEmptyWhenThereIsNothingToShow(string? plain, string? rich) =>
        Assert.Equal(string.Empty, RichTextPreview.Snippet(plain, rich));
}
