using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using NoteApp.Domain.Models;
using NoteApp.Services;
using NoteApp.ViewModels;
using NoteApp.Views;
using static NoteApp.UiTests.Notes;

namespace NoteApp.UiTests;

// Clickable links, driven through the real editor: when an address becomes a Hyperlink,
// what a click on it resolves to, and that neither loading nor undo misbehaves.
public class EditorLinkUiTests
{
    private static readonly Color Accent = (Color)ColorConverter.ConvertFromString("#6C79FF");

    [Fact]
    public void Addresses_in_a_stored_note_are_linked_on_load_without_marking_it_modified() => Wpf.Run(() =>
    {
        using var editor = new OpenEditor(With(Text("Intro", "https://old.example.com", "see www.two.com, ok")));

        var links = Hyperlinks(editor.RichText().Document);

        Assert.Equal(["https://old.example.com", "www.two.com"], links.Select(TextOf));
        Assert.False(editor.ViewModel.IsDirty);
        Assert.All(links, l => Assert.Null(l.NavigateUri));
    });

    [Fact]
    public void A_link_is_accent_coloured_underlined_and_says_how_to_open_it() => Wpf.Run(() =>
    {
        using var editor = new OpenEditor(With(Text("https://example.com")));

        var link = Assert.Single(Hyperlinks(editor.RichText().Document));

        Assert.Equal(Accent, Assert.IsType<SolidColorBrush>(link.Foreground).Color);
        Assert.NotEmpty(link.TextDecorations);
        Assert.Equal("Click to open · Alt+Click to edit", link.ToolTip);
    });

    [Fact]
    public void A_click_resolves_to_the_link_under_it_and_to_nothing_beside_it() => Wpf.Run(() =>
    {
        using var editor = new OpenEditor(With(Text("ab https://old.example.com cd")));
        var box = editor.RichText();
        var link = Assert.Single(Hyperlinks(box.Document));
        var run = (Run)link.Inlines.FirstInline;

        Assert.Equal("https://old.example.com/", Resolve(box, run, 0));
        Assert.Equal("https://old.example.com/", Resolve(box, run, run.Text.Length - 1));

        var before = (Run)((Paragraph)box.Document.Blocks.FirstBlock).Inlines.FirstInline;
        Assert.Equal("none", Resolve(box, before, 0));
    });

    [Fact]
    public void A_typed_address_becomes_a_link_once_a_space_follows_it() => Wpf.Run(() =>
    {
        using var editor = new OpenEditor(With(Text("start")));
        var box = editor.RichText();
        box.CaretPosition = box.Document.ContentEnd;

        editor.Type(box, " go https://typed.example.com");

        Assert.True(editor.ViewModel.IsDirty);
        Assert.DoesNotContain(Hyperlinks(box.Document), h => TextOf(h).Contains("typed"));

        editor.Type(box, " ");

        Assert.Contains(Hyperlinks(box.Document), h => TextOf(h) == "https://typed.example.com");
    });

    [Fact]
    public void Enter_shift_enter_and_tab_end_an_address_too() => Wpf.Run(() =>
    {
        using var editor = new OpenEditor(With(Text("start")));
        var box = editor.RichText();
        box.CaretPosition = box.Document.ContentEnd;

        editor.Type(box, " https://enter.example.com");
        editor.Execute(EditingCommands.EnterParagraphBreak, box);
        editor.Type(box, "https://shift.example.com");
        editor.Execute(EditingCommands.EnterLineBreak, box);
        editor.Type(box, "https://tab.example.com");
        editor.Execute(EditingCommands.TabForward, box);

        var texts = Hyperlinks(box.Document).Select(TextOf).ToList();
        Assert.Contains("https://enter.example.com", texts);
        Assert.Contains("https://shift.example.com", texts);
        Assert.Contains("https://tab.example.com", texts);
    });

    // Word's behaviour: the first Ctrl+Z takes the link back and keeps the text, and the
    // link is not re-created behind the user's back; the next one undoes the typing.
    [Fact]
    public void Undo_removes_the_automatic_link_first_and_it_stays_removed() => Wpf.Run(() =>
    {
        using var editor = new OpenEditor(With(Text("start")));
        var box = editor.RichText();
        box.CaretPosition = box.Document.ContentEnd;
        editor.Type(box, " https://typed.example.com ");

        box.Undo();
        Wpf.Pump();
        Wpf.Pump();

        Assert.DoesNotContain(Hyperlinks(box.Document), h => TextOf(h).Contains("typed"));
        Assert.Contains("https://typed.example.com ", AllText(box));

        box.Undo();
        Wpf.Pump();

        Assert.DoesNotContain("typed", AllText(box));
    });

