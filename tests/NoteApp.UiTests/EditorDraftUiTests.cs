using static NoteApp.UiTests.Notes;

namespace NoteApp.UiTests;

public class EditorDraftUiTests
{
    // A draft is taken on a timer, possibly in the middle of an address: it reads the text
    // as typed but must not wrap the half address into a link.
    [Fact]
    public void A_draft_holds_what_was_typed_without_linking_it() => Wpf.Run(() =>
    {
        using var editor = new OpenEditor(With(Text("start")));
        var box = editor.RichText();
        box.CaretPosition = box.Document.ContentEnd;
        editor.Type(box, " https://half");

        var draft = editor.ViewModel.CaptureDraft();

        Assert.Contains("https://half", draft.Blocks[0].PlainText);
        Assert.Empty(Hyperlinks(box.Document));
        Assert.Empty(Hyperlinks(Load(draft.Blocks[0].RichText!)));

        editor.Type(box, ".example.com ");
        Assert.Contains(Hyperlinks(box.Document), h => TextOf(h) == "https://half.example.com");
    });
}
