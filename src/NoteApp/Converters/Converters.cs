using NoteApp.Domain.Models;
using System.Windows;

namespace NoteApp.Converters;

public sealed class NoteTypeToIconConverter : System.Windows.Data.IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) =>
        value is BlockType blockType
            ? blockType switch
            {
                BlockType.Text => "NoteText",
                BlockType.File => "FileDocument",
                BlockType.Link => "Link",
                _ => "Note"
            }
            : "Note";

    public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class FileSizeConverter : System.Windows.Data.IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) =>
        value is long bytes
            ? bytes switch
            {
                < 1024 => $"{bytes} B",
                < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
                < 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1} MB",
                _ => $"{bytes / (1024.0 * 1024 * 1024):F1} GB"
            }
            : "0 B";

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class BoolToVisibilityConverter : System.Windows.Data.IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) =>
        value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) =>
        value is Visibility.Visible;
}

public sealed class InverseBoolToVisibilityConverter : System.Windows.Data.IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) =>
        value is Visibility.Collapsed;
}

public sealed class ScrollHeightToRtbHeightConverter : System.Windows.Data.IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) =>
        value is double height and > 150 ? height - 100 : 300.0;

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class NullToVisibilityConverter : System.Windows.Data.IValueConverter
{
    // value == null  -> Visible (show placeholder); else Collapsed
    public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) =>
        value is null ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class NotePreviewConverter : System.Windows.Data.IValueConverter
{
    private const int MaxLength = 120;

    public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        if (value is not NoteApp.Domain.Models.Note note || note.IsEncrypted)
            return string.Empty;

        var firstText = note.Blocks
            .OrderBy(b => b.SortOrder)
            .OfType<NoteApp.Domain.Models.NoteBlock.Text>()
            .FirstOrDefault();
        if (firstText is null || string.IsNullOrEmpty(firstText.RichText))
            return string.Empty;

        var plain = ExtractPlainText(firstText.RichText);
        plain = System.Text.RegularExpressions.Regex.Replace(plain, @"\s+", " ").Trim();
        return plain.Length > MaxLength ? plain[..MaxLength] + "…" : plain;
    }

    private static string ExtractPlainText(string content)
    {
        var doc = new System.Windows.Documents.FlowDocument();
        // Try Base64 XamlPackage first
        try
        {
            var bytes = System.Convert.FromBase64String(content);
            using var ms = new System.IO.MemoryStream(bytes);
            var range = new System.Windows.Documents.TextRange(doc.ContentStart, doc.ContentEnd);
            range.Load(ms, System.Windows.DataFormats.XamlPackage);
            return new System.Windows.Documents.TextRange(doc.ContentStart, doc.ContentEnd).Text;
        }
        catch { /* not XamlPackage */ }

        // Try legacy plain XAML
        try
        {
            if (System.Windows.Markup.XamlReader.Parse(content) is System.Windows.Documents.FlowDocument parsed)
                return new System.Windows.Documents.TextRange(parsed.ContentStart, parsed.ContentEnd).Text;
        }
        catch { /* not XAML */ }

        // Last resort: treat as plain text
        return content;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) =>
        throw new NotSupportedException();
}
