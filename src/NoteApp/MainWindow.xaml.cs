using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using NoteApp.Domain.Models;
using NoteApp.ViewModels;
using NoteApp.Views;

namespace NoteApp;

public partial class MainWindow : Window
{
    public static readonly RoutedCommand FocusSearchCommand = new(nameof(FocusSearchCommand), typeof(MainWindow));

    private bool _closeConfirmed;

    public MainWindow()
    {
        InitializeComponent();
        Icon = CreateAppIcon();
    }

    // Ctrl+F: search the text block the focus is in; from anywhere else, search the notes.
    private void OnFocusSearch(object sender, ExecutedRoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm || vm.IsSettingsOpen)
            return;

        var focused = Keyboard.FocusedElement as DependencyObject;
        if (FindTextBlockOwner(focused) is { } block && FindAncestor<NoteEditorView>(focused) is { } editor)
        {
            editor.OpenSearch(block.Id);
            return;
        }

        vm.NavigateToNotesCommand.Execute(null);

        // The middle pane may have just swapped to the list: its template is only
        // instantiated on the next layout pass, so the box does not exist yet.
        _ = Dispatcher.BeginInvoke(DispatcherPriority.Loaded,
            new Action(() => FindDescendant<NoteListView>(MiddlePane)?.FocusSearchBox()));
    }

    // Every element of a text block card — the RichTextBox, its toolbar buttons, its
    // search bar — carries the block as its Tag, so the owner is the nearest one.
    private static BlockViewModel? FindTextBlockOwner(DependencyObject? start)
    {
        for (var node = start; node is not null; node = GetParent(node))
        {
            if (node is FrameworkElement { Tag: BlockViewModel { BlockType: BlockType.Text } block })
                return block;
        }

        return null;
    }

    private static T? FindAncestor<T>(DependencyObject? start) where T : DependencyObject
    {
        for (var node = start; node is not null; node = GetParent(node))
        {
            if (node is T hit)
                return hit;
        }

        return null;
    }

    private static T? FindDescendant<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T hit)
                return hit;
            if (FindDescendant<T>(child) is { } deeper)
                return deeper;
        }

        return null;
    }

    // Focus can sit on a content element (a FlowDocument part), which has no visual parent.
    private static DependencyObject? GetParent(DependencyObject node) =>
        node is Visual ? VisualTreeHelper.GetParent(node) : LogicalTreeHelper.GetParent(node);

    // Closing cannot await, so when there IS something to ask the first pass is
    // cancelled and the window closes itself again once the guard has an answer.
    private async void OnClosing(object sender, CancelEventArgs e)
    {
        // Nothing to guard: let this pass close the window. Cancelling here and
        // re-closing would only be a detour — and the guard answers synchronously in
        // that case, so the re-close would land inside this very Closing pass, which
        // WPF forbids ("...while a Window is closing").
        if (_closeConfirmed || DataContext is not MainViewModel vm || !vm.HasUnsavedChanges)
            return;

        e.Cancel = true;

        if (!await vm.ConfirmLeaveEditorAsync())
            return;

        _closeConfirmed = true;

        // Queue it: answering the dialog can also complete synchronously, and Close()
        // is illegal until this handler has returned.
        _ = Dispatcher.BeginInvoke(new Action(Close));
    }

    private static BitmapSource CreateAppIcon()
    {
        const int size = 128;
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            var scale = size / 24.0;

            // Background rounded square
            dc.DrawRoundedRectangle(
                new SolidColorBrush(Color.FromRgb(63, 81, 181)),
                null,
                new Rect(0, 0, size, size),
                size * 0.18, size * 0.18);

            dc.PushTransform(new ScaleTransform(scale, scale));

            // Note paper
            dc.DrawGeometry(Brushes.White, null,
                Geometry.Parse("M5,2 L15,2 L20,7 L20,22 L5,22 Z"));

            // Fold
            dc.DrawGeometry(new SolidColorBrush(Color.FromArgb(120, 63, 81, 181)), null,
                Geometry.Parse("M15,2 L15,7 L20,7 Z"));

            // Text lines
            var lineBrush = new SolidColorBrush(Color.FromArgb(90, 63, 81, 181));
            dc.DrawRoundedRectangle(lineBrush, null, new Rect(7.5, 10, 9, 1.4), 0.7, 0.7);
            dc.DrawRoundedRectangle(lineBrush, null, new Rect(7.5, 13, 6.5, 1.4), 0.7, 0.7);
            dc.DrawRoundedRectangle(lineBrush, null, new Rect(7.5, 16, 8, 1.4), 0.7, 0.7);

            // Pencil
            dc.DrawGeometry(new SolidColorBrush(Color.FromRgb(255, 193, 7)),
                null, Geometry.Parse("M18.5,14 L21.5,11 L22.5,12 L19.5,15 Z"));
            dc.DrawGeometry(new SolidColorBrush(Color.FromRgb(255, 152, 0)),
                null, Geometry.Parse("M18.5,14 L18,15.8 L19.5,15 Z"));

            dc.Pop();
        }

        var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }
}