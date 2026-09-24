using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaterialDesignThemes.Wpf;
using NoteApp.Data.Repositories;
using System.IO;
using NoteApp.Domain.Models;
using NoteApp.Domain.ValueObjects;
using NoteApp.Services;
using NoteApp.Views;

namespace NoteApp.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly NoteService _noteService;
    private readonly ITagRepository _tagRepository;
    private readonly AppSettingsService _settingsService;
    private readonly AutoBackupService _autoBackup;
    private readonly DraftStore _drafts;

    // Middle pane: NoteList <-> TagManager
    [ObservableProperty] private ObservableObject? _middlePaneContent;
    // Right pane: a NoteEditorViewModel, or null => empty-state placeholder
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCurrentEditorCommand), nameof(DismissCommand), nameof(DeleteOpenNoteCommand), nameof(ToggleFocusModeCommand))]
    private ObservableObject? _currentEditor;
    // Rail and note list hidden, the editor gets the whole window (MainWindow collapses the columns).
    [ObservableProperty] private bool _isFocusMode;
    // Scale of the editor's blocks; kept in the settings.
    [ObservableProperty] private double _editorZoom = 1.0;
    // Settings modal (hosted in RootDialog). The keyboard shortcuts stay inert while
    // it is open: Ctrl+N would otherwise create a note underneath the overlay.
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateNoteCommand), nameof(SaveCurrentEditorCommand), nameof(DismissCommand), nameof(DeleteOpenNoteCommand), nameof(ToggleFocusModeCommand))]
    private bool _isSettingsOpen;

    public NoteListViewModel NoteListViewModel { get; }
    public TagManagerViewModel TagManagerViewModel { get; }
    public SettingsViewModel SettingsViewModel { get; }
    public SnackbarMessageQueue MessageQueue { get; } = new(TimeSpan.FromSeconds(3));

    public MainViewModel(
        NoteService noteService,
        AppSettingsService settingsService,
        ITagRepository tagRepository,
        NoteListViewModel noteListViewModel,
        TagManagerViewModel tagManagerViewModel,
        SettingsViewModel settingsViewModel,
        AutoBackupService autoBackup,
        DraftStore drafts)
    {
        _drafts = drafts;
        _noteService = noteService;
        _settingsService = settingsService;
        _autoBackup = autoBackup;
        _tagRepository = tagRepository;
        NoteListViewModel = noteListViewModel;
        TagManagerViewModel = tagManagerViewModel;
        SettingsViewModel = settingsViewModel;

        noteListViewModel.EditNoteRequested += OnEditNoteRequested;
        noteListViewModel.CreateNoteRequested += OnCreateNoteRequested;
        noteListViewModel.ShowMessage += OnShowMessage;
        noteListViewModel.ShowUndoableMessage += OnShowUndoableMessage;
        noteListViewModel.NoteDeleted += OnNoteDeleted;

        tagManagerViewModel.ShowMessage += OnShowMessage;
        settingsViewModel.ShowMessage += OnShowMessage;
        settingsViewModel.CloseRequested += () => IsSettingsOpen = false;

        // Checked four times a minute rather than restarted on every keystroke; it only
        // runs while an encrypted note is actually open.
        _lockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
        _lockTimer.Tick += OnLockTimerTick;

        // First look shortly after startup (not during it), then once an hour.
        _backupTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
        _backupTimer.Tick += OnBackupTimerTick;
        _backupTimer.Start();

        _draftTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(20) };
        _draftTimer.Tick += (_, _) => KeepDraft();
        _draftTimer.Start();

        // Once the window is up: the question needs an owner to appear over.
        _ = Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(OfferDraftsLeftBehind));

        EditorZoom = _settingsService.Current.EditorZoom;

        // Startup: choose middle pane; Settings.LaunchPage opens the dialog over Notes
        MiddlePaneContent = _settingsService.Current.LaunchPage == StartupPage.Tags
            ? TagManagerViewModel
            : NoteListViewModel;
        if (_settingsService.Current.LaunchPage == StartupPage.Settings)
            IsSettingsOpen = true;
    }

    [RelayCommand]
    private void NavigateToNotes() => MiddlePaneContent = NoteListViewModel;

    [RelayCommand]
    private void NavigateToTags()
    {
        MiddlePaneContent = TagManagerViewModel;
        TagManagerViewModel.LoadTagsCommand.Execute(null);
    }

    [RelayCommand]
    private void OpenSettings() => IsSettingsOpen = true;

    private HelpWindow? _helpWindow;

    // F1 and the rail button. One instance: asking again brings it back to the front.
    [RelayCommand]
    private void ShowHelp()
    {
        if (_helpWindow is { IsLoaded: true })
        {
            _helpWindow.Activate();
            return;
        }

        _helpWindow = new HelpWindow { Owner = Application.Current.MainWindow };
        _helpWindow.Closed += (_, _) => _helpWindow = null;
        _helpWindow.Show();
    }

    private bool CanActOnShell => !IsSettingsOpen;
    private bool HasOpenEditor => !IsSettingsOpen && CurrentEditor is NoteEditorViewModel;
    private bool CanDismiss => IsSettingsOpen || CurrentEditor is NoteEditorViewModel;

    [RelayCommand(CanExecute = nameof(CanActOnShell))]
    private async Task CreateNote()
    {
        if (!await ConfirmLeaveEditorAsync())
            return;

        await OpenEditorAsync(note: null, password: null);
    }

    // Ctrl+S. Save() syncs the rich text itself, so it is safe mid-typing.
    [RelayCommand(CanExecute = nameof(HasOpenEditor))]
    private async Task SaveCurrentEditor()
    {
        if (CurrentEditor is NoteEditorViewModel editor)
            await editor.SaveCommand.ExecuteAsync(null);
    }

    // Only a stored note can go to the trash; a new, unsaved one is simply cancelled.
    private bool CanDeleteOpenNote => !IsSettingsOpen && CurrentEditor is NoteEditorViewModel { EditedNote: not null };

    // The editor's trash button. Routed through the list so it gets the same UNDO and
    // the same editor close (NoteDeleted). A note the current filters hide is not in
    // the list, so its summary is built from the note itself: DeleteNote only needs
    // the id and the title.
    [RelayCommand(CanExecute = nameof(CanDeleteOpenNote))]
    private async Task DeleteOpenNote()
    {
        if (CurrentEditor is not NoteEditorViewModel { EditedNote: { } note })
            return;

        var summary = NoteListViewModel.Notes.FirstOrDefault(n => n.Id == note.Id)
            ?? new NoteSummary(note.Id, note.Title, string.Empty, note.Tags, note.IsEncrypted,
                note.HasText, note.HasFiles, note.HasLinks, note.HasChecklists, note.CreatedAt, note.UpdatedAt);

        await NoteListViewModel.DeleteNoteCommand.ExecuteAsync(summary);
    }

    // Escape closes whatever is on top: the Settings dialog, then focus mode, else the editor.
    [RelayCommand(CanExecute = nameof(CanDismiss))]
    private async Task Dismiss()
    {
        if (IsSettingsOpen)
        {
            SettingsViewModel.CloseCommand.Execute(null);
            return;
        }

        if (IsFocusMode)
        {
            IsFocusMode = false;
            return;
        }

        await CloseEditorAsync();
    }

    // Ctrl+wheel, Ctrl+plus / Ctrl+minus: 10 % a step, between 50 % and 250 %. Ctrl+0: 100 %.
    public const double ZoomStep = 0.1;

    [RelayCommand]
    private void ZoomEditor(int steps) =>
        SetEditorZoom(Math.Round(EditorZoom + steps * ZoomStep, 1));

    [RelayCommand]
    private void ResetEditorZoom() => SetEditorZoom(1.0);

    private void SetEditorZoom(double zoom)
    {
        zoom = Math.Clamp(zoom, AppSettings.MinEditorZoom, AppSettings.MaxEditorZoom);
        if (zoom == EditorZoom)
            return;

        EditorZoom = zoom;
        _settingsService.Save(_settingsService.Current with { EditorZoom = zoom });
        MessageQueue.Enqueue($"Zoom {zoom:P0}");
    }

    // F11 and the editor header button. Only with a note open: focus on nothing is an empty window.
    [RelayCommand(CanExecute = nameof(HasOpenEditor))]
    private void ToggleFocusMode() => IsFocusMode = !IsFocusMode;

    // The editor went away (closed, deleted, locked): so does focus mode.
    partial void OnCurrentEditorChanged(ObservableObject? value)
    {
        if (value is null)
            IsFocusMode = false;
    }

    // The list only carries summaries: the full note (blocks included) is
    // loaded here, decrypting on the way when it is password-protected.
    private async void OnEditNoteRequested(NoteSummary summary)
    {
        if (_suppressEditRequest)
            return;

        if (!await ConfirmLeaveEditorAsync())
        {
            RestoreListSelectionToOpenNote();
            return;
        }

        string? password = null;

        if (summary.IsEncrypted)
        {
            password = AskPassword();
            if (password is null)
            {
                NoteListViewModel.SelectedNote = null;
                return;
            }
        }

        var noteResult = password is null
            ? await _noteService.GetNoteByIdAsync(summary.Id)
            : await _noteService.UnlockNoteAsync(summary.Id, password);

        if (!noteResult.TryGet(out var note, out var error))
        {
            MessageQueue.Enqueue(error.Message);
            NoteListViewModel.SelectedNote = null;
            return;
        }

        await OpenEditorAsync(note, password, NoteListViewModel.SearchText);
    }

    private static string? AskPassword()
    {
        var dialog = new PasswordDialog(isSetMode: false) { Owner = Application.Current.MainWindow };
        return dialog.ShowDialog() == true ? dialog.Password : null;
    }

    // A link to another note, a "Linked from" chip: the note is opened by its id, through the
    // same leave guard and password prompt as a click in the list, which then shows it selected.
    public async Task OpenNoteByIdAsync(NoteId id)
    {
        if (CurrentEditor is NoteEditorViewModel { EditedNoteId: { } open } && open == id)
            return;

        if (!await ConfirmLeaveEditorAsync())
            return;

        if (!(await _noteService.GetNoteByIdAsync(id)).TryGet(out var note, out var error))
        {
            MessageQueue.Enqueue(error.Code == "NOT_FOUND" ? "That note no longer exists (it may be in the trash)." : error.Message);
            return;
        }

        string? password = null;
        if (note.IsEncrypted)
        {
            password = AskPassword();
            if (password is null)
                return;

            if (!(await _noteService.UnlockNoteAsync(id, password)).TryGet(out note, out error))
            {
                MessageQueue.Enqueue(error.Message);
                return;
            }
        }

        await OpenEditorAsync(note, password);
        RestoreListSelectionToOpenNote();
    }

    private void OnCreateNoteRequested() => CreateNoteCommand.Execute(null);

    private async void OnNoteSaved(Note note, string? password)
    {
        if (CurrentEditor is NoteEditorViewModel saved)
            DropDraft(saved);

        MessageQueue.Enqueue($"Note '{note.Title}' saved successfully.");
        NoteListViewModel.LoadNotesCommand.Execute(null);

        if (CurrentEditor is NoteEditorViewModel existingEditor)
            existingEditor.RefreshAfterSave(note);
        else
            await OpenEditorAsync(note, password);

        // A just-created note now has a stored identity: its trash button wakes up,
        // and if it was saved encrypted the idle lock starts watching it.
        DeleteOpenNoteCommand.NotifyCanExecuteChanged();
        RearmEncryptedNoteLock();
    }

    private async Task OpenEditorAsync(Note? note, string? password, string? searchTerm = null)
    {
        var tags = await _tagRepository.GetAllAsync();
        var allTags = tags.Match(t => t, _ => (IReadOnlyList<Tag>)[]);

        var editor = new NoteEditorViewModel(_noteService, _tagRepository, allTags, note, password)
        {
            SearchTerm = string.IsNullOrWhiteSpace(searchTerm) ? null : searchTerm.Trim()
        };
        editor.SaveCompleted += OnNoteSaved;
        editor.CancelRequested += OnEditorCancelled;
        editor.OpenNoteRequested += id => _ = OpenNoteByIdAsync(id);
        CurrentEditor = editor;
        RearmEncryptedNoteLock();

        if (note is not null)
            await editor.LoadLinkedFromAsync();
    }

    private async void OnEditorCancelled() => await CloseEditorAsync();

    // Returning to empty-state leaves the note list intact in the middle pane.
    // Also clear SelectedNote so the user can re-select the same note immediately.
    private async Task CloseEditorAsync()
    {
        if (!await ConfirmLeaveEditorAsync())
            return;

        CurrentEditor = null;
        NoteListViewModel.SelectedNote = null;
        RearmEncryptedNoteLock();
    }

    // The note is gone: drop its editor without asking anything, there is
    // nothing left to save it into.
    private void OnNoteDeleted(NoteSummary deleted)
    {
        if (CurrentEditor is NoteEditorViewModel editor && editor.EditedNoteId == deleted.Id)
        {
            DropDraft(editor);
            CurrentEditor = null;
            RearmEncryptedNoteLock();
        }
    }

    // Closing Settings is when a changed lock delay reaches the open note.
    partial void OnIsSettingsOpenChanged(bool value)
    {
        if (!value)
            RearmEncryptedNoteLock();
    }

    // --- Unsaved changes guard ---

    // Set while the list selection is being put back: assigning SelectedNote raises
    // EditNoteRequested again, which would re-open the dialog for ever.
    private bool _suppressEditRequest;

    // Lets the window skip the guard entirely when there is nothing to ask about,
    // which is the ordinary way to quit.
    public bool HasUnsavedChanges => CurrentEditor is NoteEditorViewModel { IsDirty: true };

    // Every exit from the editor goes through here — opening another note, creating
    // one, cancelling, closing the window. true = it is safe to go on, false = the
    // user chose to stay in the editor.
    public async Task<bool> ConfirmLeaveEditorAsync()
    {
        if (CurrentEditor is not NoteEditorViewModel editor || !editor.IsDirty)
            return true;

        var name = string.IsNullOrWhiteSpace(editor.Title) ? "This note" : $"'{editor.Title}'";
        var answer = MessageBox.Show(
            Application.Current.MainWindow,
            $"{name} has unsaved changes.\n\nSave them before leaving?",
            "Unsaved changes",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Warning);

        switch (EditorLeave.Choose(answer))
        {
            case LeaveChoice.Discard:
                DropDraft(editor);
                return true;

            case LeaveChoice.Stay:
                return false;

            default:
                await editor.SaveCommand.ExecuteAsync(null);

                // A save that failed validation — no block, invalid link, missing
                // password — leaves the note dirty with its ErrorMessage on screen.
                // Staying in the editor is then the only sane outcome.
                return !editor.IsDirty;
        }
    }

    private void RestoreListSelectionToOpenNote()
    {
        var openId = (CurrentEditor as NoteEditorViewModel)?.EditedNoteId;

        _suppressEditRequest = true;
        try
        {
            NoteListViewModel.SelectedNote = openId is null
                ? null
                : NoteListViewModel.Notes.FirstOrDefault(n => n.Id == openId.Value);
        }
        finally
        {
            _suppressEditRequest = false;
        }
    }

    // --- Automatic lock of the open encrypted note ---

    private readonly IdleLock _encryptedNoteLock = new();
    private readonly DispatcherTimer _lockTimer;

    // MainWindow forwards every keystroke, click and wheel turn here.
    public void NotifyActivity() => _encryptedNoteLock.NotifyActivity(DateTime.UtcNow);

    // Called whenever what the editor holds changes, and after Settings closes: the
    // delay is re-read from the settings each time, so a change applies at once.
    private void RearmEncryptedNoteLock()
    {
        _encryptedNoteLock.Timeout = TimeSpan.FromMinutes(_settingsService.Current.LockEncryptedNotesAfterMinutes);

        // Only a stored encrypted note has decrypted content on screen to protect. A
        // brand-new note whose encryption box was just ticked is still being written.
        if (_encryptedNoteLock.IsEnabled && CurrentEditor is NoteEditorViewModel { EditedNote.IsEncrypted: true })
        {
            _encryptedNoteLock.Arm(DateTime.UtcNow);
            _lockTimer.Start();
        }
        else
        {
            _encryptedNoteLock.Disarm();
            _lockTimer.Stop();
        }
    }

    private async void OnLockTimerTick(object? sender, EventArgs e)
    {
        if (_encryptedNoteLock.HasExpired(DateTime.UtcNow))
            await LockEncryptedNoteAsync($"after {_encryptedNoteLock.Timeout.TotalMinutes:0} minute(s) of inactivity");
    }

    // Windows locked (Win+L), the session disconnected, the computer going to sleep: nobody
    // is in front of the screen any more, so the open encrypted note is locked now rather
    // than when the idle delay runs out. App.xaml.cs wires SystemEvents to this.
    public async Task LockForAbsenceAsync(string why)
    {
        if (_settingsService.Current.LockEncryptedNotesWhenWindowsLocks)
            await LockEncryptedNoteAsync(why);
    }

    private async Task LockEncryptedNoteAsync(string why)
    {
        if (CurrentEditor is not NoteEditorViewModel { EditedNote.IsEncrypted: true } editor)
        {
            RearmEncryptedNoteLock();
            return;
        }

        var title = editor.Title;

        // The password is still in memory, so a modified note is saved — still
        // encrypted — instead of being either discarded or left decrypted on screen.
        if (editor.IsDirty)
        {
            await editor.SaveCommand.ExecuteAsync(null);

            if (editor.IsDirty)
            {
                // Validation failed (no block, invalid link). Keep the editor and its
                // error message, and stop re-trying every fifteen seconds.
                _encryptedNoteLock.NotifyActivity(DateTime.UtcNow);
                MessageQueue.Enqueue($"'{title}' could not be locked: {editor.ErrorMessage}");
                return;
            }
        }

        CurrentEditor = null;
        NoteListViewModel.SelectedNote = null;
        RearmEncryptedNoteLock();
        MessageQueue.Enqueue($"'{title}' locked {why}.");
    }

    // --- Drafts of unsaved changes (DraftStore) ---

    private readonly DispatcherTimer _draftTimer;

    // Every 20 s while the open note has unsaved changes. An encrypted one is never written
    // out, and the draft it may have had before encryption was ticked goes away.
    private void KeepDraft()
    {
        if (CurrentEditor is not NoteEditorViewModel { IsDirty: true } editor)
            return;

        try
        {
            if (editor.CanKeepDraft)
                _drafts.Save(editor.CaptureDraft());
            else
                _drafts.Delete(editor.DraftKey);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A draft is a safety net, not a feature to fail loudly on every 20 s.
        }
    }

    // Saved or discarded: nothing left to recover. Both keys, because a new note changes
    // key the moment it is first saved.
    private void DropDraft(NoteEditorViewModel editor)
    {
        _drafts.Delete(editor.NewNoteDraftKey);
        if (editor.EditedNoteId is { } id)
            _drafts.Delete(id.Value);
    }

    private async void OfferDraftsLeftBehind()
    {
        foreach (var draft in _drafts.LoadAll())
        {
            var name = string.IsNullOrWhiteSpace(draft.Title) ? "an untitled note" : $"'{draft.Title}'";
            var answer = MessageBox.Show(
                Application.Current.MainWindow!,
                $"NoteApp closed without saving the changes to {name} (kept at {draft.SavedAt:g}).\n\n" +
                "Restore them now? No deletes them.",
                "Unsaved changes found",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (answer != MessageBoxResult.Yes)
            {
                _drafts.Delete(draft.Key);
                continue;
            }

            await RestoreDraftAsync(draft);
            return; // one editor at a time: any other draft is offered at the next start
        }
    }

    // Into the note it belonged to, or into a new note when that one has gone (purged, or
    // encrypted since — its plain text is not written back over it).
    private async Task RestoreDraftAsync(NoteDraft draft)
    {
        Note? note = null;
        if (draft.NoteId is { } id && (await _noteService.GetNoteByIdAsync(new NoteId(id))).TryGet(out var stored, out _)
            && !stored.IsEncrypted)
            note = stored;

        if (note is null && draft.NoteId is not null)
        {
            _drafts.Delete(draft.Key);
            draft = draft with { Key = Guid.NewGuid(), NoteId = null };
        }

        await OpenEditorAsync(note, password: null);
        if (CurrentEditor is NoteEditorViewModel editor)
        {
            editor.RestoreDraft(draft);
            MessageQueue.Enqueue("Unsaved changes restored — save to keep them.");
        }
    }

    // --- Automatic backup ---

    private readonly DispatcherTimer _backupTimer;
    private bool _noPasswordReported;

    private async void OnBackupTimerTick(object? sender, EventArgs e)
    {
        _backupTimer.Interval = TimeSpan.FromHours(1);

        switch (await _autoBackup.RunIfDueAsync(DateTime.Now))
        {
            case AutoBackupOutcome.Saved { ZipPath: var zip, Pruned: var pruned }:
                MessageQueue.Enqueue(pruned > 0
                    ? $"Automatic backup saved to {zip} ({pruned} old backup(s) removed)."
                    : $"Automatic backup saved to {zip}.");
                break;

            case AutoBackupOutcome.Failed { Error: var error }:
                MessageQueue.Enqueue($"Automatic backup failed: {error.Message}");
                break;

            // Once per session: it stays true until someone types a password in Settings.
            case AutoBackupOutcome.NoPassword when !_noPasswordReported:
                _noPasswordReported = true;
                MessageQueue.Enqueue("Automatic backup is on, but no backup password is set (Settings).");
                break;
        }
    }

    private void OnShowMessage(string message) => MessageQueue.Enqueue(message);

    // Longer than the 3 s default: this one carries the only way back from a delete.
    private void OnShowUndoableMessage(string message, Action undo) =>
        MessageQueue.Enqueue(message, "UNDO", _ => undo(), (object?)null,
            promote: false, neverConsiderToBeDuplicate: true, durationOverride: TimeSpan.FromSeconds(6));
}
