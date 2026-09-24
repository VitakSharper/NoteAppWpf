using NoteApp.Domain.Functional;
using NoteApp.Domain.ValueObjects;
using NoteApp.Services;

namespace NoteApp.Tests.Services;

// What the editor turns into a clickable link. Every address found here ends up handed
// to the shell, so what does not count matters as much as what does.
public class TextLinksTests
{
    [Fact]
    public void Finds_an_address_in_the_middle_of_a_sentence()
    {
        const string text = "docs at https://example.com/page today";

        var link = Assert.Single(TextLinks.Find(text));

        Assert.Equal("https://example.com/page", text.Substring(link.Start, link.Length));
        Assert.Equal(new Uri("https://example.com/page"), link.Url.Value);
    }

    [Fact]
    public void Finds_every_address_in_order()
    {
        var links = TextLinks.Find("http://a.com and https://b.com");

        Assert.Equal(["http://a.com/", "https://b.com/"], links.Select(l => l.Url.Value.AbsoluteUri));
    }

    [Fact]
    public void An_address_without_a_scheme_opens_over_https()
    {
        var link = Assert.Single(TextLinks.Find("see www.example.com"));

        Assert.Equal("www.example.com".Length, link.Length);
        Assert.Equal(new Uri("https://www.example.com"), link.Url.Value);
    }

    // Sentence punctuation after an address belongs to the sentence.
    [Theory]
    [InlineData("Go to https://example.com.", "https://example.com")]
    [InlineData("https://example.com, then", "https://example.com")]
    [InlineData("\"https://example.com/a?b=c\"", "https://example.com/a?b=c")]
    [InlineData("(see https://example.com)", "https://example.com")]
    [InlineData("(see https://en.wikipedia.org/wiki/Foo_(bar))", "https://en.wikipedia.org/wiki/Foo_(bar)")]
    public void Trailing_punctuation_is_left_out(string text, string expected)
    {
        var link = Assert.Single(TextLinks.Find(text));

        Assert.Equal(expected, text.Substring(link.Start, link.Length));
    }

    // Only HTTP and HTTPS reach the shell (LinkUrl), and half an address is no address.
    [Theory]
    [InlineData("")]
    [InlineData("no address here")]
    [InlineData("file:///C:/Windows/notepad.exe")]
    [InlineData("ftp://example.com")]
    [InlineData("javascript:alert(1)")]
    [InlineData("https://")]
    [InlineData("www.")]
    [InlineData("xhttps://example.com")]
    public void Finds_nothing_that_is_not_a_web_address(string text) =>
        Assert.Empty(TextLinks.Find(text));

    [Fact]
    public void At_resolves_the_character_under_the_pointer()
    {
        const string text = "a https://example.com b";

        Assert.True(TextLinks.At(text, text.IndexOf('h')).IsSome);
        Assert.True(TextLinks.At(text, text.IndexOf(".com", StringComparison.Ordinal) + 3).IsSome);
        Assert.True(TextLinks.At(text, 0).IsNone);
        Assert.True(TextLinks.At(text, text.Length - 1).IsNone);
    }

    [Fact]
    public void First_is_the_leftmost_address() =>
        Assert.Equal(new Uri("https://a.com"), Url(TextLinks.First("x https://a.com y https://b.com")));

    [Theory]
    [InlineData("https://example.com", true)]
    [InlineData("  https://example.com  ", true)]
    [InlineData("www.example.com", true)]
    [InlineData("read https://example.com", false)]
    [InlineData("https://a.com https://b.com", false)]
    [InlineData("", false)]
    public void IsLink_only_when_the_address_is_all_there_is(string text, bool expected) =>
        Assert.Equal(expected, TextLinks.IsLink(text));

    private static Uri Url(Option<LinkUrl> url) =>
        url.Match(some: u => u.Value, none: () => throw new Xunit.Sdk.XunitException("no link found"));
}
