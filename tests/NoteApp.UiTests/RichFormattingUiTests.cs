using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using NoteApp.Domain.Models;
using NoteApp.Services;
using NoteApp.Services.Export;
using NoteApp.ViewModels;
using NoteApp.Views;
using static NoteApp.UiTests.Notes;

namespace NoteApp.UiTests;

// Headings, strikethrough, the highlighter and inline code: set in the editor, kept by the
// stored payload, and seen by the exports.
public class RichFormattingUiTests
{
    [Fact]
    public void A_heading_survives_the_save_and_reaches_the_export() => Wpf.Run(() =>
    {
        using var editor = new OpenEditor(With(Text("Title", "body")));
        var box = editor.RichText();
        box.CaretPosition = box.Document.Blocks.FirstBlock.ContentStart;

        NoteEditorView.ApplyHeading(box, 2);

        var elements = Exported(editor);
        Assert.Equal([2, 0], elements.OfType<DocParagraph>().Select(p => p.HeadingLevel));
        Assert.False(Assert.IsType<DocText>(Assert.IsType<DocParagraph>(elements[0]).Inlines[0]).IsBold);
    });

    [Fact]
    public void Strikethrough_highlight_and_code_toggle_on_and_off() => Wpf.Run(() =>
    {
        using var editor = new OpenEditor(With(Text("one two three")));
        var box = editor.RichText();
        var paragraph = (Paragraph)box.Document.Blocks.FirstBlock;
        box.Selection.Select(paragraph.ContentStart.GetPositionAtOffset(5), paragraph.ContentStart.GetPositionAtOffset(8));

        NoteEditorView.ToggleStrikethrough(box);
        NoteEditorView.ToggleHighlight(box);
        NoteEditorView.ToggleInlineCode(box);

        var two = Assert.IsType<DocText>(Assert.IsType<DocParagraph>(Exported(editor)[0]).Inlines.Single(i => i is DocText { Text: "two" }));
        Assert.True(two.IsStrike);
        Assert.True(two.IsHighlight);
        Assert.True(two.IsCode);

        NoteEditorView.ToggleStrikethrough(box);
        NoteEditorView.ToggleHighlight(box);
        NoteEditorView.ToggleInlineCode(box);

        var texts = Assert.IsType<DocParagraph>(Exported(editor)[0]).Inlines.OfType<DocText>().ToList();
        Assert.All(texts, t => Assert.False(t.IsStrike || t.IsHighlight || t.IsCode));
        Assert.Equal("one two three", string.Concat(texts.Select(t => t.Text)));
    });

    // The find bar paints with the same property: closing it gives the marks back.
    [Fact]
    public void A_search_leaves_highlighter_marks_where_they_were() => Wpf.Run(() =>
    {
        using var editor = new OpenEditor(With(Text("one marked word")), searchTerm: "ark");
        var box = editor.RichText();
        Wpf.Pump();

        var marked = Find(box.Document, "marked");
        box.Selection.Select(marked.Start, marked.End);
        EditorFormattingUiTests.Click(editor, "Highlight");
        EditorFormattingUiTests.Click(editor, "Close Search");

        Assert.Equal(["marked"], Highlighted(Exported(editor)));
    });

    [Theory]
    [InlineData("# ", 1)]
    [InlineData("## ", 2)]
    [InlineData("### ", 3)]
    public void Hashes_and_a_space_at_the_start_of_a_line_make_a_heading(string typed, int level) => Wpf.Run(() =>
    {
        using var editor = new OpenEditor(With(Text("")));
        var box = editor.RichText();

        editor.Type(box, typed + "Plan");

        var paragraph = Assert.IsType<Paragraph>(Assert.Single(box.Document.Blocks));
        Assert.Equal(level, RichTextFormat.HeadingLevel(paragraph));
        Assert.Equal("Plan", TextOf(paragraph));
    });