    [Fact]
    public void An_address_typed_last_is_linked_when_the_block_is_stored() => Wpf.Run(() =>
    {
        using var editor = new OpenEditor(With(Text("start")));
        var box = editor.RichText();
        box.CaretPosition = box.Document.ContentEnd;
        editor.Type(box, " end https://last.example.com");

        SyncAll(editor.View);

        var block = editor.ViewModel.Blocks[0];
        Assert.Contains(Hyperlinks(Load(block.RichTextContent)), h => TextOf(h) == "https://last.example.com");
        Assert.Contains("https://last.example.com", block.PlainTextContent);
    });

    // The exporters see the same target a Ctrl+Click would open: the address for an
    // automatic link, the NavigateUri for a pasted one — never a file: target.
    [Fact]
    public void Exported_text_carries_each_links_target() => Wpf.Run(() =>
    {
        using var editor = new OpenEditor(With(Text("see https://auto.example.com here")));
        var box = editor.RichText();
        var paragraph = (Paragraph)box.Document.Blocks.FirstBlock;
        paragraph.Inlines.Add(new Hyperlink(new Run("pasted")) { NavigateUri = new Uri("https://pasted.example.com/") });
        paragraph.Inlines.Add(new Hyperlink(new Run("evil")) { NavigateUri = new Uri("file:///C:/Windows/notepad.exe") });
        SyncAll(editor.View);

        var exported = NoteApp.Services.Export.RichTextDocument.Extract(editor.ViewModel.Blocks[0].RichTextContent);

        var texts = Assert.IsType<NoteApp.Services.Export.DocParagraph>(Assert.Single(exported)).Inlines
            .OfType<NoteApp.Services.Export.DocText>().ToList();
        Assert.Equal("https://auto.example.com/", texts.Single(t => t.Text == "https://auto.example.com").Link);
        Assert.Equal("https://pasted.example.com/", texts.Single(t => t.Text == "pasted").Link);
        Assert.Null(texts.Single(t => t.Text == "evil").Link);
        Assert.Null(texts.Single(t => t.Text == "see ").Link);
    });

    [Fact]
    public void A_checklist_line_that_is_an_address_reads_as_a_link() => Wpf.Run(() =>
    {
        using var editor = new OpenEditor(With(Checklist("https://check.example.com", "read https://mid.example.com later", "buy milk")));
        var boxes = editor.Find<TextBox>().Where(b => b.Tag is ChecklistItemViewModel).ToList();

        Assert.Equal(Accent, Assert.IsType<SolidColorBrush>(boxes[0].Foreground).Color);
        Assert.NotEmpty(boxes[0].TextDecorations);
        Assert.True(boxes[1].TextDecorations is null || boxes[1].TextDecorations.Count == 0);

        var openButtons = editor.Find<Button>().Where(b => b.Tag is ChecklistItemViewModel).ToList();
        Assert.Equal([Visibility.Visible, Visibility.Visible, Visibility.Collapsed], openButtons.Select(b => b.Visibility));

        // Done wins over the link styling.
        ((ChecklistItemViewModel)boxes[0].Tag).IsDone = true;
        Wpf.Pump();
        Assert.Equal(TextDecorationLocation.Strikethrough, Assert.Single(boxes[0].TextDecorations).Location);
    });

    [Fact]
    public void A_click_in_a_checklist_item_resolves_to_the_address_under_it() => Wpf.Run(() =>
    {
        using var editor = new OpenEditor(With(Checklist("read https://mid.example.com later")));
        var box = editor.Find<TextBox>().Single(b => b.Tag is ChecklistItemViewModel);

        Assert.Equal("https://mid.example.com/", ResolveIn(box, box.Text.IndexOf("mid", StringComparison.Ordinal)));
        Assert.Equal("none", ResolveIn(box, 1));
    });

    [Fact]
    public void The_link_block_open_button_follows_the_validity_of_the_address() => Wpf.Run(() =>
    {
        using var editor = new OpenEditor(With(Link("https://link.example.com")));
        var button = editor.Find<Button>().Single(b => b.Tag is BlockViewModel { BlockType: BlockType.Link } && b.ToolTip is string t && t.StartsWith("Open"));

        Assert.True(button.IsEnabled);

        editor.ViewModel.Blocks[0].LinkUrlText = "not a url";
        Wpf.Pump();

        Assert.False(button.IsEnabled);
    });

