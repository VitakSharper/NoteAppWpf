using NoteApp.Data.Queries;
using NoteApp.Domain.Functional;
using NoteApp.Domain.Models;
using NoteApp.Domain.ValueObjects;
using NoteApp.Services;

namespace NoteApp.Tests.Services;

public class ReminderTests
{
    private static readonly DateTime Now = new(2026, 9, 24, 15, 0, 0, DateTimeKind.Local);

    [Fact]
    public void A_reminder_fires_once_between_the_check_before_and_now()
    {
        var at = Now.ToUniversalTime();
        Reminder At(int minutes) => new(NoteId.New(), $"at {minutes}", null, at.AddMinutes(minutes));
        var reminders = new[] { At(-10), At(0), At(5), At(-1) };

        var due = ReminderSchedule.DueBetween(reminders, at.AddMinutes(-5), at);

        Assert.Equal(["at -1", "at 0"], due.Select(r => r.NoteTitle));
        Assert.Empty(ReminderSchedule.DueBetween(reminders, at, at)); // the next check: nothing again
    }

    [Fact]
    public void A_due_time_reads_as_a_day_and_a_time()
    {
        string Label(DateTime local) => ReminderSchedule.Label(local.ToUniversalTime(), Now);

        Assert.Equal("today 17:30", Label(Now.Date.AddHours(17.5)));
        Assert.Equal("tomorrow 09:00", Label(Now.Date.AddDays(1).AddHours(9)));
        Assert.Equal("yesterday 09:00", Label(Now.Date.AddDays(-1).AddHours(9)));
        Assert.EndsWith(" 09:00", Label(Now.Date.AddDays(3).AddHours(9)));
        Assert.Contains("2027", Label(Now.AddYears(1)));
    }

    [Fact]
    public void The_quick_choices_are_all_ahead_and_start_with_an_hour_from_now()
    {
        var choices = ReminderSchedule.QuickChoices(Now);

        Assert.Equal("In an hour", choices[0].Label);
        Assert.Equal(new DateTime(2026, 9, 24, 16, 0, 0), choices[0].Local);
        Assert.Contains(choices, c => c.Label.StartsWith("This evening"));
        Assert.All(choices, c => Assert.True(c.Local > Now));
        Assert.Equal(DayOfWeek.Monday, choices.Single(c => c.Label.StartsWith("Next Monday")).Local.DayOfWeek);

        // Late in the day there is no "this evening" left.
        Assert.DoesNotContain(ReminderSchedule.QuickChoices(Now.Date.AddHours(19)), c => c.Label.StartsWith("This evening"));
    }

    [Fact]
    public void The_reminders_view_groups_by_how_soon()
    {
        ReminderSchedule.Group GroupOf(DateTime local) => ReminderSchedule.GroupOf(local.ToUniversalTime(), Now);

        Assert.Equal(ReminderSchedule.Group.Overdue, GroupOf(Now.AddMinutes(-1)));
        Assert.Equal(ReminderSchedule.Group.Today, GroupOf(Now.AddHours(2)));
        Assert.Equal(ReminderSchedule.Group.Tomorrow, GroupOf(Now.Date.AddDays(1).AddHours(8)));
        Assert.Equal(ReminderSchedule.Group.Later, GroupOf(Now.AddDays(5)));
    }

    [Fact]
    public void An_item_keeps_its_due_time_through_the_json()
    {
        var due = new DateTime(2026, 9, 25, 7, 0, 0, DateTimeKind.Utc);

        var json = ChecklistJson.Serialize([new ChecklistItem("call", false, due), new ChecklistItem("plain", false)]);
        var back = ChecklistJson.Deserialize(json);

        Assert.Contains(ChecklistJson.DueMarker, json);
        Assert.Equal(due, back[0].Due);
        Assert.Equal(DateTimeKind.Utc, back[0].Due!.Value.Kind);
        Assert.Null(back[1].Due);
    }

    [Fact]
    public async Task Reminders_are_the_notes_own_and_their_undone_items()
    {
        var service = new NoteService(new Rows());

        var reminders = (await service.RemindersAsync()).Unwrap();

        Assert.Equal([("Plan", null), ("Plan", "call the bank")], reminders.Select(r => (r.NoteTitle, r.ItemText)));
        Assert.Equal("call the bank — Plan", reminders[1].Label);
    }

    private sealed class Rows : ThrowingNoteRepository
    {
        public override Task<Result<IReadOnlyList<ReminderRow>, AppError>> RemindersAsync() =>
            Task.FromResult(Result<IReadOnlyList<ReminderRow>, AppError>.Ok(
            [
                new ReminderRow
                {
                    NoteId = Guid.NewGuid(),
                    Title = "Plan",
                    RemindAt = new DateTime(2026, 9, 25, 7, 0, 0),
                    ChecklistJson =
                    [
                        ChecklistJson.Serialize([
                            new ChecklistItem("call the bank", false, new DateTime(2026, 9, 25, 8, 0, 0, DateTimeKind.Utc)),
                            new ChecklistItem("already done", true, new DateTime(2026, 9, 25, 6, 0, 0, DateTimeKind.Utc)),
                            new ChecklistItem("no reminder", false)])
                    ]
                }
            ]));
    }
}