    [Fact]
    public void A_dash_and_a_space_start_a_bullet_list_and_one_dot_a_numbered_one() => Wpf.Run(() =>
    {
        using var editor = new OpenEditor(With(Text(""), Text("")));

        editor.Type(editor.RichText(0), "- milk");
        editor.Type(editor.RichText(1), "1. first");

        var bullets = Assert.IsType<List>(Assert.Single(editor.RichText(0).Document.Blocks));
        Assert.Equal(TextMarkerStyle.Disc, bullets.MarkerStyle);
        Assert.Equal("milk", string.Concat(Inlines([bullets]).OfType<Run>().Select(r => r.Text)));
        Assert.Equal(TextMarkerStyle.Decimal, Assert.IsType<List>(Assert.Single(editor.RichText(1).Document.Blocks)).MarkerStyle);
    });

    // Only the very start of a line: the same characters later on stay text.
    [Fact]
    public void A_starter_after_other_text_stays_text() => Wpf.Run(() =>
    {
        using var editor = new OpenEditor(With(Text("")));
        var box = editor.RichText();

        editor.Type(box, "a # b");

        var paragraph = Assert.IsType<Paragraph>(Assert.Single(box.Document.Blocks));
        Assert.Equal(0, RichTextFormat.HeadingLevel(paragraph));
        Assert.Equal("a # b", TextOf(paragraph));
    });

    [Fact]
    public void Brackets_and_a_space_turn_the_line_into_a_checklist_between_the_text_around_it() => Wpf.Run(() =>
    {
        using var editor = new OpenEditor(With(Text("before", "", "after")));
        var box = editor.RichText();
        box.CaretPosition = box.Document.Blocks.ElementAt(1).ContentStart;

        editor.Type(box, "[] ");
        Wpf.Pump();

        var blocks = editor.ViewModel.Blocks;
        Assert.Equal([BlockType.Text, BlockType.Checklist, BlockType.Text], blocks.Select(b => b.BlockType));
        Assert.Equal([""], blocks[1].ChecklistItems.Select(i => i.Text));
        Assert.Equal("before", TextOf(Assert.Single(editor.RichText(0).Document.Blocks)));
        Assert.Equal("after", Load(blocks[2].RichTextContent).Blocks.Select(TextOf).Single(t => t.Length > 0));
        Assert.True(editor.ViewModel.IsDirty);
    });

    [Fact]
    public void A_text_block_that_was_only_the_starter_gives_way_to_the_checklist() => Wpf.Run(() =>
    {
        using var editor = new OpenEditor(With(Text("")));

        editor.Type(editor.RichText(), "[] ");
        Wpf.Pump();

        var block = Assert.Single(editor.ViewModel.Blocks);
        Assert.Equal(BlockType.Checklist, block.BlockType);
    });

    // Enter at the end of a heading starts body text, not a second heading.
    [Fact]
    public void The_line_after_a_heading_is_body_text() => Wpf.Run(() =>
    {
        using var editor = new OpenEditor(With(Text("")));
        var box = editor.RichText();
        editor.Type(box, "# Plan");

        var source = PresentationSource.FromVisual(box)!;
        box.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, Key.Enter) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
        editor.Execute(EditingCommands.EnterParagraphBreak, box);

        var paragraphs = box.Document.Blocks.OfType<Paragraph>().ToList();
        Assert.Equal([1, 0], paragraphs.Select(RichTextFormat.HeadingLevel));
    });

    private static IReadOnlyList<DocElement> Exported(OpenEditor editor)
    {
        EditorLinkUiTests.SyncAll(editor.View);
        return RichTextDocument.Extract(editor.ViewModel.Blocks[0].RichTextContent);
    }

    private static List<string> Highlighted(IReadOnlyList<DocElement> elements) =>
        elements.OfType<DocParagraph>().SelectMany(p => p.Inlines).OfType<DocText>().Where(t => t.IsHighlight).Select(t => t.Text).ToList();

    // By characters, not symbols: the find bar has already split the text into several runs.
    private static TextRange Find(FlowDocument document, string word)
    {
        var paragraph = (Paragraph)document.Blocks.FirstBlock;
        var index = TextOf(paragraph).IndexOf(word, StringComparison.Ordinal);
        return new TextRange(CharAt(paragraph, index), CharAt(paragraph, index + word.Length));
    }

    private static TextPointer CharAt(Paragraph paragraph, int index)
    {
        var position = paragraph.ContentStart.GetInsertionPosition(LogicalDirection.Forward);
        for (var i = 0; i < index; i++)
            position = position.GetNextInsertionPosition(LogicalDirection.Forward)!;
        return position;
    }
}
