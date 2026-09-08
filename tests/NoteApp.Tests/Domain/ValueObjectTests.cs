using NoteApp.Domain.ValueObjects;

namespace NoteApp.Tests.Domain;

public class ValueObjectTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NoteTitle_RejectsBlank(string? value) =>
        Assert.True(NoteTitle.From(value).IsFailure);

    [Fact]
    public void NoteTitle_TrimsAndKeepsTheText() =>
        Assert.Equal("Hello", NoteTitle.From("  Hello  ").Unwrap().Value);

    [Fact]
    public void NoteTitle_RejectsMoreThan500Characters()
    {
        Assert.True(NoteTitle.From(new string('a', 500)).IsSuccess);
        Assert.True(NoteTitle.From(new string('a', 501)).IsFailure);
    }

    [Fact]
    public void TagName_NormalizesToLowerCaseAndTrims() =>
        Assert.Equal("work", TagName.From("  WoRk ").Unwrap().Value);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void TagName_RejectsBlank(string? value) =>
        Assert.True(TagName.From(value).IsFailure);

    [Fact]
    public void TagName_RejectsMoreThan100Characters() =>
        Assert.True(TagName.From(new string('t', 101)).IsFailure);

    [Theory]
    [InlineData("https://example.com")]
    [InlineData("http://example.com/path?q=1")]
    [InlineData("  https://example.com  ")]
    public void LinkUrl_AcceptsHttpAndHttps(string value) =>
        Assert.True(LinkUrl.From(value).IsSuccess);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ftp://example.com")]
    [InlineData("example.com")]
    [InlineData("not a url")]
    public void LinkUrl_RejectsEverythingElse(string? value) =>
        Assert.True(LinkUrl.From(value).IsFailure);

    [Fact]
    public void NoteId_RejectsEmptyGuid_AndNewIsNeverEmpty()
    {
        Assert.True(NoteId.From(Guid.Empty).IsFailure);
        Assert.NotEqual(Guid.Empty, NoteId.New().Value);
    }
}
