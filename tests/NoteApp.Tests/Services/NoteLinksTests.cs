using NoteApp.Domain.Functional;
using NoteApp.Domain.ValueObjects;
using NoteApp.Services;

namespace NoteApp.Tests.Services;

public class NoteLinksTests
{
    [Fact]
    public void A_note_link_address_names_the_note_and_reads_back()
    {
        var id = NoteId.New();

        var uri = NoteLinks.UriFor(id);

        Assert.Equal($"noteapp://note/{id.Value:D}", uri.ToString());
        Assert.Equal(id, Assert.IsType<Option<NoteId>.Some>(NoteLinks.Parse(uri)).Value);
    }

    [Theory]
    [InlineData("https://example.com/")]
    [InlineData("noteapp://tag/0f8fad5b-d9cb-469f-a165-70867728950e")]
    [InlineData("noteapp://note/not-a-guid")]
    [InlineData("noteapp://note/00000000-0000-0000-0000-000000000000")]
    public void Anything_else_is_not_a_note_link(string address) =>
        Assert.True(NoteLinks.Parse(new Uri(address)).IsNone);

    [Fact]
    public void No_address_is_not_a_note_link() => Assert.True(NoteLinks.Parse(null).IsNone);

    [Fact]
    public void The_stored_list_round_trips_and_forgives_junk()
    {
        var a = NoteId.New();
        var b = NoteId.New();

        Assert.Null(NoteLinks.Join([]));
        Assert.Equal([a, b], NoteLinks.Split(NoteLinks.Join([a, b])));
        Assert.Equal([a], NoteLinks.Split($" {a.Value}, junk,{a.Value} "));
        Assert.Empty(NoteLinks.Split(null));
    }
}
