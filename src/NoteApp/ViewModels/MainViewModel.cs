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
    [NotifyCanExecuteChangedFor(nameof(SaveCurrentEditorCommand), nameof(DismissCommand), nameof(DeleteOpenNoteCommand), nameof(DuplicateOpenNoteCommand), nameof(ToggleFocusModeCommand))]
    private ObservableObject? _currentEditor;
    // Rail and note list hidden, the editor gets the whole window (MainWindow collapses the columns).
    [ObservableProperty] private bool _isFocusMode;
    // Scale of the editor's blocks; kept in the settings.
    [ObservableProperty] private double _editorZoom = 1.0;
    // Settings modal (hosted in RootDialog). The keyboard shortcuts stay inert while
    // it is open: Ctrl+N would otherwise create a note underneath the overlay.
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateNoteCommand), nameof(SaveCurrentEditorCommand), nameof(DismissCommand), nameof(DeleteOpenNoteCommand), nameof(DuplicateOpenNoteCommand), nameof(NewFromTemplateCommand), nameof(ToggleFocusModeCommand), nameof(GoBackCommand), nameof(GoForwardCommand))]
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
        noteListViewModel.NoteDuplicated += id => _ = OpenNoteByIdAsync(id);

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

        await NoteListViewModel.DeleteNoteCommand.ExecuteAsync(SummaryOf(note));
    }

    // The editor's ⋮ menu. The copy is made from what is stored, so unsaved changes are saved
    // first; a save that fails validation stops it there.
    [RelayCommand(CanExecute = nameof(CanDeleteOpenNote))]
    private async Task DuplicateOpenNote()
    {
        if (CurrentEditor is not NoteEditorViewModel editor)
            return;

        if (editor.IsDirty)
        {
            await editor.SaveCommand.ExecuteAsync(null);
            if (editor.IsDirty)
                return;
        }

        if (editor.EditedNote is not { } note)
            return;

        await NoteListViewModel.DuplicateNoteCommand.ExecuteAsync(SummaryOf(note));
    }

    // A note the current filters hide is not in the list, so its summary is built from the
    // note itself: the list commands only need the id and the title.
    private NoteSummary SummaryOf(Note note) =>
        NoteListViewModel.Notes.FirstOrDefault(n => n.Id == note.Id)
        ?? new NoteSummary(note.Id, note.Title, string.Empty, note.Tags, note.IsEncrypted,
            note.HasText, note.HasFiles, note.HasLinks, note.HasChecklists, note.CreatedAt, note.UpdatedAt);

    // --- Templates ---

    public System.Collections.ObjectModel.ObservableCollection<NoteRef> Templates { get; } = [];

    // Refreshed each time the rail's template menu opens.
    public async Task LoadTemplatesAsync()
    {
        if (!(await _noteService.TemplatesAsync()).TryGet(out var templates, out var error))
        {
            MessageQueue.Enqueue(error.Message);
            return;
        }

        Templates.Clear();
        foreach (var template in templates)
            Templates.Add(template);
    }

    [RelayCommand(CanExecute = nameof(CanActOnShell))]
    private async Task NewFromTemplate(NoteRef template)
    {
        if (!await ConfirmLeaveEditorAsync())
            return;

        if (!(await _noteService.GetTemplateAsync(template.Id)).TryGet(out var note, out var error))
        {
            MessageQueue.Enqueue(error.Message);
            return;
        }

        await OpenEditorAsync(note: null, password: null);
        if (CurrentEditor is NoteEditorViewModel editor)
            editor.FillFrom(note);
    }

    [RelayCommand]
    private async Task DeleteTemplate(NoteRef template)
    {
        var answer = MessageBox.Show(Application.Current.MainWindow!, $"Delete the template '{template.Title}'? Notes made from it stay as they are.",
            "Delete template", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes)
            return;

        (await _noteService.DeleteTemplateAsync(template.Id)).Match(
            success: _ => MessageQueue.Enqueue($"Template '{template.Title}' deleted."),
            failure: error => MessageQueue.Enqueue(error.Message));
        await LoadTemplatesAsync();
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
        NotifyHistoryChanged();
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

        // The first word or phrase searched for, not the operators around it.
        await OpenEditorAsync(note, password, NoteListViewModel.HighlightTerms.FirstOrDefault());
    }

    private static string? AskPassword()
    {
        var dialog = new PasswordDialog(isSetMode: false) { Owner = Application.Current.MainWindow };
        return dialog.ShowDialog() == true ? dialog.Password : null;
    }

    // A link to another note, a "Linked from" chip: the note is opened by its id, through the
    // same leave guard and password prompt as a click in the list, which then shows it selected.
    // true when the note is open (already, or now).
    public async Task<bool> OpenNoteByIdAsync(NoteId id)
    {
        if (CurrentEditor is NoteEditorViewModel { EditedNoteId: { } open } && open == id)
            return true;

        if (!await ConfirmLeaveEditorAsync())
            return false;

        if (!(await _noteService.GetNoteByIdAsync(id)).TryGet(out var note, out var error))
        {
            MessageQueue.Enqueue(error.Code == "NOT_FOUND" ? "That note no longer exists (it may be in the trash)." : error.Message);
            return false;
        }

        string? password = null;
        if (note.IsEncrypted)
        {
            password = AskPassword();
            if (password is null)
                return false;

            if (!(await _noteService.UnlockNoteAsync(id, password)).TryGet(out note, out error))
            {
                MessageQueue.Enqueue(error.Message);
                return false;
            }
        }

        await OpenEditorAsync(note, password);
        RestoreListSelectionToOpenNote();
        return true;
    }

    // --- Back / Forward (Alt+Left / Alt+Right, the mouse's side buttons) ---

    private readonly NavigationHistory _history = new();

    // With no note on screen (closed, deleted), Back first brings back the one the history is on.
    private bool IsShowingCurrent => CurrentEditor is NoteEditorViewModel { EditedNoteId: { } open } && open == _history.Current;
    private bool CanGoBack => !IsSettingsOpen && (_history.CanGoBack || _history.Current is not null && !IsShowingCurrent);
    private bool CanGoForward => !IsSettingsOpen && _history.CanGoForward;

    [RelayCommand(CanExecute = nameof(CanGoBack))]
    private async Task GoBack()
    {
        if (!IsShowingCurrent && _history.Current is { } current)
        {
            await OpenNoteByIdAsync(current);
        }
        else if (_history.Back() is { } previous && !await OpenNoteByIdAsync(previous))
        {
            _history.Forward(); // stayed where it was
        }

        NotifyHistoryChanged();
    }

    [RelayCommand(CanExecute = nameof(CanGoForward))]
    private async Task GoForward()
    {
        if (_history.Forward() is { } next && !await OpenNoteByIdAsync(next))
            _history.Back();

        NotifyHistoryChanged();
    }

    private void NotifyHistoryChanged()
    {
        GoBackCommand.NotifyCanExecuteChanged();
        GoForwardCommand.NotifyCanExecuteChanged();
    }

    // --- Ctrl+K ---

    // Recent notes first (the list's order), then what the shell can do.
    public async Task<IReadOnlyList<QuickSwitchEntry>> QuickSwitchEntriesAsync()
    {
        var entries = new List<QuickSwitchEntry>();
        if ((await _noteService.SearchAsync(new NoteQuery(NoteShelf.AllLive))).TryGet(out var notes, out var error))
            entries.AddRange(notes.OrderByDescending(n => n.UpdatedAt).Select(n => new NoteEntry(n.Id, n.Title.Value, n.IsEncrypted, n.IsArchived)));
        else
            MessageQueue.Enqueue(error.Message);

        entries.Add(new CommandEntry("New note", () => CreateNoteCommand.ExecuteAsync(null)));
        await LoadTemplatesAsync();
        entries.AddRange(Templates.Select(t => new CommandEntry($"New from template: {t.Title}", () => NewFromTemplateCommand.ExecuteAsync(t))));
        entries.Add(new CommandEntry("Back", () => GoBackCommand.ExecuteAsync(null)));
        entries.Add(new CommandEntry("Forward", () => GoForwardCommand.ExecuteAsync(null)));
        entries.Add(new CommandEntry("Show the notes", () => ShowShelf(archive: false, trash: false)));
        entries.Add(new CommandEntry("Show the archive", () => ShowShelf(archive: true, trash: false)));
        entries.Add(new CommandEntry("Show the trash", () => ShowShelf(archive: false, trash: true)));
        entries.Add(new CommandEntry("Tags", () => Run(NavigateToTagsCommand)));
        entries.Add(new CommandEntry("Settings", () => Run(OpenSettingsCommand)));
        entries.Add(new CommandEntry("Help", () => Run(ShowHelpCommand)));
        if (CurrentEditor is NoteEditorViewModel)
            entries.Add(new CommandEntry("Focus mode", () => Run(ToggleFocusModeCommand)));
        entries.Add(new CommandEntry(SettingsViewModel.IsDarkMode ? "Light theme" : "Dark theme", () =>
        {
            SettingsViewModel.IsDarkMode = !SettingsViewModel.IsDarkMode;
            SettingsViewModel.SaveCommand.Execute(null);
            return Task.CompletedTask;
        }));
        return entries;
    }

    public Task RunQuickSwitchAsync(QuickSwitchEntry entry) => entry switch
    {
        NoteEntry note => OpenNoteByIdAsync(note.Id),
        CommandEntry command => command.Run(),
        _ => Task.CompletedTask
    };

    private Task ShowShelf(bool archive, bool trash)
    {
        MiddlePaneContent = NoteListViewModel;
        NoteListViewModel.IsArchiveView = archive;
        NoteListViewModel.IsTrashView = trash;
        return Task.CompletedTask;
    }

    private static Task Run(System.Windows.Input.ICommand command)
    {
        if (command.CanExecute(null))
            command.Execute(null);
        return Task.CompletedTask;
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

        // A note saved for the first time joins the history.
        _history.Visit(note.Id);
        NotifyHistoryChanged();

        // A just-created note now has a stored identity: its trash button wakes up,
        // and if it was saved encrypted the idle lock starts watching it.
        DeleteOpenNoteCommand.NotifyCanExecuteChanged();
        DuplicateOpenNoteCommand.NotifyCanExecuteChanged();
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
        editor.ShowMessage += OnShowMessage;
        CurrentEditor = editor;
        RearmEncryptedNoteLock();

        if (note is not null)
            _history.Visit(note.Id);
        NotifyHistoryChanged();

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
        _history.Forget(deleted.Id);
        NotifyHistoryChanged();

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

    // The window going to the notification area: whatever the settings, no decrypted note stays
    // open behind an icon.
    public Task LockEncryptedNoteNowAsync(string why) => LockEncryptedNoteAsync(why);

    public bool KeepsRunningInTray => _settingsService.Current.CloseToTray;

    // --- Quick notes (Ctrl+Alt+N, the tray icon) ---

    // One text block, titled by what was typed or by its first line. The error message, or
    // null once it is stored.
    public async Task<string?> SaveQuickNoteAsync(string title, string text)
    {
        var body = text.Trim('\r', '\n');
        var name = QuickNotes.TitleFor(title, body, DateTime.Now);
        var block = new NoteBlock.Text(RichTextPayload.FromPlainText(body), body);

        if (!(await _noteService.CreateNoteAsync(name, [block], [])).TryGet(out var note, out var error))
            return error.Message;

        NoteListViewModel.LoadNotesCommand.Execute(null);
        MessageQueue.Enqueue($"Quick note '{note.Title}' saved.");
        return null;
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
