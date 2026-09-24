using System.Globalization;
using NoteApp.Domain.Functional;

namespace NoteApp.Services;

public enum AutoBackupInterval
{
    Off,
    Daily,
    Weekly
}

// When an automatic backup is due and which old ones go. Pure: the backups are known by
// their folder names alone (BackupService writes NoteApp_<timestamp>\NoteApp_<timestamp>.zip),
// so nothing else has to remember when the last one ran.
public static class BackupSchedule
{
    public const string FolderPrefix = "NoteApp_";
    public const string TimestampFormat = "yyyy-MM-dd_HHmmss";

    public static string FolderName(DateTime timestamp) =>
        FolderPrefix + timestamp.ToString(TimestampFormat, CultureInfo.InvariantCulture);

    // Anything else in the folder is not ours: it is neither counted nor ever deleted.
    public static Option<DateTime> TimestampOf(string folderName) =>
        folderName.StartsWith(FolderPrefix, StringComparison.Ordinal)
        && DateTime.TryParseExact(folderName[FolderPrefix.Length..], TimestampFormat,
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var timestamp)
            ? new Option<DateTime>.Some(timestamp)
            : Option<DateTime>.Empty();

    public static TimeSpan? Period(AutoBackupInterval interval) => interval switch
    {
        AutoBackupInterval.Daily => TimeSpan.FromDays(1),
        AutoBackupInterval.Weekly => TimeSpan.FromDays(7),
        _ => null
    };

    // Manual backups count too: one taken by hand this morning makes the daily one unnecessary.
    public static bool IsDue(AutoBackupInterval interval, IEnumerable<string> folderNames, DateTime now)
    {
        if (Period(interval) is not { } period)
            return false;

        var latest = Timestamps(folderNames).DefaultIfEmpty(DateTime.MinValue).Max();
        return now - latest >= period;
    }

    // The oldest beyond the newest `keep`; keep = 0 keeps everything.
    public static IReadOnlyList<string> ToDelete(IEnumerable<string> folderNames, int keep) =>
        keep <= 0
            ? []
            : folderNames
                .Select(name => (Name: name, Timestamp: TimestampOf(name)))
                .Where(b => b.Timestamp.IsSome)
                .OrderByDescending(b => b.Timestamp.GetValueOrDefault(DateTime.MinValue))
                .Skip(keep)
                .Select(b => b.Name)
                .ToList();

    private static IEnumerable<DateTime> Timestamps(IEnumerable<string> folderNames) =>
        folderNames
            .Select(TimestampOf)
            .OfType<Option<DateTime>.Some>()
            .Select(s => s.Value);
}
