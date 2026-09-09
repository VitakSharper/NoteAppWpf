using NoteApp.Domain.Models;
using NoteApp.Services;

namespace NoteApp.Tests.Services;

// One format for the NoteBlocks.ChecklistJson column and for the encrypted blob, so
// its shape is worth pinning: a stored note has to survive the next release.
public class ChecklistJsonTests
{
    [Fact]
    public void Round_trips_items_in_order()
    {
        IReadOnlyList<ChecklistItem> items =
            [new ChecklistItem("buy milk", true), new ChecklistItem("call the bank", false)];

        var restored = ChecklistJson.Deserialize(ChecklistJson.Serialize(items));

        Assert.Equal(items, restored);
    }

    // Short keys, and "d" left out when false: the column carries whole lists.
    [Fact]
    public void Writes_short_keys_and_omits_unchecked_items()
    {
        var json = ChecklistJson.Serialize([new ChecklistItem("buy milk", true), new ChecklistItem("call the bank", false)]);

        Assert.Equal("""[{"t":"buy milk","d":true},{"t":"call the bank"}]""", json);
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
