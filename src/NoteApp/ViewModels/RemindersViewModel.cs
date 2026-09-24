using System.Collections.ObjectModel;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NoteApp.Domain.Models;
using NoteApp.Domain.ValueObjects;
using NoteApp.Services;

namespace NoteApp.ViewModels;

// One line of the Reminders view.
public sealed record ReminderItem(Reminder Reminder, string When, bool IsOverdue, string Group)
{
    public string Title => Reminder.ItemText ?? Reminder.NoteTitle;
    public string? Note => Reminder.IsForItem ? Reminder.NoteTitle : null;
    // An item's reminder is part of the note's content: it is changed there, not here.
    public bool CanClear => !Reminder.IsForItem;
}

// The middle pane's Reminders page: every reminder, overdue first, grouped by day.
public partial class RemindersViewModel(NoteService noteService) : ObservableObject
{
    [ObservableProperty] private ObservableCollection<ReminderItem> _items = [];
    [ObservableProperty] private bool _isLoading;

    public event Action<NoteId>? OpenNoteRequested;
    public event Action<string>? ShowMessage;

    [RelayCommand]
    public async Task Load()
    {
        IsLoading = true;
        try
        {
            if (!(await noteService.RemindersAsync()).TryGet(out var reminders, out var error))
            {
                ShowMessage?.Invoke(error.Message);
                return;
            }

            var now = DateTime.Now;
            var nowUtc = DateTime.UtcNow;
            Items = new ObservableCollection<ReminderItem>(reminders.Select(r => new ReminderItem(
                r,
                ReminderSchedule.Label(r.DueUtc, now),
                ReminderSchedule.IsOverdue(r.DueUtc, nowUtc),
                ReminderSchedule.GroupOf(r.DueUtc, now).ToString())));

            var view = CollectionViewSource.GetDefaultView(Items);
            view.GroupDescriptions.Clear();
            view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(ReminderItem.Group)));
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void Open(ReminderItem item) => OpenNoteRequested?.Invoke(item.Reminder.NoteId);

    [RelayCommand]
    private async Task Clear(ReminderItem item)
    {
        if (!item.CanClear)
            return;

        if ((await noteService.SetReminderAsync(item.Reminder.NoteId, null)).TryGet(out _, out var error))
            Items.Remove(item);
        else
            ShowMessage?.Invoke(error.Message);
    }
}
