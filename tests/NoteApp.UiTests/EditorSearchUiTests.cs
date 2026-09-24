using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using NoteApp.ViewModels;
using static NoteApp.UiTests.Notes;

namespace NoteApp.UiTests;

// A note opened from a search shows where the term is: the find bar of the first text
// block containing it opens with the term, its matches highlighted.
public class EditorSearchUiTests
{
    [Fact]
    public void The_first_block_with_the_term_opens_its_find_bar_on_it() => Wpf.Run(() =>
    {
        using var editor = new OpenEditor(With(Text("nothing here"), Text("a Needle and a needle")), searchTerm: "needle");
        Wpf.Pump();

        var bars = SearchBars(editor);
        Assert.Equal([Visibility.Collapsed, Visibility.Visible], bars.Select(b => b.Visibility));

        var findBox = editor.Find<TextBox>().Single(t => t.Tag is BlockViewModel && t.IsVisible && Equals(t.Text, "needle"));
        Assert.False(findBox.IsKeyboardFocused);

        var status = editor.Find<TextBlock>().Single(t => t.Tag is BlockViewModel && t.IsVisible && t.Text.Length > 0);
        Assert.Equal("1 of 2", status.Text);

        var document = editor.RichText(1).Document;
        Assert.Contains(Brushes.Orange, Backgrounds(document));
        Assert.False(editor.ViewModel.IsDirty);
    });

    [Fact]
    public void Without_a_search_no_find_bar_opens() => Wpf.Run(() =>
    {
        using var editor = new OpenEditor(With(Text("a needle")));
        Wpf.Pump();

        Assert.All(SearchBars(editor), b => Assert.Equal(Visibility.Collapsed, b.Visibility));
    });

    [Fact]
    public void A_term_no_text_block_contains_opens_nothing() => Wpf.Run(() =>
    {
        using var editor = new OpenEditor(With(Text("a needle"), Checklist("haystack")), searchTerm: "haystack");
        Wpf.Pump();

        Assert.All(SearchBars(editor), b => Assert.Equal(Visibility.Collapsed, b.Visibility));
    });

    private static List<Border> SearchBars(OpenEditor editor) =>
        editor.Find<Border>().Where(b => b.Tag is BlockViewModel && b.Child is DockPanel).ToList();

    private static IEnumerable<Brush?> Backgrounds(FlowDocument document) =>
        Inlines(document.Blocks).OfType<Run>().Select(r => r.Background);
}
