using System.IO;
using System.Windows;
using System.Windows.Documents;
using NoteApp.Data.Queries;
using NoteApp.Domain.Functional;
using NoteApp.Domain.Models;
using NoteApp.Domain.ValueObjects;
using NoteApp.Services;
using NoteApp.Services.Export;
using NoteApp.ViewModels;
using static NoteApp.UiTests.Notes;

namespace NoteApp.UiTests;

// "[[" links a note to another one; the link opens that note, and the other note lists
// this one under "Linked from".
public class NoteLinkUiTests
{
    private static readonly NoteId OtherId = NoteId.New();

    [Fact]
    public void Typing_two_brackets_offers_the_other_notes_and_links_the_one_picked() => Wpf.Run(() =>
    {
        using var editor = new OpenEditor(With(Text("")), repository: new TwoNotes());
        var box = editor.RichText();

        editor.Type(box, "see [[");
        Wpf.Pump();

        Assert.True(editor.View.NotePicker.IsOpen);
        var offered = editor.View.NotePickerList.Items.Cast<NoteRef>().ToList();
        Assert.Equal(["Other note", "Plan"], offered.Select(n => n.Title.Value));

        editor.View.PickNote(offered[0]);
        editor.Type(box, "now");

        var paragraph = (Paragraph)box.Document.Blocks.FirstBlock;
        Assert.Equal("see Other note now", TextOf(paragraph));
        var link = Assert.Single(Hyperlinks(box.Document));
        Assert.Equal("Other note", TextOf(link));
        Assert.Equal(OtherId, Assert.IsType<Option<NoteId>.Some>(RichTextLinks.NoteAt(link.ContentStart.GetPositionAtOffset(1)!)).Value);

        EditorLinkUiTests.SyncAll(editor.View);
        Assert.Equal([OtherId], editor.ViewModel.Blocks[0].NoteLinks);
    });

    // A note link is not an address, whatever its title looks like: the linkify leaves it be,
    // and the exports write its text.
    [Fact]
    public void A_note_link_titled_like_an_address_stays_a_note_link() => Wpf.Run(() =>
    {
        using var editor = new OpenEditor(With(TextWithNoteLink("www.example.com notes")));
        var box = editor.RichText();

        var link = Assert.Single(Hyperlinks(box.Document));
        Assert.Equal(NoteLinks.UriFor(OtherId), link.NavigateUri);
        Assert.True(RichTextLinks.Target(link).IsNone);

        EditorLinkUiTests.SyncAll(editor.View);
        var text = Assert.IsType<DocText>(Assert.IsType<DocParagraph>(RichTextDocument.Extract(editor.ViewModel.Blocks[0].RichTextContent)[0]).Inlines
            .Single(i => i is DocText { Text: "www.example.com notes" }));
        Assert.Null(text.Link);
    });

    [Fact]
    public void Escape_in_the_picker_keeps_the_brackets_as_text() => Wpf.Run(() =>
    {
        using var editor = new OpenEditor(With(Text("")), repository: new TwoNotes());
        var box = editor.RichText();
        editor.Type(box, "[[");
        Wpf.Pump();

        editor.View.NotePicker.IsOpen = false;
        Wpf.Pump();

        Assert.Equal("[[", TextOf((Paragraph)box.Document.Blocks.FirstBlock));
        Assert.Empty(Hyperlinks(box.Document));
    });

    [Fact]
    public void The_shell_opens_a_linked_note_and_shows_who_links_to_it() => Wpf.Run(() =>
    {
        var shell = FocusModeUiTests.Shell(new TwoNotes());

        shell.OpenNoteByIdAsync(OtherId).GetAwaiter().GetResult();

        var editor = Assert.IsType<NoteEditorViewModel>(shell.CurrentEditor);
        Assert.Equal(OtherId, editor.EditedNoteId);
        Assert.True(editor.HasLinkedFrom);
        Assert.Equal(["Plan"], editor.LinkedFrom.Select(n => n.Title.Value));
    });

    [Fact]
    public void A_link_to_a_note_that_is_gone_opens_nothing() => Wpf.Run(() =>
    {
        var shell = FocusModeUiTests.Shell(new TwoNotes());

        shell.OpenNoteByIdAsync(NoteId.New()).GetAwaiter().GetResult();

        Assert.Null(shell.CurrentEditor);
    });

    private static NoteBlock.Text TextWithNoteLink(string title)
    {
        var paragraph = new Paragraph(new Run("see "));
        paragraph.Inlines.Add(new Hyperlink(new Run(title)) { NavigateUri = NoteLinks.UriFor(OtherId) });
        var document = new FlowDocument(paragraph);
        using var stream = new MemoryStream();
        new TextRange(document.ContentStart, document.ContentEnd).Save(stream, DataFormats.XamlPackage);
        return new NoteBlock.Text(Convert.ToBase64String(stream.ToArray()), "see " + title);
    }

    // "Plan" (which links to "Other note") and "Other note".
    private sealed class TwoNotes : UnusedNoteRepository
    {
        private static readonly Guid PlanId = Guid.NewGuid();

        public override Task<Result<IReadOnlyList<NoteSummaryRow>, AppError>> SearchSummariesAsync(
            string? searchText, IReadOnlyList<Guid>? tagIds, BlockType? blockType, bool deletedOnly = false) =>
            Task.FromResult(Result<IReadOnlyList<NoteSummaryRow>, AppError>.Ok(
            [
                new NoteSummaryRow { Id = PlanId, Title = "Plan", HasText = true },
                new NoteSummaryRow { Id = OtherId.Value, Title = "Other note", HasText = true }
            ]));

        public override Task<Result<Note, AppError>> GetByIdAsync(NoteId id) =>
            Task.FromResult(id == OtherId
                ? Result<Note, AppError>.Ok(With(Text("the other one")) with { Id = OtherId })
                : Result<Note, AppError>.Fail(AppError.NotFound("gone")));

        public override Task<Result<IReadOnlyList<NoteRow>, AppError>> LinkedFromAsync(NoteId id) =>
            Task.FromResult(Result<IReadOnlyList<NoteRow>, AppError>.Ok([new NoteRow { Id = PlanId, Title = "Plan" }]));
    }
}
