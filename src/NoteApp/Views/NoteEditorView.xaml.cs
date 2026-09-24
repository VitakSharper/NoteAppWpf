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
using NoteApp.Domain.Functional;
using NoteApp.Domain.Models;
using NoteApp.Domain.ValueObjects;
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
        DataObject.AddCopyingHandler(this, OnCopying);
    }

    // Ctrl+C / Ctrl+X anywhere in an encrypted note, or in a field of a secret block: the
    // copy is kept out of the clipboard history and wiped after a while (SensitiveClipboard),
    // the way the copy buttons of a secret do it. After the copy has landed, hence deferred.
    private void OnCopying(object sender, DataObjectCopyingEventArgs e)
    {
        var fromSecret = e.OriginalSource is FrameworkElement { Tag: BlockViewModel { BlockType: BlockType.Secret } };
        if (e.IsDragDrop || DataContext is not NoteEditorViewModel vm || !(vm.IsEncrypted || fromSecret))
            return;

        SensitiveClipboard.MarkPrivate(e.DataObject);
        _ = Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(SensitiveClipboard.ClearLater));
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
            _subscribedViewModel.SnapshotRequested -= SnapshotAllRichTextBoxes;
            _subscribedViewModel.PasswordRequested -= OnPasswordRequested;
        }

        _subscribedViewModel = vm;

        if (vm is not null)
        {
            vm.SyncAllBlocksRequested += SyncAllRichTextBoxes;
            vm.SnapshotRequested += SnapshotAllRichTextBoxes;
            vm.PasswordRequested += OnPasswordRequested;
        }
    }

    // Element registrations are keyed by block id, and another note means
    // another set of blocks: stale entries must not survive the swap.
    private void ResetBlockRegistrations()
    {
        _searchTermShown = false;

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

    private void SyncAllRichTextBoxes() => SyncAll(linkify: true);

    // For drafts: taken on a timer, possibly mid-word, so no address is linked by it.
    private void SnapshotAllRichTextBoxes() => SyncAll(linkify: false);

    private void SyncAll(bool linkify)
    {
        if (DataContext is not NoteEditorViewModel vm)
            return;

        foreach (var (blockId, rtb) in _richTextBoxes)
        {
            var block = vm.Blocks.FirstOrDefault(b => b.Id == blockId);
            if (block is not null)
                SyncBlock(block, rtb, linkify);
        }
    }

    // Pushes the document into the block: the rich payload for storage and the
    // plain text the list/search rely on. Search highlights are ordinary document
    // properties and would be saved with it, so they are stripped and restored.
    private void SyncBlock(BlockViewModel block, RichTextBox rtb, bool linkify = true) => WithoutDirtyTracking(() =>
    {
        var hadHighlights = ClearHighlights(block.Id);

        // An address typed last, with no space after it yet, is linked before it is stored.
        if (linkify)
            RichTextLinks.Linkify(rtb.Document, rtb.Selection);

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
        if (_suppressDirty != 0 || DataContext is not NoteEditorViewModel vm)
            return;

        vm.MarkDirty();

        // Not on undo or redo: undoing an automatic link must not bring it straight back.
        if (sender is RichTextBox rtb && e.UndoAction is not (UndoAction.Undo or UndoAction.Redo) && EndsAWord(rtb, e.Changes))
            ScheduleLinkify(rtb);
    }

    private void OnRichTextBoxLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is RichTextBox rtb && rtb.Tag is BlockViewModel block)
        {
            _richTextBoxes[block.Id] = rtb;
            // Remove first: Loaded can fire again for the same box on re-attach.
            CommandManager.RemovePreviewExecutedHandler(rtb, OnPreviewPasteExecuted);
            CommandManager.AddPreviewExecutedHandler(rtb, OnPreviewPasteExecuted);

            // Linked on load too: notes written before links existed have their addresses as plain text.
            if (!string.IsNullOrEmpty(block.RichTextContent))
                WithoutDirtyTracking(() =>
                {
                    DeserializeIntoRichTextBox(rtb, block.RichTextContent);
                    RichTextLinks.Linkify(rtb.Document);
                });

            ShowSearchTermIfFound(block.Id, rtb);
        }
    }

    // --- The note-list search, carried into the note it opened ---

    // Blocks load top to bottom, so the first one to contain the term gets the find bar.
    private bool _searchTermShown;

    private void ShowSearchTermIfFound(Guid blockId, RichTextBox rtb)
    {
        if (_searchTermShown || DataContext is not NoteEditorViewModel { SearchTerm: { } term })
            return;

        var text = new TextRange(rtb.Document.ContentStart, rtb.Document.ContentEnd).Text;
        if (!text.Contains(term, StringComparison.OrdinalIgnoreCase))
            return;

        _searchTermShown = true;

        // After this pass: the find bar and its text box register on their own Loaded.
        _ = Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() => ShowSearch(blockId, term)));
    }

    // Opens the find bar on a term without taking the focus: the list keeps it, so the
    // arrow keys still move from note to note.
    private void ShowSearch(Guid blockId, string term)
    {
        if (!_searchBars.TryGetValue(blockId, out var bar) || !_searchTextBoxes.TryGetValue(blockId, out var box))
            return;

        bar.Visibility = Visibility.Visible;
        box.Text = term;
        _searchDebounceTimer?.Stop();
        PerformSearch(blockId, term);
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

    // Ctrl+wheel zooms the blocks, anywhere in the editor. Tunnelling from the root, so it
    // runs before the RichTextBox forwards the wheel to the pane's scroll.
    private void OnEditorPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Control || Window.GetWindow(this)?.DataContext is not MainViewModel shell)
            return;

        e.Handled = true;
        shell.ZoomEditorCommand.Execute(Math.Sign(e.Delta));
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

    // --- Links ---

    private readonly HashSet<RichTextBox> _linkifyPending = [];

    // An address is complete once a space, a tab or a new line follows it: linking it on
    // every keystroke would wrap the first letters of an address still being typed.
    private static bool EndsAWord(RichTextBox rtb, ICollection<TextChange> changes) =>
        changes.Any(change => change.AddedLength > 0
            && rtb.Document.ContentStart.GetPositionAtOffset(change.Offset) is { } start
            && start.GetPositionAtOffset(change.AddedLength) is { } end
            && new TextRange(start, end).Text.Any(char.IsWhiteSpace));

    // Deferred until the edit is over (a paste is announced before it lands). The link is not
    // an edit of its own: the keystroke already marked the note, and a save can come in between.
    private void ScheduleLinkify(RichTextBox rtb)
    {
        if (!_linkifyPending.Add(rtb))
            return;

        _ = Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            _linkifyPending.Remove(rtb);
            WithoutDirtyTracking(() => RichTextLinks.Linkify(rtb.Document, rtb.Selection));
        }));
    }

    // Ctrl+Click opens the link under the pointer, in a text block, a checklist item or a
    // link block alike; a plain click still places the caret, so a link stays editable.
    // In a text block a plain click opens a link too — on the release, so a drag that
    // starts on a link still selects — and Alt+Click places the caret to edit its text.
    // In a TextBox (checklist item, link block) a click has to edit: Ctrl+Click only.
    private void OnLinkPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _linkPress = null;
        if (sender is not IInputElement host)
            return;

        var point = e.GetPosition(host);
        if (Keyboard.Modifiers == ModifierKeys.Control && LinkAt(sender, point) is Option<LinkUrl>.Some { Value: var url })
        {
            OpenLink(url);
            e.Handled = true;
            return;
        }

        if (sender is RichTextBox box && OpensOnPlainClick(Keyboard.Modifiers, e.ClickCount)
            && LinkAt(box, point) is Option<LinkUrl>.Some { Value: var pressed })
            _linkPress = new LinkPress(box, pressed, point);
    }

    private sealed record LinkPress(RichTextBox Box, LinkUrl Url, Point At);

    private LinkPress? _linkPress;

    internal static bool OpensOnPlainClick(ModifierKeys modifiers, int clickCount) =>
        modifiers == ModifierKeys.None && clickCount == 1;

    private void OnRichTextPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_linkPress is not { } press || !ReferenceEquals(press.Box, sender))
            return;

        _linkPress = null;
        if (ReleasedOnSameLink(press, e.GetPosition(press.Box)) is Option<LinkUrl>.Some { Value: var url })
            OpenLink(url);
    }

    // Not a drag, nothing selected, and still over the link it started on.
    internal static Option<LinkUrl> ReleasedOnSameLink(RichTextBox box, LinkUrl pressed, Point pressedAt, Point releasedAt) =>
        ReleasedOnSameLink(new LinkPress(box, pressed, pressedAt), releasedAt);

    private static Option<LinkUrl> ReleasedOnSameLink(LinkPress press, Point releasedAt)
    {
        var moved = releasedAt - press.At;
        if (Math.Abs(moved.X) > SystemParameters.MinimumHorizontalDragDistance
            || Math.Abs(moved.Y) > SystemParameters.MinimumVerticalDragDistance
            || !press.Box.Selection.IsEmpty)
            return Option<LinkUrl>.Empty();

        return LinkAt(press.Box, releasedAt) is Option<LinkUrl>.Some { Value: var url } && url == press.Url
            ? new Option<LinkUrl>.Some(url)
            : Option<LinkUrl>.Empty();
    }

    private static Option<LinkUrl> LinkAt(object host, Point point) => host switch
    {
        RichTextBox rtb when rtb.GetPositionFromPoint(point, snapToText: false) is { } position =>
            RichTextLinks.LinkAt(position),
        TextBox box when box.GetCharacterIndexFromPoint(point, snapToText: false) is >= 0 and var index =>
            TextLinks.At(box.Text, index),
        _ => Option<LinkUrl>.Empty()
    };

    // --- The hand over a link while Ctrl is down: what a Ctrl+Click would open ---

    private FrameworkElement? _linkHost;

    private void OnLinkHostMouseMove(object sender, MouseEventArgs e)
    {
        if (sender is not FrameworkElement host)
            return;

        _linkHost = host;
        UpdateLinkCursor(host, e.GetPosition(host), Keyboard.Modifiers);
    }

    private void OnLinkHostMouseLeave(object sender, MouseEventArgs e)
    {
        if (sender is FrameworkElement host)
            UpdateLinkCursor(host, default, ModifierKeys.None, leaving: true);
        _linkHost = null;
    }

    // Pressing or releasing Ctrl or Alt with the mouse still: no move comes to say so.
    private void OnEditorPreviewKeyChanged(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt && _linkHost is { } host)
            UpdateLinkCursor(host, Mouse.GetPosition(host), Keyboard.Modifiers);
    }

    // The hand shows over exactly what a click would open: in a text block a plain click or
    // a Ctrl+Click (not Alt, which edits), in a TextBox only a Ctrl+Click. ForceCursor: the
    // RichTextBox's own cursor logic would otherwise put its I-beam back.
    internal static void UpdateLinkCursor(FrameworkElement host, Point point, ModifierKeys modifiers, bool leaving = false)
    {
        var opens = host is RichTextBox
            ? modifiers is ModifierKeys.None or ModifierKeys.Control
            : modifiers == ModifierKeys.Control;
        var overLink = !leaving && opens && LinkAt(host, point).IsSome;
        if (overLink == (host.ForceCursor && host.Cursor == Cursors.Hand))
            return;

        if (overLink)
        {
            host.Cursor = Cursors.Hand;
            host.ForceCursor = true;
        }
        else
        {
            host.ClearValue(CursorProperty);
            host.ClearValue(ForceCursorProperty);
        }

        Mouse.UpdateCursor();
    }

    private void OnOpenChecklistLink(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: ChecklistItemViewModel item } && TextLinks.First(item.Text) is Option<LinkUrl>.Some { Value: var url })
            OpenLink(url);
    }

    private void OnOpenLinkBlock(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: BlockViewModel block } && LinkUrl.From(block.LinkUrlText).TryGet(out var url, out _))
            OpenLink(url);
    }

    private void OnOpenSecretUrl(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: BlockViewModel block } && LinkUrl.From(block.SecretUrl).TryGet(out var url, out _))
            OpenLink(url);
    }

    private void OnCopySecretUserName(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: BlockViewModel block })
            CopySecret(block.SecretUserName, "User name");
    }

    private void OnCopySecretPassword(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: BlockViewModel block })
            CopySecret(block.SecretPassword, "Password");
    }

    private void CopySecret(string value, string what)
    {
        if (value.Length == 0)
            return;

        var seconds = SensitiveClipboard.ClearAfter.TotalSeconds;
        Notify(SensitiveClipboard.Copy(value)
            ? $"{what} copied — wiped from the clipboard in {seconds:0} s."
            : "The clipboard is busy (another program has it open). Try again.");
    }

    // The shell's snackbar, when there is one (not in the view's tests).
    private void Notify(string message) =>
        (Window.GetWindow(this)?.DataContext as MainViewModel)?.MessageQueue.Enqueue(message);

    private void OpenLink(LinkUrl url) =>
        LinkLauncher.Open(url).Match(
            success: _ => { },
            failure: error => MessageBox.Show(error.Message, "Open link", MessageBoxButton.OK, MessageBoxImage.Warning));

    // --- Image paste & insert ---

    private void OnPreviewPasteExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.Command != ApplicationCommands.Paste || sender is not RichTextBox rtb)
            return;

        ScheduleLinkify(rtb);

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

    // Same toggle, "1." markers: both exporters already render Decimal lists.
    private void OnNumberedList(object sender, RoutedEventArgs e)
    {
        var rtb = FindRichTextBox(sender);
        if (rtb is null) return;

        rtb.Focus();
        EditingCommands.ToggleNumbering.Execute(null, rtb);
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

    // Enter in an item adds one below it and moves the caret there, the way every
    // checklist behaves; the row itself only exists after the next layout pass.
    private void OnChecklistItemKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox box || box.Tag is not ChecklistItemViewModel item || DataContext is not NoteEditorViewModel vm)
            return;

        if (ChecklistMoveOffset(e.Key == Key.System ? e.SystemKey : e.Key, Keyboard.Modifiers) is { } offset)
        {
            e.Handled = true;
            MoveChecklistItem(vm, item, offset, box.CaretIndex);
            return;
        }

        if (e.Key != Key.Enter)
            return;

        var block = vm.Blocks.FirstOrDefault(b => b.ChecklistItems.Contains(item));
        if (block is null)
            return;

        var added = vm.InsertChecklistItemAfter(block, item);
        e.Handled = true;

        _ = Dispatcher.BeginInvoke(DispatcherPriority.Loaded,
            new Action(() => FindChecklistTextBox(BlocksScrollViewer, added)?.Focus()));
    }

    // Alt turns the arrow keys into Key.System, with the real key in SystemKey.
    internal static int? ChecklistMoveOffset(Key key, ModifierKeys modifiers) =>
        modifiers != ModifierKeys.Alt ? null : key switch
        {
            Key.Up => -1,
            Key.Down => 1,
            _ => null
        };

    // The row is rebuilt by the move: the focus and the caret follow it to its new place.
    internal void MoveChecklistItem(NoteEditorViewModel vm, ChecklistItemViewModel item, int offset, int caretIndex)
    {
        if (!vm.MoveChecklistItem(item, offset))
            return;

        _ = Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            if (FindChecklistTextBox(BlocksScrollViewer, item) is not { } box)
                return;

            box.Focus();
            box.CaretIndex = Math.Min(caretIndex, box.Text.Length);
        }));
    }

    private static TextBox? FindChecklistTextBox(DependencyObject root, ChecklistItemViewModel item)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is TextBox box && ReferenceEquals(box.Tag, item))
                return box;
            if (FindChecklistTextBox(child, item) is { } hit)
                return hit;
        }

        return null;
    }

    // --- Drag and drop: files from Explorer, blocks by their grip ---

    // Tunnelling, from the root: a file dropped on a text block must not land in its
    // RichTextBox as a file path or an embedded object.
    private void OnEditorPreviewDragOver(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            return;

        e.Effects = DragDropEffects.Copy;
        e.Handled = true;
    }

    private void OnEditorPreviewDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths)
            return;

        e.Handled = true;
        DropFiles(paths);
    }

    internal void DropFiles(IReadOnlyList<string> paths)
    {
        if (DataContext is NoteEditorViewModel vm)
            vm.AddFileBlocks(paths);
    }

    private Point _gripDownAt;

    private void OnBlockGripMouseDown(object sender, MouseButtonEventArgs e) => _gripDownAt = e.GetPosition(this);

    private void OnBlockGripMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || sender is not FrameworkElement { DataContext: BlockViewModel block } grip)
            return;

        var moved = e.GetPosition(this) - _gripDownAt;
        if (Math.Abs(moved.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(moved.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        DragDrop.DoDragDrop(grip, new DataObject(typeof(BlockViewModel), block), DragDropEffects.Move);
    }

    private void OnBlockPreviewDragOver(object sender, DragEventArgs e)
    {
        if (sender is not Border { DataContext: BlockViewModel target } card || e.Data.GetData(typeof(BlockViewModel)) is not BlockViewModel dragged)
            return;

        e.Handled = true;
        if (ReferenceEquals(dragged, target))
        {
            e.Effects = DragDropEffects.None;
            HideDropMarker(card);
            return;
        }

        e.Effects = DragDropEffects.Move;
        ShowDropMarker(card, below: IsLowerHalf(card, e));
    }

    private void OnBlockDragLeave(object sender, DragEventArgs e)
    {
        if (sender is Border card)
            HideDropMarker(card);
    }

    private void OnBlockPreviewDrop(object sender, DragEventArgs e)
    {
        if (sender is not Border { DataContext: BlockViewModel target } card || e.Data.GetData(typeof(BlockViewModel)) is not BlockViewModel dragged)
            return;

        e.Handled = true;
        HideDropMarker(card);
        DropBlock(dragged, target, below: IsLowerHalf(card, e));
    }

    // Moving a block rebuilds its card, and a RichTextBox reloads from its block: whatever
    // was typed since the last sync would be lost, so every text block is synced first.
    internal void DropBlock(BlockViewModel dragged, BlockViewModel target, bool below)
    {
        if (DataContext is not NoteEditorViewModel vm || ReferenceEquals(dragged, target))
            return;

        var from = vm.Blocks.IndexOf(dragged);
        var to = vm.Blocks.IndexOf(target) + (below ? 1 : 0);
        if (from < to)
            to--; // the dragged block leaves its place first

        SyncAllRichTextBoxes();
        vm.MoveBlock(dragged, to);
    }

    private static bool IsLowerHalf(FrameworkElement card, DragEventArgs e) =>
        e.GetPosition(card).Y > card.ActualHeight / 2;

    private static void ShowDropMarker(Border card, bool below)
    {
        card.BorderBrush = (Brush)card.FindResource("AccentBrush");
        card.BorderThickness = below ? new Thickness(1, 1, 1, 4) : new Thickness(1, 4, 1, 1);
    }

    private static void HideDropMarker(Border card)
    {
        card.ClearValue(Border.BorderBrushProperty);
        card.BorderThickness = new Thickness(1);
    }

    // --- Export ---

    private void OnExportToPdf(object sender, RoutedEventArgs e) =>
        Export("PDF", "pdf", "PDF Files (*.pdf)|*.pdf", PdfExportService.Export);

    private void OnExportToWord(object sender, RoutedEventArgs e) =>
        Export("Word", "docx", "Word documents (*.docx)|*.docx", WordExportService.Export);

    private void OnExportToMarkdown(object sender, RoutedEventArgs e) =>
        Export("Markdown", "md", "Markdown (*.md)|*.md", MarkdownExportService.Export);

    // Every exporter takes the same blocks, in note order, and differs only in how it
    // renders them (Markdown writes its images to a folder next to the file).
    private void Export(string format, string extension, string filter,
        Action<string, IReadOnlyList<ExportBlock>, string> export)
    {
        if (DataContext is not NoteEditorViewModel vm) return;

        SyncAllRichTextBoxes();

        var blocks = vm.Blocks.Select<BlockViewModel, ExportBlock>(b => b.BlockType switch
        {
            BlockType.Text => new TextExportBlock(b.RichTextContent),
            BlockType.Link => new LinkExportBlock(b.LinkUrlText, b.LinkDescription),
            BlockType.Checklist => new ChecklistExportBlock(
                b.ChecklistItems
                    .Where(i => !string.IsNullOrWhiteSpace(i.Text))
                    .Select(i => new DocChecklistItem(i.Text.Trim(), i.IsDone))
                    .ToList()),
            // Never the password: it does not even reach the exporter.
            BlockType.Secret => new SecretExportBlock(b.SecretLabel.Trim(), b.SecretUserName.Trim(), b.SecretUrl.Trim()),
            _ => new FileExportBlock(b.FileName, b.FileSize)
        }).ToList();

        if (blocks.Count == 0)
        {
            MessageBox.Show("Nothing to export.", $"Export to {format}",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = filter,
            FileName = ExportFileName(vm.Title, extension),
            Title = $"Export to {format}"
        };

        if (dialog.ShowDialog() != true) return;

        try
        {
            export(ExportTitle(vm.Title), blocks, dialog.FileName);

            MessageBox.Show($"{format} exported successfully!", $"Export to {format}",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to export {format}: {ex.Message}", $"Export to {format}",
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
