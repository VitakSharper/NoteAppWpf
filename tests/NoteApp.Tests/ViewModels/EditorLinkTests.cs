using NoteApp.Domain.Models;
using NoteApp.ViewModels;

namespace NoteApp.Tests.ViewModels;

// The open buttons and the link styling of a checklist row are bound to these flags, so
// each has to be raised whenever the text it is computed from changes.
public class EditorLinkTests
{
    [Fact]
    public void A_checklist_item_follows_its_text()
    {
        var item = new ChecklistItemViewModel();
        var raised = new List<string?>();
        item.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        item.Text = "https://example.com";

        Assert.True(item.HasLink);
        Assert.True(item.IsLink);
        Assert.Contains(nameof(ChecklistItemViewModel.HasLink), raised);
        Assert.Contains(nameof(ChecklistItemViewModel.IsLink), raised);

        item.Text = "read https://example.com tonight";

        Assert.True(item.HasLink);
        Assert.False(item.IsLink);

        item.Text = "buy milk";

        Assert.False(item.HasLink);
        Assert.False(item.IsLink);
    }

    // Same rule as the save: an address the block could not store cannot be opened either.
    [Theory]
    [InlineData("https://example.com", true)]
    [InlineData("www.example.com", false)]
    [InlineData("file:///C:/Windows/notepad.exe", false)]
    [InlineData("", false)]
    public void A_link_block_opens_only_a_valid_address(string url, bool expected)
    {
        var block = new BlockViewModel { BlockType = BlockType.Link };
        var raised = new List<string?>();
        block.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        block.LinkUrlText = url;

        Assert.Equal(expected, block.CanOpenLink);
        if (url.Length > 0)
            Assert.Contains(nameof(BlockViewModel.CanOpenLink), raised);
    }
}
