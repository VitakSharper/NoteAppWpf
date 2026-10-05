using NoteApp.Domain.Models;
using NoteApp.Services;
using static NoteApp.Domain.Models.ChecklistItemState;

namespace NoteApp.Tests.Services;

// One format for the NoteBlocks.ChecklistJson column and for the encrypted blob, so
// its shape is worth pinning: a stored note has to survive the next release.
public class ChecklistJsonTests
{
    [Fact]
    public void Round_trips_items_in_order()
    {
        IReadOnlyList<ChecklistItem> items =
            [new ChecklistItem("buy milk", Done), new ChecklistItem("call the bank", InProgress, Note: "ask for\nthe advisor"), new ChecklistItem("pay rent", Todo)];

        var restored = ChecklistJson.Deserialize(ChecklistJson.Serialize(items));

        Assert.Equal(items, restored);
    }

    // Short keys, and "d" / "p" left out when false: the column carries whole lists.
    [Fact]
    public void Writes_short_keys_and_omits_unchecked_items()
    {
        var json = ChecklistJson.Serialize(
            [new ChecklistItem("buy milk", Done), new ChecklistItem("call the bank", InProgress), new ChecklistItem("pay rent", Todo)]);

        Assert.Equal("""[{"t":"buy milk","d":true},{"t":"call the bank","p":true},{"t":"pay rent"}]""", json);
    }

    // "n" holds the item's note, left out when there is none.
    [Fact]
    public void A_note_is_written_under_n_only_when_there_is_one()
    {
        var json = ChecklistJson.Serialize([new ChecklistItem("call the bank", Todo, Note: "ask for the advisor"), new ChecklistItem("pay rent", Todo)]);

        Assert.Equal("""[{"t":"call the bank","n":"ask for the advisor"},{"t":"pay rent"}]""", json);
    }

    // Columns written before "p" existed hold only "d": they read as they always did.
    [Fact]
    public void A_list_without_progress_reads_as_done_or_todo()
    {
        var items = ChecklistJson.Deserialize("""[{"t":"a","d":true},{"t":"b"}]""");

        Assert.Equal([Done, Todo], items.Select(i => i.State));
        Assert.All(items, i => Assert.Equal(string.Empty, i.Note));
    }

    // A hand-edited row may carry both; done is the stronger claim.
    [Fact]
    public void Done_wins_over_in_progress()
    {
        var item = Assert.Single(ChecklistJson.Deserialize("""[{"t":"a","d":true,"p":true}]"""));

        Assert.Equal(Done, item.State);
    }

    [Fact]
    public void An_empty_list_is_an_empty_array()
    {
        Assert.Equal("[]", ChecklistJson.Serialize([]));
        Assert.Empty(ChecklistJson.Deserialize("[]"));
    }

    // A hand-edited or truncated column costs its checklist, never the whole note.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not json")]
    [InlineData("""[{"t":"half""")]
    public void Unreadable_content_yields_an_empty_checklist(string? json) =>
        Assert.Empty(ChecklistJson.Deserialize(json));

    [Fact]
    public void A_missing_text_field_reads_as_an_empty_item()
    {
        var items = ChecklistJson.Deserialize("""[{"d":true}]""");

        var item = Assert.Single(items);
        Assert.Equal(string.Empty, item.Text);
        Assert.True(item.IsDone);
    }
}
