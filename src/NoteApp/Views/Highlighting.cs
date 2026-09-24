using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using NoteApp.Services;

namespace NoteApp.Views;

// A TextBlock that marks the search terms in its text (TextHighlights): set Text and Terms
// instead of TextBlock.Text. The note list uses it on titles and previews.
public static class Highlighting
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.RegisterAttached(
        "Text", typeof(string), typeof(Highlighting), new PropertyMetadata(null, OnChanged));

    public static readonly DependencyProperty TermsProperty = DependencyProperty.RegisterAttached(
        "Terms", typeof(IReadOnlyList<string>), typeof(Highlighting), new PropertyMetadata(null, OnChanged));

    public static string? GetText(DependencyObject element) => (string?)element.GetValue(TextProperty);
    public static void SetText(DependencyObject element, string? value) => element.SetValue(TextProperty, value);

    public static IReadOnlyList<string>? GetTerms(DependencyObject element) => (IReadOnlyList<string>?)element.GetValue(TermsProperty);
    public static void SetTerms(DependencyObject element, IReadOnlyList<string>? value) => element.SetValue(TermsProperty, value);

    private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBlock block)
            return;

        block.Inlines.Clear();
        foreach (var (text, isMatch) in TextHighlights.Split(GetText(block) ?? string.Empty, GetTerms(block) ?? []))
        {
            var run = new Run(text);
            if (isMatch)
            {
                run.Background = RichTextFormat.Highlighter;
                run.FontWeight = FontWeights.SemiBold;
            }

            block.Inlines.Add(run);
        }
    }
}
