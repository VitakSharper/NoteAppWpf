using System.IO;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using NoteApp.Data.Queries;
using NoteApp.Domain.Functional;
using NoteApp.Domain.Models;
using NoteApp.Services.Export;
using NoteApp.Services.Import;
using static NoteApp.UiTests.Notes;

namespace NoteApp.UiTests;

// Import builds FlowDocuments, hence the WPF thread; what comes out is read back through the
// export's extraction, so both directions agree on what formatting is.
public class MarkdownImportUiTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "NoteApp.UiTests", $"import-{Guid.NewGuid()}");

    [Fact]
    public void An_exported_note_comes_back_block_by_block() => Wpf.Run(() =>
    {
        var note = MarkdownImport.FromMarkdown("""
            # Weekly review

            ## Wins

            Shipped **the importer**, *on time*, ~~late~~ and <mark>noted</mark>, with `dotnet test` — see [the docs](https://docs.example.com/).

            - one
            - two

            - [x] tests
            - [ ] release

            ```
            SELECT *
            	FROM Notes
            ```

            after the code
            """, "fallback");

        Assert.Equal("Weekly review", note.Title);
        Assert.Equal([BlockType.Text, BlockType.Checklist, BlockType.Code, BlockType.Text], note.Blocks.Select(b => b.Type));

        var first = RichTextDocument.Extract(((NoteBlock.Text)note.Blocks[0]).RichText);
        Assert.Equal(1, Assert.IsType<DocParagraph>(first[0]).HeadingLevel);
        var texts = Assert.IsType<DocParagraph>(first[1]).Inlines.OfType<DocText>().ToList();
        Assert.True(texts.Single(t => t.Text == "the importer").IsBold);
        Assert.True(texts.Single(t => t.Text == "on time").IsItalic);
        Assert.True(texts.Single(t => t.Text == "late").IsStrike);
        Assert.True(texts.Single(t => t.Text == "noted").IsHighlight);
        Assert.True(texts.Single(t => t.Text == "dotnet test").IsCode);
        Assert.Equal("https://docs.example.com/", texts.Single(t => t.Text == "the docs").Link);
        Assert.Equal(DocListMarker.Bullet, Assert.IsType<DocList>(first[2]).Marker);

        var checklist = (NoteBlock.Checklist)note.Blocks[1];
        Assert.Equal([("tests", true), ("release", false)], checklist.Items.Select(i => (i.Text, i.IsDone)));
        Assert.Equal("SELECT *\n\tFROM Notes", ((NoteBlock.Code)note.Blocks[2]).Content.ReplaceLineEndings("\n"));
        Assert.Equal("after the code", ((NoteBlock.Text)note.Blocks[3]).PlainText);
    });

    [Fact]
    public void A_file_without_a_title_line_is_named_after_the_file_and_its_images_come_along() => Wpf.Run(() =>
    {
        Directory.CreateDirectory(Path.Combine(_folder, "Trip_files"));
        var pixel = BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgra32, null, new byte[4], 4);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(pixel));
        using (var stream = File.Create(Path.Combine(_folder, "Trip_files", "image1.png")))
            encoder.Save(stream);
        var path = Path.Combine(_folder, "Trip.md");
        File.WriteAllText(path, "Day one ![](Trip_files/image1.png) and ![gone](missing.png)\n");

        var note = MarkdownImport.FromFile(path);

        Assert.Equal("Trip", note.Title);
        var inlines = Assert.IsType<DocParagraph>(RichTextDocument.Extract(((NoteBlock.Text)Assert.Single(note.Blocks)).RichText)[0]).Inlines;
        Assert.Single(inlines.OfType<DocImage>());
        Assert.Contains(inlines.OfType<DocText>(), t => t.Text.Contains("gone"));
    });

    [Fact]
    public void A_text_file_is_one_text_block_with_its_addresses_linked() => Wpf.Run(() =>
    {
        Directory.CreateDirectory(_folder);
        var path = Path.Combine(_folder, "todo.txt");
        File.WriteAllText(path, "call https://bank.example.com\nthen lunch\n");

        var note = MarkdownImport.FromFile(path);

        Assert.Equal("todo", note.Title);
        var text = (NoteBlock.Text)Assert.Single(note.Blocks);
        Assert.Equal("call https://bank.example.com\nthen lunch", text.PlainText.ReplaceLineEndings("\n"));
        Assert.Single(Hyperlinks(Load(text.RichText)));
    });

    [Fact]
    public void Dropped_files_become_notes_and_the_rest_is_ignored() => Wpf.Run(() =>
    {
        Directory.CreateDirectory(_folder);
        var md = Path.Combine(_folder, "a.md");
        var txt = Path.Combine(_folder, "b.txt");
        var png = Path.Combine(_folder, "c.png");
        File.WriteAllText(md, "# From markdown\n\nbody");
        File.WriteAllText(txt, "plain");
        File.WriteAllBytes(png, [1, 2, 3]);
        var repo = new Capturing();
        var shell = FocusModeUiTests.Shell(repo);

        var count = shell.ImportFilesAsync([md, txt, png]).GetAwaiter().GetResult();

        Assert.Equal(2, count);
        Assert.Equal(["From markdown", "b"], repo.Created.Select(n => n.Title.Value));
    });

    public void Dispose()
    {
        if (Directory.Exists(_folder))
            Directory.Delete(_folder, recursive: true);
    }

    private sealed class Capturing : UnusedNoteRepository
    {
        public List<Note> Created { get; } = [];

        public override Task<Result<Note, AppError>> CreateAsync(Note note, byte[]? encryptedContent = null)
        {
            Created.Add(note);
            return Task.FromResult(Result<Note, AppError>.Ok(note));
        }

        public override Task<Result<IReadOnlyList<NoteSummaryRow>, AppError>> SearchSummariesAsync(NoteQuery query) =>
            Task.FromResult(Result<IReadOnlyList<NoteSummaryRow>, AppError>.Ok([]));
    }
}
