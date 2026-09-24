using NoteApp.Domain.Models;
using NoteApp.ViewModels;

namespace NoteApp.Tests.ViewModels;

public class ChecklistExtrasTests
{
    [Fact]
    public void Hiding_the_done_items_is_not_an_edit()
    {
        var vm = Editor(Checklist(("a", true), ("b", false)));

        vm.Blocks[0].HideDone = true;

        Assert.False(vm.IsDirty);
    }

    [Fact]
    public void HasDone_follows_the_ticks()
    {
        var vm = Editor(Checklist(("a", false)));
        var block = vm.Blocks[0];
        var raised = new List<string?>();
        block.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        Assert.False(block.HasDone);
        block.ChecklistItems[0].IsDone = true;

        Assert.True(block.HasDone);
        Assert.Contains(nameof(BlockViewModel.HasDone), raised);
    }

    [Theory]
    [InlineData(1, -1, new[] { "b", "a", "c" })]
    [InlineData(1, 1, new[] { "a", "c", "b" })]
    public void An_item_moves_one_row(int index, int offset, string[] expected)
    {
        var vm = Editor(Checklist(("a", false), ("b", false), ("c", false)));
        var items = vm.Blocks[0].ChecklistItems;

        Assert.True(vm.MoveChecklistItem(items[index], offset));

        Assert.Equal(expected, items.Select(i => i.Text));
        Assert.True(vm.IsDirty);
    }

    [Fact]
    public void The_first_item_cannot_go_higher()
    {
        var vm = Editor(Checklist(("a", false), ("b", false)));

        Assert.False(vm.MoveChecklistItem(vm.Blocks[0].ChecklistItems[0], -1));
        Assert.False(vm.IsDirty);
    }

    // With the done ones hidden, the move is over what the user can see.
    [Fact]
    public void Hidden_done_items_are_hopped_over()
    {
        var vm = Editor(Checklist(("a", false), ("done", true), ("c", false)));
        var block = vm.Blocks[0];
        block.HideDone = true;

        Assert.True(vm.MoveChecklistItem(block.ChecklistItems[2], -1));

        Assert.Equal(["c", "a", "done"], block.ChecklistItems.Select(i => i.Text));
    }

    private static NoteBlock.Checklist Checklist(params (string Text, bool Done)[] items) =>
        new(items.Select(i => new ChecklistItem(i.Text, i.Done)).ToList());

    private static NoteEditorViewModel Editor(NoteBlock block) =>
        new(new NoteApp.Services.NoteService(null!), null!, [], SampleNote(block));
}
