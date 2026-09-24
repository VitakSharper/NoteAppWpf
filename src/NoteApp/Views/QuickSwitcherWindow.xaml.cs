using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using NoteApp.ViewModels;

namespace NoteApp.Views;

// Ctrl+K. Shown with ShowDialog; Chosen is what Enter or a click picked (null: nothing).
public partial class QuickSwitcherWindow : Window
{
    public static readonly DependencyProperty TermsProperty = DependencyProperty.Register(
        nameof(Terms), typeof(IReadOnlyList<string>), typeof(QuickSwitcherWindow), new PropertyMetadata(Array.Empty<string>()));

    private readonly IReadOnlyList<QuickSwitchEntry> _entries;
    private bool _closing;

    public QuickSwitcherWindow(IReadOnlyList<QuickSwitchEntry> entries)
    {
        InitializeComponent();
        _entries = entries;
        Filter();
        Loaded += (_, _) => QueryBox.Focus();
        Closing += (_, _) => _closing = true;
    }

    public QuickSwitchEntry? Chosen { get; private set; }

    // The words typed, which the results highlight.
    public IReadOnlyList<string> Terms
    {
        get => (IReadOnlyList<string>)GetValue(TermsProperty);
        set => SetValue(TermsProperty, value);
    }

    internal void Filter()
    {
        Terms = QueryBox.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var results = QuickSwitch.Rank(QueryBox.Text, _entries, e => e.Label);
        Results.ItemsSource = results;
        Results.SelectedIndex = results.Count > 0 ? 0 : -1;
        NothingFound.Visibility = results.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnQueryChanged(object sender, TextChangedEventArgs e) => Filter();

    private void OnQueryKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Down or Key.Up when Results.Items.Count > 0:
                Results.SelectedIndex = Math.Clamp(Results.SelectedIndex + (e.Key == Key.Down ? 1 : -1), 0, Results.Items.Count - 1);
                Results.ScrollIntoView(Results.SelectedItem);
                e.Handled = true;
                break;
            case Key.Enter:
                Choose(Results.SelectedItem as QuickSwitchEntry);
                e.Handled = true;
                break;
            case Key.Escape:
                Choose(null);
                e.Handled = true;
                break;
        }
    }

    private void OnResultClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source && ItemsControl.ContainerFromElement(Results, source) is ListBoxItem { DataContext: QuickSwitchEntry entry })
            Choose(entry);
    }

    // A click outside is a way of saying no.
    private void OnDeactivated(object? sender, EventArgs e)
    {
        if (!_closing)
            Choose(null);
    }

    internal void Choose(QuickSwitchEntry? entry)
    {
        if (_closing)
            return;

        Chosen = entry;
        _closing = true;
        try
        {
            DialogResult = entry is not null;
        }
        catch (InvalidOperationException)
        {
            Close(); // shown with Show() rather than ShowDialog()
        }
    }
}
