using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using NoteApp.Domain.Models;
using NoteApp.Services;
using NoteApp.Services.Export;
using NoteApp.ViewModels;

namespace NoteApp.Views;

public partial class NoteEditorView : UserControl
{
    private readonly Dictionary<Guid, RichTextBox> _richTextBoxes = [];
    private readonly Dictionary<Guid, Border> _searchBars = [];
    private readonly Dictionary<Guid, TextBox> _searchTextBoxes = [];
    private readonly Dictionary<Guid, TextBlock> _searchStatusBlocks = [];
    private readonly Dictionary<Guid, List<TextRange>> _searchMatches = [];
    private readonly Dictionary<Guid, int> _searchCurrentIndex = [];
    private DispatcherTimer? _searchDebounceTimer;
    private Guid _pendingSearchBlockId;
    private string _pendingSearchText = string.Empty;
    private bool _suppressTagToggle;
    private NoteEditorViewModel? _subscribedViewModel;

    public NoteEditorView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Unloaded += OnUnloaded;
    }

    // The editor pane reuses this view instance across notes, so the view model
    // subscriptions have to follow the DataContext rather than be wired once.
    private void OnLoaded(object sender, RoutedEventArgs e) =>
        Subscribe(DataContext as NoteEditorViewModel);

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        ResetBlockRegistrations();
        Subscribe(e.NewValue as NoteEditorViewModel);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) => Subscribe(null);

    private void Subscribe(NoteEditorViewModel? vm)
    {
        if (ReferenceEquals(_subscribedViewModel, vm))
            return;

        if (_subscribedViewModel is not null)
        {
            _subscribedViewModel.SyncAllBlocksRequested -= SyncAllRichTextBoxes;
            _subscribedViewModel.PasswordRequested -= OnPasswordRequested;
        }

        _subscribedViewModel = vm;

        if (vm is not null)
        {
            vm.SyncAllBlocksRequested += SyncAllRichTextBoxes;
            vm.PasswordRequested += OnPasswordRequested;
        }
    }

    // Element registrations are keyed by block id, and another note means
    // another set of blocks: stale entries must not survive the swap.
    private void ResetBlockRegistrations()
    {
        foreach (var rtb in _richTextBoxes.Values)
            CommandManager.RemovePreviewExecutedHandler(rtb, OnPreviewPasteExecuted);

        _richTextBoxes.Clear();
        _searchBars.Clear();
        _searchTextBoxes.Clear();
        _searchStatusBlocks.Clear();
        _searchMatches.Clear();
        _searchCurrentIndex.Clear();
        _searchDebounceTimer?.Stop();
    }

    private string? OnPasswordRequested()
    {
        var dialog = new PasswordDialog(isSetMode: true)
        {
            Owner = Window.GetWindow(this)
        };
        return dialog.ShowDialog() == true ? dialog.Password : null;
    }

    // --- Serialization: XamlPackage (supports images) with XAML fallback ---

    private static string SerializeDocument(FlowDocument doc)
    {
        var range = new TextRange(doc.ContentStart, doc.ContentEnd);
        using var ms = new MemoryStream();
        range.Save(ms, DataFormats.XamlPackage);
        return Convert.ToBase64String(ms.ToArray());
    }

    private static void DeserializeIntoRichTextBox(RichTextBox rtb, string content)
    {
        // Try XamlPackage (Base64) first — new format with image support
        try
        {
            var bytes = Convert.FromBase64String(content);
            using var ms = new MemoryStream(bytes);
            var range = new TextRange(rtb.Document.ContentStart, rtb.Document.ContentEnd);
            range.Load(ms, DataFormats.XamlPackage);
            ClearForeground(rtb.Document);
            return;
        }
        catch { /* Not Base64/XamlPackage — try legacy XAML */ }

        // Fall back to plain XAML (existing notes saved before image support)
        try
        {
            if (XamlReader.Parse(content) is FlowDocument doc)
            {
                ClearForeground(doc);
                rtb.Document = doc;
                return;
            }
        }
        catch { /* Not valid XAML either */ }

        // Last resort: treat as plain text
        rtb.Document.Blocks.Clear();
        rtb.Document.Blocks.Add(new Paragraph(new Run(content)));
    }

    // TextRange.Save bakes the inherited Foreground into the payload — the dark-theme
    // body colour for a note written in dark mode — so it came back white-on-white in
    // the light theme. The editor has no text-colour feature, so every stored Foreground
    // is theme leakage: drop them all and let the document inherit the current theme.
    private static void ClearForeground(FlowDocument doc)
    {
        doc.ClearValue(FlowDocument.ForegroundProperty);
        foreach (var block in doc.Blocks)
            ClearForeground(block);
    }

    private static void ClearForeground(TextElement element)
    {
        element.ClearValue(TextElement.ForegroundProperty);

        IEnumerable<TextElement> children = element switch
        {
            Paragraph paragraph => paragraph.Inlines,
            Span span => span.Inlines,
            Section section => section.Blocks,
            ListItem item => item.Blocks,
            List list => list.ListItems,
            _ => []
        };

        foreach (var child in children)
            ClearForeground(child);
    }

    private void SyncAllRichTextBoxes()
    {
        if (DataContext is not NoteEditorViewModel vm)
            return;

        foreach (var (blockId, rtb) in _richTextBoxes)
        {
            var block = vm.Blocks.FirstOrDefault(b => b.Id == blockId);
            if (block is not null)
                SyncBlock(block, rtb);
        }
    }

    // Pushes the document into the block: the rich payload for storage and the
    // plain text the list/search rely on. Search highlights are ordinary document
    // properties and would be saved with it, so they are stripped and restored.
    private void SyncBlock(BlockViewModel block, RichTextBox rtb) => WithoutDirtyTracking(() =>
    {
        var hadHighlights = ClearHighlights(block.Id);

        block.RichTextContent = SerializeDocument(rtb.Document);
        block.PlainTextContent = new TextRange(rtb.Document.ContentStart, rtb.Document.ContentEnd).Text;

        if (hadHighlights)
            ReapplyHighlights(block.Id);
    });

    // --- Modified state ---

    // Every write to the document raises TextChanged, the user's and ours alike:
    // loading a block, and the search highlights, which are plain document properties.
    // Bracketing our own writes keeps a note from looking edited when it is not.
    private int _suppressDirty;

    private void WithoutDirtyTracking(Action action)
    {
        _suppressDirty++;
        try { action(); }
        finally { _suppressDirty--; }
    }

    private void OnRichTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressDirty == 0 && DataContext is NoteEditorViewModel vm)
            vm.MarkDirty();
    }

    private void OnRichTextBoxLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is RichTextBox rtb && rtb.Tag is BlockViewModel block)
        {
            _richTextBoxes[block.Id] = rtb;
            // Remove first: Loaded can fire again for the same box on re-attach.
            CommandManager.RemovePreviewExecutedHandler(rtb, OnPreviewPasteExecuted);
            CommandManager.AddPreviewExecutedHandler(rtb, OnPreviewPasteExecuted);

            if (!string.IsNullOrEmpty(block.RichTextContent))
                WithoutDirtyTracking(() => DeserializeIntoRichTextBox(rtb, block.RichTextContent));
        }
    }

    private void OnRichTextBoxUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is not RichTextBox rtb || rtb.Tag is not BlockViewModel block)
            return;

        // Only drop the registration while it still points at this very box:
        // reordering blocks can raise Loaded for the new container first.
        if (!_richTextBoxes.TryGetValue(block.Id, out var registered) || !ReferenceEquals(registered, rtb))
            return;

        CommandManager.RemovePreviewExecutedHandler(rtb, OnPreviewPasteExecuted);
        _richTextBoxes.Remove(block.Id);
        _searchBars.Remove(block.Id);
        _searchTextBoxes.Remove(block.Id);
        _searchStatusBlocks.Remove(block.Id);
        _searchMatches.Remove(block.Id);
        _searchCurrentIndex.Remove(block.Id);
    }

    // Pixels scrolled per wheel notch — WPF's default of three 16px lines.
    private const double WheelPixelsPerNotch = 48;

    // The box no longer scrolls on its own, but its inner ScrollViewer would still
    // swallow the wheel: scroll the pane's ScrollViewer with the delta instead.
    private void OnRichTextBoxPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (e.Handled)
            return;

        e.Handled = true;
        var notches = e.Delta / (double)Mouse.MouseWheelDeltaForOneLine;
        BlocksScrollViewer.ScrollToVerticalOffset(BlocksScrollViewer.VerticalOffset - notches * WheelPixelsPerNotch);
    }

    private void OnRichTextLostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is RichTextBox rtb && rtb.Tag is BlockViewModel block)
            SyncBlock(block, rtb);
    }

    // --- Image paste & insert ---

    private void OnPreviewPasteExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.Command != ApplicationCommands.Paste || sender is not RichTextBox rtb)
            return;
        if (!Clipboard.ContainsImage())
            return;

        var bitmapSource = Clipboard.GetImage();
        if (bitmapSource is null) return;

        InsertImageAtCaret(rtb, bitmapSource);
        e.Handled = true;
    }

    private void OnInsertImage(object sender, RoutedEventArgs e)
    {
        var rtb = FindRichTextBox(sender);
        if (rtb is null) return;

        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Image files|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp|All files|*.*",
            Title = "Select an image"
        };

        if (dialog.ShowDialog() != true) return;

        var bitmapImage = new BitmapImage();
        bitmapImage.BeginInit();
        bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
        bitmapImage.UriSource = new Uri(dialog.FileName);
        bitmapImage.EndInit();
        bitmapImage.Freeze();

        InsertImageAtCaret(rtb, bitmapImage);
    }

    private static void InsertImageAtCaret(RichTextBox rtb, BitmapSource source)
    {
        var pngImage = EncodeToPngBitmapImage(source);
        var image = new Image
        {
            Source = pngImage,
            MaxWidth = 600,
            Stretch = Stretch.Uniform
        };

        var insertionPoint = rtb.CaretPosition.GetInsertionPosition(LogicalDirection.Forward);
        var container = new InlineUIContainer(image, insertionPoint);
        rtb.CaretPosition = container.ElementEnd;
    }

    private static BitmapImage EncodeToPngBitmapImage(BitmapSource source)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(source));
        using var ms = new MemoryStream();
        encoder.Save(ms);
        ms.Position = 0;

        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.CacheOption = BitmapCacheOption.OnLoad;
        bmp.StreamSource = ms;
        bmp.EndInit();
        bmp.Freeze();
        return bmp;
    }

    private RichTextBox? FindRichTextBox(object sender)
    {
        if (sender is Button btn && btn.Tag is BlockViewModel block && _richTextBoxes.TryGetValue(block.Id, out var rtb))
            return rtb;
        return null;
    }

    private void OnBold(object sender, RoutedEventArgs e)
    {
        var rtb = FindRichTextBox(sender);
        if (rtb is null) return;
        var selection = rtb.Selection;
        var current = selection.GetPropertyValue(TextElement.FontWeightProperty);
        selection.ApplyPropertyValue(TextElement.FontWeightProperty,
            current is FontWeight w && w == FontWeights.Bold ? FontWeights.Normal : FontWeights.Bold);
    }

    private void OnItalic(object sender, RoutedEventArgs e)
    {
        var rtb = FindRichTextBox(sender);
        if (rtb is null) return;
        var selection = rtb.Selection;
        var current = selection.GetPropertyValue(TextElement.FontStyleProperty);
        selection.ApplyPropertyValue(TextElement.FontStyleProperty,
            current is FontStyle s && s == FontStyles.Italic ? FontStyles.Normal : FontStyles.Italic);
    }

    private void OnUnderline(object sender, RoutedEventArgs e)
    {
        var rtb = FindRichTextBox(sender);
        if (rtb is null) return;
        var selection = rtb.Selection;
        var current = selection.GetPropertyValue(Inline.TextDecorationsProperty);
        selection.ApplyPropertyValue(Inline.TextDecorationsProperty,
            current == TextDecorations.Underline ? null : TextDecorations.Underline);
    }

    private void OnBulletList(object sender, RoutedEventArgs e)
    {
        var rtb = FindRichTextBox(sender);
        if (rtb is null) return;

        // Toggle bullets on the current selection: building a fresh List and
        // appending it moved the text to the bottom of the document instead.
        rtb.Focus();
        EditingCommands.ToggleBullets.Execute(null, rtb);
    }

    // --- Search bar element registration ---

    private void OnSearchBarLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is Border border && border.Tag is BlockViewModel block)
            _searchBars[block.Id] = border;
    }

    private void OnSearchTextBoxLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb && tb.Tag is BlockViewModel block)
            _searchTextBoxes[block.Id] = tb;
    }

    private void OnSearchStatusLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is TextBlock tb && tb.Tag is BlockViewModel block)
            _searchStatusBlocks[block.Id] = tb;
    }

    // --- Search functionality ---

    private void OnToggleSearch(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not BlockViewModel block) return;

        if (_searchBars.TryGetValue(block.Id, out var bar) && bar.Visibility == Visibility.Visible)
            CloseSearch(block.Id);
        else
            OpenSearch(block.Id);
    }

    // Also the Ctrl+F target when the focus sits in this block (MainWindow.OnFocusSearch).
    // An already-open bar just takes the focus back, the way a browser's find bar does.
    public void OpenSearch(Guid blockId)
    {
        if (!_searchBars.TryGetValue(blockId, out var bar))
            return;

        bar.Visibility = Visibility.Visible;
        if (_searchTextBoxes.TryGetValue(blockId, out var tb))
        {
            tb.Focus();
            tb.SelectAll();
        }
    }

    private void OnCloseSearch(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is BlockViewModel block)
            CloseSearch(block.Id);
    }

    private void CloseSearch(Guid blockId)
    {
        ClearHighlights(blockId);
        _searchMatches.Remove(blockId);
        _searchCurrentIndex.Remove(blockId);

        if (_searchBars.TryGetValue(blockId, out var bar))
            bar.Visibility = Visibility.Collapsed;

        if (_searchTextBoxes.TryGetValue(blockId, out var tb))
            tb.Text = string.Empty;

        if (_searchStatusBlocks.TryGetValue(blockId, out var status))
            status.Text = string.Empty;
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox tb || tb.Tag is not BlockViewModel block) return;

        _pendingSearchBlockId = block.Id;
        _pendingSearchText = tb.Text;

        if (_searchDebounceTimer == null)
        {
            _searchDebounceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(300)
            };
            _searchDebounceTimer.Tick += (_, _) =>
            {
                _searchDebounceTimer.Stop();
                PerformSearch(_pendingSearchBlockId, _pendingSearchText);
            };
        }

        _searchDebounceTimer.Stop();
        _searchDebounceTimer.Start();
    }

    private void OnSearchKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox tb || tb.Tag is not BlockViewModel block) return;

        if (e.Key == Key.Enter)
        {
            // Flush any pending debounced search immediately
            if (_searchDebounceTimer is { IsEnabled: true })
            {
                _searchDebounceTimer.Stop();
                PerformSearch(_pendingSearchBlockId, _pendingSearchText);
            }
            // If no search has run yet for current text, run it now
            else if (!_searchMatches.ContainsKey(block.Id) && !string.IsNullOrEmpty(tb.Text))
            {
                PerformSearch(block.Id, tb.Text);
            }

            if (Keyboard.Modifiers == ModifierKeys.Shift)
                NavigateMatch(block.Id, -1);
            else
                NavigateMatch(block.Id, 1);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            CloseSearch(block.Id);
            e.Handled = true;
        }
    }

    private void OnSearchNext(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is BlockViewModel block)
            NavigateMatch(block.Id, 1);
    }

    private void OnSearchPrevious(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is BlockViewModel block)
            NavigateMatch(block.Id, -1);
    }

    private void PerformSearch(Guid blockId, string searchText)
    {
        ClearHighlights(blockId);
        _searchMatches.Remove(blockId);
        _searchCurrentIndex.Remove(blockId);

        if (string.IsNullOrEmpty(searchText) || !_richTextBoxes.TryGetValue(blockId, out var rtb))
        {
            UpdateSearchStatus(blockId);
            return;
        }

        var matches = FindAllMatches(rtb.Document, searchText);
        _searchMatches[blockId] = matches;

        if (matches.Count > 0)
        {
            foreach (var match in matches)
                ApplyHighlight(match, Brushes.Yellow);

            _searchCurrentIndex[blockId] = 0;
            ApplyHighlight(matches[0], Brushes.Orange);
            BringIntoView(rtb, matches[0]);
        }

        UpdateSearchStatus(blockId);
    }

    private void NavigateMatch(Guid blockId, int direction)
    {
        if (!_searchMatches.TryGetValue(blockId, out var matches) || matches.Count == 0) return;
        if (!_searchCurrentIndex.TryGetValue(blockId, out var current)) return;

        // Reset current highlight to yellow
        ApplyHighlight(matches[current], Brushes.Yellow);

        // Move to next/previous
        var next = (current + direction + matches.Count) % matches.Count;
        _searchCurrentIndex[blockId] = next;

        // Highlight active match in orange
        ApplyHighlight(matches[next], Brushes.Orange);

        if (_richTextBoxes.TryGetValue(blockId, out var rtb))
            BringIntoView(rtb, matches[next]);

        UpdateSearchStatus(blockId);
    }

    private static List<TextRange> FindAllMatches(FlowDocument doc, string searchText)
    {
        // Build a flat list of TextPointers — one per text character — by walking
        // through each text run and advancing a pointer symbol by symbol.
        // This avoids O(n) GetPositionAtOffset calls per character.
        var charPointers = new List<TextPointer>();
        var textBuilder = new System.Text.StringBuilder();
        var position = doc.ContentStart;

        while (position != null && position.CompareTo(doc.ContentEnd) < 0)
        {
            if (position.GetPointerContext(LogicalDirection.Forward) == TextPointerContext.Text)
            {
                var text = position.GetTextInRun(LogicalDirection.Forward);
                var pointer = position;
                for (var i = 0; i < text.Length; i++)
                {
                    charPointers.Add(pointer);
                    textBuilder.Append(text[i]);
                    pointer = pointer.GetNextInsertionPosition(LogicalDirection.Forward) ?? pointer;
                }
            }

            position = position.GetNextContextPosition(LogicalDirection.Forward);
        }

        var fullText = textBuilder.ToString();
        var matches = new List<TextRange>();
        if (fullText.Length < searchText.Length)
            return matches;

        var searchIndex = 0;
        while (searchIndex <= fullText.Length - searchText.Length)
        {
            var found = fullText.IndexOf(searchText, searchIndex, StringComparison.OrdinalIgnoreCase);
            if (found < 0) break;

            var start = charPointers[found];
            var endCharIndex = found + searchText.Length;
            var end = endCharIndex < charPointers.Count
                ? charPointers[endCharIndex]
                : doc.ContentEnd;

            matches.Add(new TextRange(start, end));
            searchIndex = found + searchText.Length;
        }

        return matches;
    }

    private void ApplyHighlight(TextRange range, Brush background) => WithoutDirtyTracking(() =>
        range.ApplyPropertyValue(TextElement.BackgroundProperty, background));

    private bool ClearHighlights(Guid blockId)
    {
        if (!_searchMatches.TryGetValue(blockId, out var matches) || matches.Count == 0) return false;
        if (!_richTextBoxes.ContainsKey(blockId)) return false;

        // null rather than Transparent: Transparent is still a value, and it
        // would be written into the note along with the rest of the document.
        WithoutDirtyTracking(() =>
        {
            foreach (var match in matches)
                match.ApplyPropertyValue(TextElement.BackgroundProperty, null);
        });

        return true;
    }

    private void ReapplyHighlights(Guid blockId)
    {
        if (!_searchMatches.TryGetValue(blockId, out var matches) || matches.Count == 0) return;

        var current = _searchCurrentIndex.GetValueOrDefault(blockId, 0);
        for (var i = 0; i < matches.Count; i++)
            ApplyHighlight(matches[i], i == current ? Brushes.Orange : Brushes.Yellow);
    }

    private void UpdateSearchStatus(Guid blockId)
    {
        if (!_searchStatusBlocks.TryGetValue(blockId, out var status)) return;

        if (!_searchMatches.TryGetValue(blockId, out var matches) || matches.Count == 0)
        {
            var hasSearchText = _searchTextBoxes.TryGetValue(blockId, out var tb)
                                && !string.IsNullOrEmpty(tb.Text);
            status.Text = hasSearchText ? "No matches" : string.Empty;
            return;
        }

        var current = _searchCurrentIndex.GetValueOrDefault(blockId, 0);
        status.Text = $"{current + 1} of {matches.Count}";
    }

    private static void BringIntoView(RichTextBox rtb, TextRange range)
    {
        rtb.Selection.Select(range.Start, range.End);

        var frameworkElement = range.Start.GetAdjacentElement(LogicalDirection.Forward) as FrameworkContentElement
                              ?? range.Start.Paragraph;
        frameworkElement?.BringIntoView();

        // Also scroll the outer ScrollViewer so the RichTextBox itself is visible
        for (DependencyObject? parent = VisualTreeHelper.GetParent(rtb); parent != null; parent = VisualTreeHelper.GetParent(parent))
        {
            if (parent is ScrollViewer sv)
            {
                var transform = rtb.TransformToAncestor(sv);
                var rect = range.Start.GetCharacterRect(LogicalDirection.Forward);
                if (rect != Rect.Empty)
                {
                    var pointInScrollViewer = transform.Transform(new Point(0, rect.Top));
                    var targetOffset = sv.VerticalOffset + pointInScrollViewer.Y - sv.ViewportHeight / 2;
                    sv.ScrollToVerticalOffset(Math.Max(0, targetOffset));
                }
                break;
            }
        }
    }

    private void OnTagToggleLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Primitives.ToggleButton toggle
            && toggle.Tag is Domain.Models.Tag tag
            && DataContext is NoteEditorViewModel vm)
        {
            _suppressTagToggle = true;
            toggle.IsChecked = vm.IsTagSelected(tag);
            _suppressTagToggle = false;
        }
    }

    private void OnTagChecked(object sender, RoutedEventArgs e)
    {
        if (_suppressTagToggle) return;
        if (sender is System.Windows.Controls.Primitives.ToggleButton toggle
            && toggle.Tag is Domain.Models.Tag tag
            && DataContext is NoteEditorViewModel vm)
        {
            vm.ToggleTagCommand.Execute(tag);
        }
    }

    private void OnTagUnchecked(object sender, RoutedEventArgs e)
    {
        if (_suppressTagToggle) return;
        if (sender is System.Windows.Controls.Primitives.ToggleButton toggle
            && toggle.Tag is Domain.Models.Tag tag
            && DataContext is NoteEditorViewModel vm)
        {
            vm.ToggleTagCommand.Execute(tag);
        }
    }

    private void OnExportToPdf(object sender, RoutedEventArgs e)
    {
        if (DataContext is not NoteEditorViewModel vm) return;

        SyncAllRichTextBoxes();

        var textContents = vm.Blocks
            .Where(b => b.BlockType == BlockType.Text && !string.IsNullOrEmpty(b.RichTextContent))
            .Select(b => b.RichTextContent)
            .ToList();

        if (textContents.Count == 0)
        {
            MessageBox.Show("No text blocks to export.", "Export to PDF",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "PDF Files (*.pdf)|*.pdf",
            FileName = ExportFileName(vm.Title, "pdf"),
            Title = "Export to PDF"
        };

        if (dialog.ShowDialog() != true) return;

        try
        {
            PdfExportService.Export(ExportTitle(vm.Title), textContents, dialog.FileName);

            MessageBox.Show("PDF exported successfully!", "Export to PDF",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to export PDF: {ex.Message}", "Export to PDF",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // Unlike the PDF, Word gets every block in note order: links become hyperlinks
    // and file blocks are named (their bytes stay in the database).
    private void OnExportToWord(object sender, RoutedEventArgs e)
    {
        if (DataContext is not NoteEditorViewModel vm) return;

        SyncAllRichTextBoxes();

        var blocks = vm.Blocks.Select<BlockViewModel, ExportBlock>(b => b.BlockType switch
        {
            BlockType.Text => new TextExportBlock(b.RichTextContent),
            BlockType.Link => new LinkExportBlock(b.LinkUrlText, b.LinkDescription),
            _ => new FileExportBlock(b.FileName, b.FileSize)
        }).ToList();

        if (blocks.Count == 0)
        {
            MessageBox.Show("Nothing to export.", "Export to Word",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "Word documents (*.docx)|*.docx",
            FileName = ExportFileName(vm.Title, "docx"),
            Title = "Export to Word"
        };

        if (dialog.ShowDialog() != true) return;

        try
        {
            WordExportService.Export(ExportTitle(vm.Title), blocks, dialog.FileName);

            MessageBox.Show("Word document exported successfully!", "Export to Word",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to export Word document: {ex.Message}", "Export to Word",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static string ExportTitle(string title) =>
        string.IsNullOrWhiteSpace(title) ? "Untitled Note" : title;

    private static string ExportFileName(string title, string extension)
    {
        var safe = string.Join("_", title.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
        return string.IsNullOrWhiteSpace(safe) ? $"note.{extension}" : $"{safe}.{extension}";
    }
}
