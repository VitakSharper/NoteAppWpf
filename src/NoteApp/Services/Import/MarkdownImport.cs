using System.IO;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media.Imaging;
using Markdig;
using MdTable = Markdig.Extensions.Tables.Table;
using MdTableCell = Markdig.Extensions.Tables.TableCell;
using MdTableRow = Markdig.Extensions.Tables.TableRow;
using Markdig.Extensions.TaskLists;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using NoteApp.Domain.Models;
using WpfBlock = System.Windows.Documents.Block;
using WpfImage = System.Windows.Controls.Image;
using WpfInline = System.Windows.Documents.Inline;
using WpfList = System.Windows.Documents.List;

namespace NoteApp.Services.Import;

public sealed record ImportedNote(string Title, IReadOnlyList<NoteBlock> Blocks);

// A Markdown (or plain text) file as a note — the reverse of MarkdownExportService, so an
// exported note comes back much as it was: headings, emphasis, strikethrough, highlight
// (<mark>), inline code, lists and links go into text blocks; task lists become checklists,
// fenced code a code block, local images are embedded. A leading "# Title" is the title.
// WPF: builds FlowDocuments, so on the UI thread.
public static class MarkdownImport
{
    public static readonly IReadOnlyList<string> Extensions = [".md", ".markdown", ".txt"];

    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseTaskLists()
        .UseEmphasisExtras()
        .UsePipeTables()
        .Build();

    public static bool CanImport(string path) =>
        Extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    public static ImportedNote FromFile(string path)
    {
        var text = File.ReadAllText(path);
        var fallbackTitle = Path.GetFileNameWithoutExtension(path);
        return Path.GetExtension(path).Equals(".txt", StringComparison.OrdinalIgnoreCase)
            ? FromPlainText(text, fallbackTitle)
            : FromMarkdown(text, fallbackTitle, Path.GetDirectoryName(Path.GetFullPath(path)));
    }

    public static ImportedNote FromPlainText(string text, string title) =>
        new(title, [new NoteBlock.Text(RichTextPayload.FromPlainText(text.Trim('\r', '\n')), text.Trim())]);

    public static ImportedNote FromMarkdown(string markdown, string fallbackTitle, string? imageFolder = null)
    {
        var document = Markdig.Markdown.Parse(markdown, Pipeline);
        var builder = new Builder(imageFolder);

        var title = fallbackTitle;
        var first = true;
        foreach (var block in document)
        {
            // The export's "# Title" line is the title, not a heading of the note.
            if (first && block is HeadingBlock { Level: 1 } heading)
            {
                title = PlainText(heading.Inline).Trim() is { Length: > 0 } t ? t : fallbackTitle;
                first = false;
                continue;
            }

            first = false;
            builder.Add(block);
        }

        var blocks = builder.Finish();
        if (blocks.Count == 0)
            blocks = [new NoteBlock.Text(RichTextPayload.FromPlainText(""), "")];
        return new ImportedNote(title.Length > 500 ? title[..500] : title, blocks);
    }

    // Consecutive rich content shares one text block; a checklist or a code block ends it.
    private sealed class Builder(string? imageFolder)
    {
        private readonly List<NoteBlock> _blocks = [];
        private FlowDocument? _text;

        public void Add(Markdig.Syntax.Block block)
        {
            switch (block)
            {
                // Markdown joins "- a" and "- [ ] b" into one list when they share a marker (as the
                // export writes a bullet list followed by a checklist): each run of task items
                // becomes a checklist, the runs between them stay a list in the text.
                case ListBlock list when list.OfType<ListItemBlock>().Any(item => TaskOf(item) is not null):
                    foreach (var run in Runs(list.OfType<ListItemBlock>()))
                    {
                        if (TaskOf(run[0]) is not null)
                        {
                            FlushText();
                            _blocks.Add(new NoteBlock.Checklist(run.Select(ToItem).ToList()));
                        }
                        else
                        {
                            (_text ??= new FlowDocument()).Blocks.Add(ToList(list, run));
                        }
                    }
                    break;

                case CodeBlock code:
                    FlushText();
                    var content = code.Lines.ToString().TrimEnd('\r', '\n');
                    if (content.Length > 0)
                        _blocks.Add(new NoteBlock.Code(content));
                    break;

                default:
                    foreach (var wpf in ToWpf(block))
                        (_text ??= new FlowDocument()).Blocks.Add(wpf);
                    break;
            }
        }

