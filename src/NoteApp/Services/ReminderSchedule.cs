using System.Globalization;
using NoteApp.Domain.Models;

namespace NoteApp.Services;

// The rules of reminders, clock passed in (like IdleLock): which ones fall due between two
// checks, how a due time reads, the quick choices, and the Reminders view's groups.
public static class ReminderSchedule
{
    // Due after the previous check and by now: each reminder fires once, and one that fell
    // due while NoteApp was closed fires at the first check after it starts.
    public static IReadOnlyList<Reminder> DueBetween(IEnumerable<Reminder> reminders, DateTime afterUtc, DateTime byUtc) =>
        reminders.Where(r => r.DueUtc > afterUtc && r.DueUtc <= byUtc).OrderBy(r => r.DueUtc).ToList();

    public static string Label(DateTime dueUtc, DateTime nowLocal)
    {
        var due = DateTime.SpecifyKind(dueUtc, DateTimeKind.Utc).ToLocalTime();
        var time = due.ToString("HH:mm", CultureInfo.CurrentCulture);
        var day = (due.Date - nowLocal.Date).Days switch
        {
            0 => "today",
            1 => "tomorrow",
            -1 => "yesterday",
            > 1 and < 7 => due.ToString("dddd", CultureInfo.CurrentCulture),
            _ when due.Year == nowLocal.Year => due.ToString("ddd d MMM", CultureInfo.CurrentCulture),
            _ => due.ToString("d MMM yyyy", CultureInfo.CurrentCulture)
        };
        return $"{day} {time}";
    }

    public static bool IsOverdue(DateTime dueUtc, DateTime nowUtc) => dueUtc <= nowUtc;

    // The one-click choices of the reminder window, in local time: an hour from now (on a
    // five-minute mark), this evening while it is still ahead, tomorrow morning, next Monday.
    public static IReadOnlyList<(string Label, DateTime Local)> QuickChoices(DateTime nowLocal)
    {
        var choices = new List<(string, DateTime)>();
        var inAnHour = nowLocal.AddHours(1);
        inAnHour = new DateTime(inAnHour.Year, inAnHour.Month, inAnHour.Day, inAnHour.Hour, inAnHour.Minute / 5 * 5, 0, nowLocal.Kind);
        choices.Add(("In an hour", inAnHour));

        var evening = nowLocal.Date.AddHours(18);
        if (evening > nowLocal.AddMinutes(30))
            choices.Add(("This evening, 18:00", evening));

        choices.Add(("Tomorrow, 09:00", nowLocal.Date.AddDays(1).AddHours(9)));

        var toMonday = ((int)DayOfWeek.Monday - (int)nowLocal.DayOfWeek + 7) % 7;
        choices.Add(("Next Monday, 09:00", nowLocal.Date.AddDays(toMonday == 0 ? 7 : toMonday).AddHours(9)));
        return choices;
    }

    public enum Group { Overdue, Today, Tomorrow, Later }

    public static Group GroupOf(DateTime dueUtc, DateTime nowLocal)
    {
        var due = DateTime.SpecifyKind(dueUtc, DateTimeKind.Utc).ToLocalTime();
        if (due <= nowLocal)
            return Group.Overdue;
        return (due.Date - nowLocal.Date).Days switch
        {
            0 => Group.Today,
            1 => Group.Tomorrow,
            _ => Group.Later
        };
    }
}
