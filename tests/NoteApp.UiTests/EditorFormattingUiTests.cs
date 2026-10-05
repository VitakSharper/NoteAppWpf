using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using NoteApp.Domain.Models;
using NoteApp.Services.Export;
using NoteApp.Views;
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
            [new("a", ChecklistItemState.Done), new("b", ChecklistItemState.Todo), new("c", ChecklistItemState.InProgress)])));
        var rows = editor.Find<System.Windows.Controls.TextBox>().Where(t => t.Tag is NoteApp.ViewModels.ChecklistItemViewModel).ToList();
        var toggle = editor.Find<System.Windows.Controls.Primitives.ToggleButton>().Single(t => t.DataContext is NoteApp.ViewModels.BlockViewModel && Equals(t.Content, "Hide done"));
        Assert.True(toggle.IsVisible);

        editor.ViewModel.Blocks[0].HideDone = true;
        Wpf.Pump();

        Assert.False(rows[0].IsVisible);
        Assert.True(rows[1].IsVisible);
        Assert.True(rows[2].IsVisible); // started is not done
        Assert.Equal("Show done", toggle.Content);
        Assert.False(editor.ViewModel.IsDirty);
    });

    // Through the automation peer, so it is the box's own toggle that runs, as on a click.
    [Fact]
    public void Clicking_the_box_goes_from_not_started_to_in_progress_to_done_and_back() => Wpf.Run(() =>
    {
        using var editor = new OpenEditor(Notes.With(Notes.Checklist("draft the plan")));
        var item = editor.ViewModel.Blocks[0].ChecklistItems[0];
        var box = editor.Find<TriStateCheckBox>().Single();
        var toggle = (IToggleProvider)new CheckBoxAutomationPeer(box);

        var seen = new List<(ChecklistItemState, bool?)>();
        for (var i = 0; i < 3; i++)
        {
            toggle.Toggle();
            Wpf.Pump();
            seen.Add((item.State, box.IsChecked));
        }

        Assert.Equal(
            [(ChecklistItemState.InProgress, null), (ChecklistItemState.Done, true), (ChecklistItemState.Todo, false)],
            seen);
        Assert.True(editor.ViewModel.IsDirty);
        Assert.Equal("0/1 done", editor.ViewModel.Blocks[0].ChecklistSummary);
    });

    // A state set from the model (a restored draft, a loaded note) shows in the box.
    [Fact]
    public void The_box_shows_the_items_state() => Wpf.Run(() =>
    {
        using var editor = new OpenEditor(Notes.With(new NoteApp.Domain.Models.NoteBlock.Checklist(
            [new("a", ChecklistItemState.Done), new("b", ChecklistItemState.Todo), new("c", ChecklistItemState.InProgress)])));

        Assert.Equal([true, false, null], editor.Find<TriStateCheckBox>().Select(b => b.IsChecked));
        Assert.Equal("1/3 done · 1 in progress", editor.ViewModel.Blocks[0].ChecklistSummary);
    });

    [Fact]
    public void The_note_button_opens_a_field_under_the_item_and_an_empty_one_closes_when_left() => Wpf.Run(() =>
    {
        using var editor = new OpenEditor(Notes.With(Notes.Checklist("call the bank")));
        var item = editor.ViewModel.Blocks[0].ChecklistItems[0];
        var note = NoteBox(editor);
        Assert.False(note.IsVisible);
        Assert.Equal(MaterialDesignThemes.Wpf.PackIconKind.CommentPlusOutline, NoteIcon(editor));

        EditorFormattingUiTests.Click(editor, "Add a note");

        Assert.True(note.IsVisible);
        Assert.True(note.IsKeyboardFocusWithin || note.IsFocused);
        Assert.Equal(ChecklistItem.MaxNoteLength, note.MaxLength);
        Assert.False(editor.ViewModel.IsDirty);

        editor.Find<TextBox>().Single(t => ReferenceEquals(t.Tag, item)).Focus();
        Wpf.Pump();

        Assert.False(note.IsVisible);
    });

    [Fact]
    public void A_note_stays_under_its_item_and_its_field_stays_while_it_is_emptied() => Wpf.Run(() =>
    {
        using var editor = new OpenEditor(Notes.With(new NoteApp.Domain.Models.NoteBlock.Checklist(
            [new("call the bank", ChecklistItemState.Todo, Note: "ask for the advisor")])));
        var note = NoteBox(editor);
        Assert.True(note.IsVisible);
        Assert.Equal("ask for the advisor", note.Text);
        Assert.Equal(MaterialDesignThemes.Wpf.PackIconKind.CommentText, NoteIcon(editor));

        note.Focus();
        Wpf.Pump();
        note.Clear();
        Wpf.Pump();

        Assert.True(note.IsVisible);
        Assert.True(editor.ViewModel.IsDirty);
    });

    [Fact]
    public void Only_shift_enter_in_an_item_opens_its_note()
    {
        Assert.True(NoteEditorView.OpensItemNote(System.Windows.Input.Key.Enter, System.Windows.Input.ModifierKeys.Shift));
        Assert.False(NoteEditorView.OpensItemNote(System.Windows.Input.Key.Enter, System.Windows.Input.ModifierKeys.None));
        Assert.False(NoteEditorView.OpensItemNote(System.Windows.Input.Key.Tab, System.Windows.Input.ModifierKeys.Shift));
    }

    // The note field: it carries no Tag, so the code that finds an item's own box never takes it.
    private static TextBox NoteBox(OpenEditor editor) =>
        editor.Find<TextBox>().Single(t => t.Name == "ItemNote");

    private static MaterialDesignThemes.Wpf.PackIconKind NoteIcon(OpenEditor editor) =>
        Wpf.Descendants<MaterialDesignThemes.Wpf.PackIcon>(editor.Find<Button>().Single(b => b.ToolTip is string tip && (tip.StartsWith("Add a note") || tip == "Edit the note"))).Single().Kind;

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
