using System.IO;
using System.Windows.Documents;
using NoteApp.Domain.Models;
using NoteApp.ViewModels;
using static NoteApp.UiTests.Notes;

namespace NoteApp.UiTests;

// The view's drop entry points (a DragEventArgs cannot be built outside WPF itself).
public class EditorDragDropUiTests
{
    // The dragged card is rebuilt and its RichTextBox reloads from the block: text typed
    // since the last sync has to be in the block by then.
    [Fact]
    public void A_dragged_text_block_keeps_what_was_just_typed_in_it() => Wpf.Run(() =>
    {
        using var editor = new OpenEditor(With(Text("first"), Link("https://example.com")));
        var box = editor.RichText();
        box.CaretPosition = box.Document.ContentEnd;
        editor.Type(box, " typed");
        var text = editor.ViewModel.Blocks[0];

        editor.View.DropBlock(text, editor.ViewModel.Blocks[1], below: true);
        Wpf.Pump();

        Assert.Equal([BlockType.Link, BlockType.Text], editor.ViewModel.Blocks.Select(b => b.BlockType));
        var reloaded = editor.RichText();
        Assert.Contains("first typed", new TextRange(reloaded.Document.ContentStart, reloaded.Document.ContentEnd).Text);
    });

    [Fact]
    public void Dropping_on_the_upper_half_puts_the_block_above() => Wpf.Run(() =>
    {
        using var editor = new OpenEditor(With(Link("https://a.com"), Link("https://b.com"), Link("https://c.com")));
        var blocks = editor.ViewModel.Blocks;

        editor.View.DropBlock(blocks[2], blocks[0], below: false);

        Assert.Equal(["https://c.com/", "https://a.com/", "https://b.com/"], blocks.Select(b => b.LinkUrlText));
        Assert.True(editor.ViewModel.IsDirty);
    });

    [Fact]
    public void Every_block_has_a_grip() => Wpf.Run(() =>
    {
        using var editor = new OpenEditor(With(Text("t"), Checklist("c"), Link("https://l.com")));

        var grips = editor.Find<System.Windows.Controls.Border>().Where(b => Equals(b.ToolTip, "Drag to move this block")).ToList();

        Assert.Equal(editor.ViewModel.Blocks, grips.Select(g => (BlockViewModel)g.DataContext));
    });

    [Fact]
    public void Dropped_files_are_attached_as_file_blocks() => Wpf.Run(() =>
    {
        var path = Path.Combine(Path.GetTempPath(), $"noteapp-drop-{Guid.NewGuid()}.txt");
        File.WriteAllText(path, "hello");
        try
        {
            using var editor = new OpenEditor(With(Text("t")));

            editor.View.DropFiles([path]);
            Wpf.Pump();

            var file = editor.ViewModel.Blocks.Last();
            Assert.Equal(BlockType.File, file.BlockType);
            Assert.Equal(Path.GetFileName(path), file.FileName);
        }
        finally
        {
            File.Delete(path);
        }
    });
}