        public IReadOnlyList<NoteBlock> Finish()
        {
            FlushText();
            return _blocks;
        }

        private void FlushText()
        {
            if (_text is null)
                return;

            var plain = new TextRange(_text.ContentStart, _text.ContentEnd).Text.Trim();
            _blocks.Add(new NoteBlock.Text(RichTextPayload.From(_text), plain));
            _text = null;
        }

        private static IEnumerable<List<ListItemBlock>> Runs(IEnumerable<ListItemBlock> items)
        {
            var run = new List<ListItemBlock>();
            foreach (var item in items)
            {
                if (run.Count > 0 && TaskOf(run[0]) is null != TaskOf(item) is null)
                {
                    yield return run;
                    run = [];
                }
                run.Add(item);
            }

            if (run.Count > 0)
                yield return run;
        }

        private static TaskList? TaskOf(ListItemBlock item) =>
            (item.FirstOrDefault() as ParagraphBlock)?.Inline?.FirstChild as TaskList;

        private static ChecklistItem ToItem(ListItemBlock item) =>
            new(string.Join(" ", item.OfType<LeafBlock>().Select(b => PlainText(b.Inline).Trim())).Trim(), TaskOf(item)?.Checked == true);

        private IEnumerable<WpfBlock> ToWpf(Markdig.Syntax.Block block)
        {
            switch (block)
            {
                // Under the title's "#": "##" is the note's first level, as the export writes it.
                case HeadingBlock heading:
                    var paragraph = Paragraph(heading.Inline);
                    RichTextFormat.SetHeading(paragraph, Math.Clamp(heading.Level - 1, 1, 3));
                    yield return paragraph;
                    break;

                case ParagraphBlock p:
                    yield return Paragraph(p.Inline);
                    break;

                case ListBlock list:
                    yield return ToList(list, list.OfType<ListItemBlock>());
                    break;

                case QuoteBlock quote:
                    foreach (var inner in quote.SelectMany(ToWpf))
                    {
                        inner.FontStyle = FontStyles.Italic;
                        inner.Margin = new Thickness(16, 0, 0, 0);
                        yield return inner;
                    }
                    break;

                case MdTable table:
                    foreach (var row in table.OfType<MdTableRow>())
                        yield return new Paragraph(new Run(string.Join("  |  ", row.OfType<MdTableCell>()
                            .Select(cell => string.Join(" ", cell.OfType<LeafBlock>().Select(b => PlainText(b.Inline).Trim()))))));
                    break;

                case ThematicBreakBlock:
                    yield return new Paragraph();
                    break;

                case HtmlBlock html:
                    yield return new Paragraph(new Run(html.Lines.ToString().Trim()));
                    break;
            }
        }

        private WpfList ToList(ListBlock list, IEnumerable<ListItemBlock> items)
        {
            var wpfList = new WpfList { MarkerStyle = list.IsOrdered ? TextMarkerStyle.Decimal : TextMarkerStyle.Disc };
            foreach (var item in items)
            {
                var listItem = new ListItem();
                foreach (var inner in item.SelectMany(ToWpf))
                    listItem.Blocks.Add(inner);
                if (listItem.Blocks.Count == 0)
                    listItem.Blocks.Add(new Paragraph());
                wpfList.ListItems.Add(listItem);
            }

            return wpfList;
        }

        private Paragraph Paragraph(ContainerInline? inlines)
        {
            var paragraph = new Paragraph();
            if (inlines is not null)
                AddInlines(paragraph.Inlines, inlines, new Style());
            return paragraph;
        }

        private sealed record Style(bool Bold = false, bool Italic = false, bool Strike = false, bool Highlight = false, bool Underline = false);

