using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using NoteApp.Services.Export;
using static NoteApp.UiTests.Notes;

namespace NoteApp.UiTests;

public class EditorFormattingUiTests
{
    // The toolbar button makes a "1." list, and the exporters see one.
    [Fact]
    public void The_numbered_list_button_numbers_the_current_paragraph() => Wpf.Run(() =>
    {
        using var editor = new OpenEditor(With(Text("first", "second")));
        var box = editor.RichText();
        box.CaretPosition = box.Document.Blocks.FirstBlock.ContentStart;

        Click(editor, "Numbered List");

        var list = Assert.IsType<List>(box.Document.Blocks.FirstBlock);
        Assert.Equal(System.Windows.TextMarkerStyle.Decimal, list.MarkerStyle);

        EditorLinkUiTests.SyncAll(editor.View);
        var exported = RichTextDocument.Extract(editor.ViewModel.Blocks[0].RichTextContent);
        Assert.Equal(DocListMarker.Decimal, Assert.IsType<DocList>(exported[0]).Marker);
    });

    [Fact]
    public void Numbering_twice_takes_the_list_away_again() => Wpf.Run(() =>
    {
        using var editor = new OpenEditor(With(Text("only")));
        var box = editor.RichText();
        box.CaretPosition = box.Document.ContentStart;

        Click(editor, "Numbered List");
        Click(editor, "Numbered List");

        Assert.IsType<Paragraph>(box.Document.Blocks.FirstBlock);
    });

    // By the start of its tooltip: the rest may tell the keyboard shortcut.
    internal static void Click(OpenEditor editor, string toolTip)
    {
        var button = editor.Find<Button>().First(b => b.ToolTip is string tip && tip.StartsWith(toolTip, StringComparison.Ordinal));
        button.RaiseEvent(new System.Windows.RoutedEventArgs(ButtonBase.ClickEvent, button));
        Wpf.Pump();
    }
}

public class ChecklistUiTests
{
    [Fact]
    public void Hide_done_collapses_the_ticked_rows_only() => Wpf.Run(() =>
    {
        using var editor = new OpenEditor(Notes.With(new NoteApp.Domain.Models.NoteBlock.Checklist(
            [new("a", true), new("b", false)])));
        var rows = editor.Find<System.Windows.Controls.TextBox>().Where(t => t.Tag is NoteApp.ViewModels.ChecklistItemViewModel).ToList();
        var toggle = editor.Find<System.Windows.Controls.Primitives.ToggleButton>().Single(t => t.DataContext is NoteApp.ViewModels.BlockViewModel && Equals(t.Content, "Hide done"));
        Assert.True(toggle.IsVisible);

        editor.ViewModel.Blocks[0].HideDone = true;
        Wpf.Pump();

        Assert.False(rows[0].IsVisible);
        Assert.True(rows[1].IsVisible);
        Assert.Equal("Show done", toggle.Content);
        Assert.False(editor.ViewModel.IsDirty);
    });

    [Fact]
    public void Only_alt_with_an_arrow_moves_an_item()
    {
        Assert.Equal(-1, NoteApp.Views.NoteEditorView.ChecklistMoveOffset(System.Windows.Input.Key.Up, System.Windows.Input.ModifierKeys.Alt));
        Assert.Equal(1, NoteApp.Views.NoteEditorView.ChecklistMoveOffset(System.Windows.Input.Key.Down, System.Windows.Input.ModifierKeys.Alt));
        Assert.Null(NoteApp.Views.NoteEditorView.ChecklistMoveOffset(System.Windows.Input.Key.Up, System.Windows.Input.ModifierKeys.None));
        Assert.Null(NoteApp.Views.NoteEditorView.ChecklistMoveOffset(System.Windows.Input.Key.Left, System.Windows.Input.ModifierKeys.Alt));
    }

    [Fact]
    public void A_moved_item_keeps_the_focus_and_the_caret() => Wpf.Run(() =>
    {
        using var editor = new OpenEditor(Notes.With(Notes.Checklist("first", "second")));
        var second = editor.ViewModel.Blocks[0].ChecklistItems[1];

        editor.View.MoveChecklistItem(editor.ViewModel, second, -1, caretIndex: 3);
        Wpf.Pump();

        var box = editor.Find<System.Windows.Controls.TextBox>().First(t => t.Tag is NoteApp.ViewModels.ChecklistItemViewModel);
        Assert.Same(second, box.Tag);
        Assert.True(box.IsKeyboardFocusWithin || box.IsFocused);
        Assert.Equal(3, box.CaretIndex);
    });
}
