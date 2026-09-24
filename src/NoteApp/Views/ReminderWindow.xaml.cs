using System.Windows;
using System.Windows.Input;
using NoteApp.Services;

namespace NoteApp.Views;

// When to be reminded of a note or of a checklist item. A dialog rather than a popup: the
// date and time pickers open popups of their own. Chosen is the answer, in UTC; Removed says
// the reminder is to go. Neither set: cancelled.
public partial class ReminderWindow : Window
{
    public DateTime? Chosen { get; private set; }
    public bool Removed { get; private set; }

    public ReminderWindow(string what, DateTime? currentUtc)
    {
        InitializeComponent();
        What.Text = what;

        var now = DateTime.Now;
        Choices.ItemsSource = ReminderSchedule.QuickChoices(now).Select(c => new { c.Label, c.Local }).ToList();

        var start = currentUtc is { } current ? DateTime.SpecifyKind(current, DateTimeKind.Utc).ToLocalTime() : now.Date.AddDays(1).AddHours(9);
        Day.SelectedDate = start.Date;
        Time.SelectedTime = start;
        ClearButton.Visibility = currentUtc is null ? Visibility.Collapsed : Visibility.Visible;
    }

    private void OnChoice(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: DateTime local })
            Finish(local);
    }

    private void OnSet(object sender, RoutedEventArgs e)
    {
        if (Day.SelectedDate is not { } day || Time.SelectedTime is not { } time)
        {
            ShowError("Pick a day and a time.");
            return;
        }

        var local = day.Date.Add(time.TimeOfDay);
        if (local <= DateTime.Now)
        {
            ShowError("That time has already passed.");
            return;
        }

        Finish(local);
    }

    private void OnClear(object sender, RoutedEventArgs e)
    {
        Removed = true;
        DialogResult = true;
    }

    private void OnCancel(object sender, ExecutedRoutedEventArgs e) => Close();

    internal void Finish(DateTime local)
    {
        Chosen = DateTime.SpecifyKind(local, DateTimeKind.Local).ToUniversalTime();
        try
        {
            DialogResult = true;
        }
        catch (InvalidOperationException)
        {
            Close(); // shown with Show() rather than ShowDialog()
        }
    }

    private void ShowError(string error)
    {
        Error.Text = error;
        Error.Visibility = Visibility.Visible;
    }
}
