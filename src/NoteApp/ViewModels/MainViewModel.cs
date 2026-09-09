using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaterialDesignThemes.Wpf;
using NoteApp.Data.Repositories;
using NoteApp.Domain.Models;
using NoteApp.Services;
using NoteApp.Views;

namespace NoteApp.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly NoteService _noteService;
    private readonly ITagRepository _tagRepository;
    private readonly AppSettingsService _settingsService;

    // Middle pane: NoteList <-> TagManager
    [ObservableProperty] private ObservableObject? _middlePaneContent;
    // Right pane: a NoteEditorViewModel, or null => empty-state placeholder
    [ObservableProperty] private ObservableObject? _currentEditor;
    // Settings modal (hosted in RootDialog)
    [ObservableProperty] private bool _isSettingsOpen;

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
        SettingsViewModel settingsViewModel)
    {
        _noteService = noteService;
        _settingsService = settingsService;
        _tagRepository = tagRepository;
        NoteListViewModel = noteListViewModel;
        TagManagerViewModel = tagManagerViewModel;
        SettingsViewModel = settingsViewModel;

        noteListViewModel.EditNoteRequested += OnEditNoteRequested;
        noteListViewModel.CreateNoteRequested += OnCreateNoteRequested;
        noteListViewModel.ShowMessage += OnShowMessage;
        noteListViewModel.NoteDeleted += OnNoteDeleted;

        tagManagerViewModel.ShowMessage += OnShowMessage;
        settingsViewModel.ShowMessage += OnShowMessage;
        settingsViewModel.CloseRequested += () => IsSettingsOpen = false;

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

    [RelayCommand]
    private async Task CreateNote()
    {
        if (!await ConfirmLeaveEditorAsync())
            return;

        await OpenEditorAsync(note: null, password: null);
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
            var dialog = new PasswordDialog(isSetMode: false)
            {
                Owner = Application.Current.MainWindow
            };
            if (dialog.ShowDialog() != true)
            {
                NoteListViewModel.SelectedNote = null;
                return;
            }
            password = dialog.Password;
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

        await OpenEditorAsync(note, password);
    }

    private void OnCreateNoteRequested() => CreateNoteCommand.Execute(null);

    private async void OnNoteSaved(Note note, string? password)
    {
        MessageQueue.Enqueue($"Note '{note.Title}' saved successfully.");
        NoteListViewModel.LoadNotesCommand.Execute(null);

        if (CurrentEditor is NoteEditorViewModel existingEditor)
            existingEditor.RefreshAfterSave(note);
        else
            await OpenEditorAsync(note, password);
    }

    private async Task OpenEditorAsync(Note? note, string? password)
    {
        var tags = await _tagRepository.GetAllAsync();
        var allTags = tags.Match(t => t, _ => (IReadOnlyList<Tag>)[]);

        var editor = new NoteEditorViewModel(_noteService, _tagRepository, allTags, note, password);
        editor.SaveCompleted += OnNoteSaved;
        editor.CancelRequested += OnEditorCancelled;
        CurrentEditor = editor;
    }

    // Returning to empty-state leaves the note list intact in the middle pane.
    // Also clear SelectedNote so the user can re-select the same note immediately.
    private async void OnEditorCancelled()
    {
        if (!await ConfirmLeaveEditorAsync())
            return;

        CurrentEditor = null;
        NoteListViewModel.SelectedNote = null;
    }

    // The note is gone: drop its editor without asking anything, there is
    // nothing left to save it into.
    private void OnNoteDeleted(NoteSummary deleted)
    {
        if (CurrentEditor is NoteEditorViewModel editor && editor.EditedNoteId == deleted.Id)
            CurrentEditor = null;
    }

    // --- Unsaved changes guard ---

    // Set while the list selection is being put back: assigning SelectedNote raises
    // EditNoteRequested again, which would re-open the dialog for ever.
    private bool _suppressEditRequest;

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

        if (answer == MessageBoxResult.Cancel)
            return false;

        if (answer == MessageBoxResult.No)
            return true;

        await editor.SaveCommand.ExecuteAsync(null);

        // A save that failed validation — no block, invalid link, missing password —
        // leaves the note dirty with its ErrorMessage on screen. Staying in the editor
        // is then the only sane outcome.
        return !editor.IsDirty;
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

    private void OnShowMessage(string message) => MessageQueue.Enqueue(message);
}
