using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using NoteApp.Data.Queries;
using NoteApp.Data.Repositories;
using NoteApp.Domain.Functional;
using NoteApp.Domain.Models;
using NoteApp.Domain.ValueObjects;
using NoteApp.Services;
using NoteApp.ViewModels;
using NoteApp.Views;

namespace NoteApp.UiTests;

// An open NoteEditorView on a note, and the ways a test drives it: typing goes through
// the real text-input pipeline (TextCompositionManager), keys through EditingCommands.
public sealed class OpenEditor : IDisposable
{
    public Window Window { get; }
    public NoteEditorView View { get; }
    public NoteEditorViewModel ViewModel { get; }

    public OpenEditor(Note? note, string? searchTerm = null)
    {
        ViewModel = new NoteEditorViewModel(new NoteService(new UnusedNoteRepository()), new UnusedTagRepository(), [], note)
        {
            SearchTerm = searchTerm
        };
        View = new NoteEditorView { DataContext = ViewModel };
        Window = Wpf.Show(View);
    }

    public RichTextBox RichText(int index = 0) => Wpf.Descendants<RichTextBox>(View).ElementAt(index);

    public IEnumerable<T> Find<T>() where T : DependencyObject => Wpf.Descendants<T>(View);

    public void Type(RichTextBox box, string text)
    {
        box.Focus();
        foreach (var ch in text)
        {
            TextCompositionManager.StartComposition(new TextComposition(InputManager.Current, box, ch.ToString()));
            Wpf.Pump();
        }
    }

    public void Execute(RoutedUICommand command, IInputElement target)
    {
        command.Execute(null, target);
        Wpf.Pump();
    }

    public void Dispose() => Window.Close();
}

public static class Notes
{
    public static Note With(params NoteBlock[] blocks)
    {
        for (var i = 0; i < blocks.Length; i++)
            blocks[i] = blocks[i] with { SortOrder = i };

        return Note.Create(NoteTitle.From("Probe").Match(t => t, e => throw new InvalidOperationException(e.Message)), blocks, [])
            .Match(n => n, e => throw new InvalidOperationException(e.Message));
    }

    // A text block the way the editor stores it: one paragraph per string, as a Base64 XamlPackage.
    public static NoteBlock.Text Text(params string[] paragraphs)
    {
        var document = new FlowDocument();
        foreach (var paragraph in paragraphs)
            document.Blocks.Add(new Paragraph(new Run(paragraph)));

        using var stream = new MemoryStream();
        new TextRange(document.ContentStart, document.ContentEnd).Save(stream, DataFormats.XamlPackage);
        return new NoteBlock.Text(Convert.ToBase64String(stream.ToArray()), string.Join(Environment.NewLine, paragraphs));
    }

    public static NoteBlock.Checklist Checklist(params string[] items) =>
        new(items.Select(i => new ChecklistItem(i, false)).ToList());

    public static NoteBlock.Link Link(string url) =>
        new(LinkUrl.From(url).Match(u => u, e => throw new InvalidOperationException(e.Message)), "");

    public static FlowDocument Load(string richTextPayload)
    {
        var document = new FlowDocument();
        using var stream = new MemoryStream(Convert.FromBase64String(richTextPayload));
        new TextRange(document.ContentStart, document.ContentEnd).Load(stream, DataFormats.XamlPackage);
        return document;
    }

    public static string TextOf(TextElement element) => new TextRange(element.ContentStart, element.ContentEnd).Text;

    public static List<Hyperlink> Hyperlinks(FlowDocument document) =>
        Inlines(document.Blocks).OfType<Hyperlink>().ToList();

    public static IEnumerable<Inline> Inlines(IEnumerable<Block> blocks) =>
        blocks.SelectMany(block => block switch
        {
            Paragraph p => Inlines(p.Inlines),
            List l => l.ListItems.SelectMany(i => Inlines(i.Blocks)),
            Section s => Inlines(s.Blocks),
            _ => []
        });

    private static IEnumerable<Inline> Inlines(InlineCollection inlines) =>
        inlines.SelectMany(i => i is Span span ? [i, .. Inlines(span.Inlines)] : new[] { i });
}

// Nothing a UI test does may reach storage: a call is a bug in the test.
internal sealed class UnusedNoteRepository : INoteRepository
{
    public Task<Result<Note, AppError>> GetByIdAsync(NoteId id) => throw new NotSupportedException();
    public Task<Result<Note, AppError>> CreateAsync(Note note, byte[]? encryptedContent = null) => throw new NotSupportedException();
    public Task<Result<Note, AppError>> UpdateAsync(Note note, byte[]? encryptedContent = null) => throw new NotSupportedException();
    public Task<Result<Unit, AppError>> DeleteAsync(NoteId id) => throw new NotSupportedException();
    public Task<Result<Unit, AppError>> RestoreAsync(NoteId id) => throw new NotSupportedException();
    public Task<Result<Unit, AppError>> PurgeAsync(NoteId id) => throw new NotSupportedException();
    public Task<Result<int, AppError>> PurgeAllDeletedAsync() => throw new NotSupportedException();
    public Task<Result<IReadOnlyList<NoteSummaryRow>, AppError>> SearchSummariesAsync(string? searchText, IReadOnlyList<Guid>? tagIds, BlockType? blockType, bool deletedOnly = false) => throw new NotSupportedException();
    public Task<Result<byte[]?, AppError>> GetEncryptedContentAsync(NoteId id) => throw new NotSupportedException();
}

internal sealed class UnusedTagRepository : ITagRepository
{
    public Task<Result<IReadOnlyList<Tag>, AppError>> GetAllAsync() => throw new NotSupportedException();
    public Task<Result<Tag, AppError>> CreateAsync(Tag tag) => throw new NotSupportedException();
    public Task<Result<Tag, AppError>> UpdateAsync(Tag tag) => throw new NotSupportedException();
    public Task<Result<Unit, AppError>> DeleteAsync(Guid id) => throw new NotSupportedException();
}
