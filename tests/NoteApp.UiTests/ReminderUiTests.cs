using NoteApp.Data.Queries;
using NoteApp.Domain.Functional;
using NoteApp.Domain.Models;
using NoteApp.Domain.ValueObjects;
using NoteApp.Services;
using NoteApp.ViewModels;
using NoteApp.Views;
using static NoteApp.UiTests.Notes;

namespace NoteApp.UiTests;

public class ReminderUiTests
{
    [Fact]
    public void A_due_reminder_is_announced_once() => Wpf.Run(() =>
    {
        // Due after the shell started (its first check counts from then), before the checks.
        var repo = new OneReminder(DateTime.UtcNow.AddSeconds(30));
        var shell = FocusModeUiTests.Shell(repo);
        var announced = new List<string>();
        shell.ReminderDue += (title, text, _) => announced.Add($"{title}: {text}");

        shell.CheckRemindersAsync(DateTime.UtcNow.AddMinutes(1)).GetAwaiter().GetResult();
        shell.CheckRemindersAsync(DateTime.UtcNow.AddMinutes(2)).GetAwaiter().GetResult();

        Assert.Equal(["Reminder: Dentist"], announced);
    });

    [Fact]
    public void The_reminders_page_lists_them_and_removes_a_notes_own() => Wpf.Run(() =>
    {
        var repo = new OneReminder(DateTime.UtcNow.AddHours(-2));
        var page = new RemindersViewModel(new NoteService(repo));

        page.LoadCommand.ExecuteAsync(null).GetAwaiter().GetResult();
        var item = Assert.Single(page.Items);
        Assert.True(item.IsOverdue);
        Assert.Equal("Overdue", item.Group);

        page.ClearCommand.ExecuteAsync(item).GetAwaiter().GetResult();
        Assert.Empty(page.Items);
        Assert.Null(repo.RemindAt);
    });

    [Fact]
    public void Setting_a_reminder_on_an_open_note_shows_next_to_its_title() => Wpf.Run(() =>
    {
        var repo = new OneReminder(null);
        using var editor = new OpenEditor(With(Text("teeth")), repository: repo);
        var at = DateTime.UtcNow.AddDays(1);

        editor.ViewModel.SetReminderAsync(at).GetAwaiter().GetResult();
        Wpf.Pump();

        Assert.Equal(at, repo.RemindAt);
        Assert.True(editor.ViewModel.HasReminder);
        Assert.StartsWith("tomorrow", editor.ViewModel.ReminderLabel);
        Assert.Contains(editor.Find<System.Windows.Controls.TextBlock>(), t => t.IsVisible && t.Text.StartsWith("🔔 tomorrow"));
        Assert.False(editor.ViewModel.IsDirty); // not content: nothing to save
    });

    [Fact]
    public void An_items_reminder_is_saved_with_the_checklist() => Wpf.Run(() =>
    {
        var repo = new OneReminder(null);
        using var editor = new OpenEditor(With(Checklist("call the bank")), repository: repo);
        var due = DateTime.UtcNow.AddHours(3);

        editor.ViewModel.Blocks[0].ChecklistItems[0].Due = due;
        editor.ViewModel.SaveCommand.ExecuteAsync(null).GetAwaiter().GetResult();

        var item = Assert.IsType<NoteBlock.Checklist>(Assert.Single(repo.Updated!.Blocks)).Items.Single();
        Assert.Equal(due, item.Due);
    });

    [Fact]
    public void The_reminder_window_hands_back_a_quick_choice_in_utc() => Wpf.Run(() =>
    {
        var window = new ReminderWindow("Remind me", null) { Left = -10000, Top = -10000, ShowActivated = false };
        window.Show();
        var tomorrowNine = DateTime.Now.Date.AddDays(1).AddHours(9);

        window.Finish(tomorrowNine);

        Assert.Equal(tomorrowNine.ToUniversalTime(), window.Chosen);
        Assert.False(window.Removed);
    });

    private sealed class OneReminder(DateTime? remindAt) : UnusedNoteRepository
    {
        private readonly Guid _id = Guid.NewGuid();

        public DateTime? RemindAt { get; private set; } = remindAt;
        public Note? Updated { get; private set; }

        public override Task<Result<IReadOnlyList<ReminderRow>, AppError>> RemindersAsync() =>
            Task.FromResult(Result<IReadOnlyList<ReminderRow>, AppError>.Ok(RemindAt is null ? [] :
                [new ReminderRow { NoteId = _id, Title = "Dentist", RemindAt = RemindAt }]));

        public override Task<Result<Unit, AppError>> SetReminderAsync(NoteId id, DateTime? remindAtUtc)
        {
            RemindAt = remindAtUtc;
            return Task.FromResult(Result<Unit, AppError>.Ok(Unit.Value));
        }

        public override Task<Result<Note, AppError>> UpdateAsync(Note note, byte[]? encryptedContent = null)
        {
            Updated = note;
            return Task.FromResult(Result<Note, AppError>.Ok(note));
        }
    }
}

public class UpdateOfferUiTests
{
    [Fact]
    public void GitHub_is_asked_once_a_day_and_only_when_the_setting_is_on() => Wpf.Run(() =>
    {
        var shell = FocusModeUiTests.Shell();
        var asked = 0;
        Task<Option<LatestRelease>> Latest()
        {
            asked++;
            return Task.FromResult<Option<LatestRelease>>(new Option<LatestRelease>.Some(
                new LatestRelease(new Version(9, 0, 0), "v9.0.0", new Uri("https://github.com/VitakSharper/NoteAppWpf/releases/tag/v9.0.0"))));
        }
        var now = DateTime.UtcNow;

        shell.OfferUpdateAsync(Latest, new Version(1, 1, 0), now).GetAwaiter().GetResult();
        shell.OfferUpdateAsync(Latest, new Version(1, 1, 0), now.AddHours(2)).GetAwaiter().GetResult();
        Assert.Equal(1, asked);

        shell.SettingsViewModel.CheckForUpdates = false;
        shell.SettingsViewModel.SaveCommand.Execute(null);
        shell.OfferUpdateAsync(Latest, new Version(1, 1, 0), now.AddDays(2)).GetAwaiter().GetResult();
        Assert.Equal(1, asked);
    });
}