    // What a Ctrl+Click would open shows as a hand, and only while Ctrl is down.
    [Fact]
    public void The_hand_shows_over_what_a_click_would_open() => Wpf.Run(() =>
    {
        using var editor = new OpenEditor(With(Text("ab https://old.example.com cd"), Checklist("read https://mid.example.com later")));
        var box = editor.RichText();
        var run = (Run)Assert.Single(Hyperlinks(box.Document)).Inlines.FirstInline;
        var onLink = CenterOf(run, 2);
        var before = CenterOf((Run)((Paragraph)box.Document.Blocks.FirstBlock).Inlines.FirstInline, 0);

        // A text block: a plain click opens, so no modifier needed; Alt edits instead.
        NoteEditorView.UpdateLinkCursor(box, onLink, System.Windows.Input.ModifierKeys.None);
        Assert.Equal(System.Windows.Input.Cursors.Hand, box.Cursor);
        Assert.True(box.ForceCursor);

        NoteEditorView.UpdateLinkCursor(box, onLink, System.Windows.Input.ModifierKeys.Alt);
        Assert.False(box.ForceCursor);
        Assert.NotEqual(System.Windows.Input.Cursors.Hand, box.Cursor);

        NoteEditorView.UpdateLinkCursor(box, onLink, System.Windows.Input.ModifierKeys.Control);
        Assert.True(box.ForceCursor);

        NoteEditorView.UpdateLinkCursor(box, before, System.Windows.Input.ModifierKeys.None);
        Assert.False(box.ForceCursor);

        // A checklist item: a click edits it, so the hand needs Ctrl.
        var item = editor.Find<TextBox>().Single(t => t.Tag is ChecklistItemViewModel);
        var rect = item.GetRectFromCharacterIndex(item.Text.IndexOf("mid", StringComparison.Ordinal));
        var onItemLink = new Point(rect.X + 2, rect.Y + rect.Height / 2);
        NoteEditorView.UpdateLinkCursor(item, onItemLink, System.Windows.Input.ModifierKeys.None);
        Assert.False(item.ForceCursor);
        NoteEditorView.UpdateLinkCursor(item, onItemLink, System.Windows.Input.ModifierKeys.Control);
        Assert.Equal(System.Windows.Input.Cursors.Hand, item.Cursor);
    });

    // A click in a text block opens on the release: not after a drag, not with a selection,
    // not with Alt (edit), not on the second click of a double-click.
    [Fact]
    public void A_plain_click_on_a_text_link_opens_it_unless_it_was_a_drag() => Wpf.Run(() =>
    {
        using var editor = new OpenEditor(With(Text("ab https://old.example.com cd")));
        var box = editor.RichText();
        var run = (Run)Assert.Single(Hyperlinks(box.Document)).Inlines.FirstInline;
        var at = CenterOf(run, 3);
        var url = RichTextLinks.LinkAt(box.GetPositionFromPoint(at, false)!).Match(u => u, () => throw new InvalidOperationException());

        Assert.True(NoteEditorView.OpensOnPlainClick(System.Windows.Input.ModifierKeys.None, clickCount: 1));
        Assert.False(NoteEditorView.OpensOnPlainClick(System.Windows.Input.ModifierKeys.Alt, clickCount: 1));
        Assert.False(NoteEditorView.OpensOnPlainClick(System.Windows.Input.ModifierKeys.Shift, clickCount: 1));
        Assert.False(NoteEditorView.OpensOnPlainClick(System.Windows.Input.ModifierKeys.None, clickCount: 2));

        Assert.True(NoteEditorView.ReleasedOnSameLink(box, url, at, at).IsSome);
        Assert.True(NoteEditorView.ReleasedOnSameLink(box, url, at, at + new Vector(1, 0)).IsSome);
        Assert.True(NoteEditorView.ReleasedOnSameLink(box, url, at, at + new Vector(40, 0)).IsNone);

        box.Selection.Select(run.ContentStart, run.ContentEnd);
        Assert.True(NoteEditorView.ReleasedOnSameLink(box, url, at, at).IsNone);
    });

    private static Point CenterOf(Run run, int charIndex)
    {
        var left = run.ContentStart.GetPositionAtOffset(charIndex)!.GetCharacterRect(LogicalDirection.Forward);
        var right = run.ContentStart.GetPositionAtOffset(charIndex + 1)!.GetCharacterRect(LogicalDirection.Backward);
        return new Point((left.X + right.X) / 2, left.Y + left.Height / 2);
    }

    private static string Resolve(RichTextBox box, Run run, int charIndex)
    {
        var left = run.ContentStart.GetPositionAtOffset(charIndex)!.GetCharacterRect(LogicalDirection.Forward);
        var right = run.ContentStart.GetPositionAtOffset(charIndex + 1)!.GetCharacterRect(LogicalDirection.Backward);
        var position = box.GetPositionFromPoint(new Point((left.X + right.X) / 2, left.Y + left.Height / 2), snapToText: false)!;
        return RichTextLinks.LinkAt(position).Match(u => u.Value.AbsoluteUri, () => "none");
    }

    private static string ResolveIn(TextBox box, int charIndex)
    {
        var rect = box.GetRectFromCharacterIndex(charIndex);
        var index = box.GetCharacterIndexFromPoint(new Point(rect.X + 2, rect.Y + rect.Height / 2), snapToText: false);
        return TextLinks.At(box.Text, index).Match(u => u.Value.AbsoluteUri, () => "none");
    }

    private static string AllText(RichTextBox box) => new TextRange(box.Document.ContentStart, box.Document.ContentEnd).Text;

    // What a save does first (the view model raises SyncAllBlocksRequested).
    internal static void SyncAll(NoteEditorView view) =>
        typeof(NoteEditorView).GetMethod("SyncAllRichTextBoxes", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(view, null);
}
