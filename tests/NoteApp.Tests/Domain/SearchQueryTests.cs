using NoteApp.Domain.Models;
using NoteApp.Services;

namespace NoteApp.Tests.Domain;

public class SearchQueryTests
{
    [Fact]
    public void Words_phrases_and_operators_are_told_apart()
    {
        var query = SearchQuery.Parse("""milk "exact phrase" tag:work tag:"two words" has:file has:Tasks is:pinned is:encrypted""");

        Assert.Equal(["milk", "exact phrase"], query.Terms);
        Assert.Equal(["work", "two words"], query.TagNames);
        Assert.Equal([BlockType.File, BlockType.Checklist], query.Has);
        Assert.True(query.PinnedOnly);
        Assert.True(query.EncryptedOnly);
    }

    // Anything that only looks like an operator is searched for as it is.
    [Theory]
    [InlineData("has:nothing", "has:nothing")]
    [InlineData("is:happy", "is:happy")]
    [InlineData("tag:", "tag:")]
    [InlineData("http://example.com", "http://example.com")]
    [InlineData("\"has:file\"", "has:file")]
    public void An_unknown_operator_is_text(string text, string term)
    {
        var query = SearchQuery.Parse(text);

        Assert.Equal([term], query.Terms);
        Assert.Empty(query.Has);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Nothing_typed_is_the_empty_query(string? text) => Assert.True(SearchQuery.Parse(text).IsEmpty);

    [Fact]
    public void An_unclosed_quote_runs_to_the_end() =>
        Assert.Equal(["a", "b c"], SearchQuery.Parse("a \"b c").Terms);

    [Fact]
    public void Every_block_kind_has_an_operator_name()
    {
        var query = SearchQuery.Parse("has:text has:link has:code has:secret has:password");

        Assert.Equal([BlockType.Text, BlockType.Link, BlockType.Code, BlockType.Secret], query.Has);
    }

    [Fact]
    public void Matches_are_cut_out_of_a_line_whatever_their_case()
    {
        Assert.Equal(
            [("Buy ", false), ("MILK", true), (" and ", false), ("milk", true), ("shake", false)],
            TextHighlights.Split("Buy MILK and milkshake", ["milk"]));
        Assert.Equal([("abc", true)], TextHighlights.Split("abc", ["ab", "bc"]));
        Assert.Equal([("abc", false)], TextHighlights.Split("abc", []));
        Assert.Empty(TextHighlights.Split("", ["a"]));
    }

    [Fact]
    public void A_preview_starts_near_a_match_it_would_otherwise_cut_off()
    {
        var text = string.Join(" ", Enumerable.Repeat("filler", 30)) + " the needle is here";

        var snippet = RichTextPreview.Snippet(text, null, around: "needle");

        Assert.StartsWith("…", snippet);
        Assert.Contains("needle", snippet);
        Assert.Equal(RichTextPreview.Snippet("short needle", null), RichTextPreview.Snippet("short needle", null, around: "needle"));
    }
}
