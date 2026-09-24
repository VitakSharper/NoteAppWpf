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

    internal static void Click(OpenEditor editor, string toolTip)
    {
        var button = editor.Find<Button>().First(b => Equals(b.ToolTip, toolTip));
        button.RaiseEvent(new System.Windows.RoutedEventArgs(ButtonBase.ClickEvent, button));
        Wpf.Pump();
    }
}