        private void AddInlines(InlineCollection target, ContainerInline container, Style style)
        {
            var htmlStyle = style;
            foreach (var inline in container)
            {
                switch (inline)
                {
                    case TaskList:
                        break;

                    case LiteralInline literal:
                        target.Add(Styled(new Run(literal.Content.ToString()), htmlStyle));
                        break;

                    case EmphasisInline emphasis:
                        var inner = emphasis.DelimiterChar switch
                        {
                            '~' => htmlStyle with { Strike = true },
                            '=' => htmlStyle with { Highlight = true },
                            _ when emphasis.DelimiterCount >= 2 => htmlStyle with { Bold = true },
                            _ => htmlStyle with { Italic = true }
                        };
                        AddInlines(target, emphasis, inner);
                        break;

                    case CodeInline code:
                        var run = Styled(new Run(code.Content), htmlStyle);
                        run.FontFamily = RichTextFormat.CodeFont;
                        target.Add(run);
                        break;

                    case LinkInline { IsImage: true } image:
                        target.Add(Image(image) ?? Styled(new Run(PlainText(image)), htmlStyle));
                        break;

                    case LinkInline link:
                        var hyperlink = new Hyperlink();
                        AddInlines(hyperlink.Inlines, link, htmlStyle);
                        if (Uri.TryCreate(link.Url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https")
                            hyperlink.NavigateUri = uri;
                        target.Add(hyperlink.Inlines.Count > 0 ? hyperlink : new Run(link.Url ?? ""));
                        break;

                    case AutolinkInline autolink:
                        target.Add(new Run(autolink.Url)); // linkified with the rest
                        break;

                    case LineBreakInline { IsHard: true }:
                        target.Add(new LineBreak());
                        break;

                    case LineBreakInline:
                        target.Add(new Run(" "));
                        break;

                    case HtmlEntityInline entity:
                        target.Add(Styled(new Run(entity.Transcoded.ToString()), htmlStyle));
                        break;

                    // The export's <u>…</u> and <mark>…</mark>; other tags are dropped.
                    case HtmlInline html:
                        htmlStyle = html.Tag.ToLowerInvariant() switch
                        {
                            "<u>" => htmlStyle with { Underline = true },
                            "</u>" => htmlStyle with { Underline = style.Underline },
                            "<mark>" => htmlStyle with { Highlight = true },
                            "</mark>" => htmlStyle with { Highlight = style.Highlight },
                            _ => htmlStyle
                        };
                        break;

                    case ContainerInline other:
                        AddInlines(target, other, htmlStyle);
                        break;
                }
            }
        }

        private static WpfInline Styled(Run run, Style style)
        {
            if (style.Bold) run.FontWeight = FontWeights.Bold;
            if (style.Italic) run.FontStyle = FontStyles.Italic;
            if (style.Highlight) run.Background = RichTextFormat.Highlighter;

            var decorations = new TextDecorationCollection();
            if (style.Underline) decorations.Add(TextDecorations.Underline[0]);
            if (style.Strike) decorations.Add(TextDecorations.Strikethrough[0]);
            if (decorations.Count > 0) run.TextDecorations = decorations;
            return run;
        }

        // A local image next to the file (the export's "<name>_files" folder); a web or missing
        // one stays as its alt text.
        private WpfInline? Image(LinkInline image)
        {
            if (imageFolder is null || string.IsNullOrWhiteSpace(image.Url))
                return null;

            try
            {
                var path = Path.GetFullPath(Path.Combine(imageFolder, Uri.UnescapeDataString(image.Url)));
                if (!File.Exists(path))
                    return null;

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(path);
                bitmap.EndInit();
                bitmap.Freeze();
                return new InlineUIContainer(new WpfImage { Source = bitmap, MaxWidth = 600, Stretch = System.Windows.Media.Stretch.Uniform });
            }
            catch (Exception ex) when (ex is IOException or NotSupportedException or UriFormatException or ArgumentException or InvalidOperationException)
            {
                return null;
            }
        }
    }

    private static string PlainText(ContainerInline? inlines) =>
        inlines is null ? string.Empty : string.Concat(inlines.Select(PlainText));

    private static string PlainText(Markdig.Syntax.Inlines.Inline inline) => inline switch
    {
        TaskList => string.Empty,
        LiteralInline literal => literal.Content.ToString(),
        CodeInline code => code.Content,
        AutolinkInline autolink => autolink.Url,
        LineBreakInline => " ",
        HtmlEntityInline entity => entity.Transcoded.ToString(),
        ContainerInline container => PlainText(container),
        _ => string.Empty
    };
}
