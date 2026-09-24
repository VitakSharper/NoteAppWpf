using System.Globalization;
using System.Windows.Data;
using NoteApp.Domain.Models;

namespace NoteApp.ViewModels;

// The headings of the note list under a date sort: pinned notes first, then Today,
// Yesterday, the previous 7 and 30 days, then one heading per month. Stored dates are
// UTC; the days are the user's. A title sort has no headings at all.
public static class NoteGroups
{
    public const string Pinned = "Pinned";

    // The UI is in English, and so are the "Sep 24" dates WPF formats on each row.
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-US");

    public static string? Label(NoteSummary note, SortOption sort, DateTime nowLocal)
    {
        var date = sort.Field switch
        {
            nameof(NoteSummary.UpdatedAt) => note.UpdatedAt,
            nameof(NoteSummary.CreatedAt) => note.CreatedAt,
            _ => (DateTime?)null
        };
        if (date is null)
            return null;
        if (note.IsPinned)
            return Pinned;

        var day = ToLocal(date.Value).Date;
        var today = nowLocal.Date;
        return (today - day).Days switch
        {
            <= 0 => "Today",
            1 => "Yesterday",
            < 7 => "Previous 7 days",
            < 30 => "Previous 30 days",
            _ => day.ToString("MMMM yyyy", English)
        };
    }

    public static bool IsGrouped(SortOption sort) => sort.Field is nameof(NoteSummary.UpdatedAt) or nameof(NoteSummary.CreatedAt);

    // SQL Server hands the UTC values back as Unspecified.
    private static DateTime ToLocal(DateTime stored) =>
        stored.Kind == DateTimeKind.Local ? stored : DateTime.SpecifyKind(stored, DateTimeKind.Utc).ToLocalTime();

    // Grouping the list's collection view by the item itself, through the label.
    public sealed class Converter(SortOption sort, DateTime nowLocal) : IValueConverter
    {
        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is NoteSummary note ? Label(note, sort, nowLocal) : null;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
