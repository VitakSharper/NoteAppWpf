using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaterialDesignThemes.Wpf;
using NoteApp.Data.Repositories;
using NoteApp.Domain.Functional;
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
        var tags = await _tagRepository.GetAllAsync();
        var allTags = tags.Match(t => t, _ => (IReadOnlyList<Tag>)[]);

        var editor = new NoteEditorViewModel(_noteService, _tagRepository, allTags);
        editor.SaveCompleted += OnNoteSaved;
        editor.CancelRequested += OnEditorCancelled;
        CurrentEditor = editor;
    }

    private async void OnEditNoteRequested(Note note)
    {
        string? password = null;

        if (note.IsEncrypted)
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

            var decryptResult = await _noteService.UnlockNoteAsync(note.Id, password);
            if (decryptResult.IsFailure)
            {
                var error = ((Result<Note, AppError>.Failure)decryptResult).Error;
                MessageQueue.Enqueue(error.Message);
                NoteListViewModel.SelectedNote = null;
                return;
            }
            note = ((Result<Note, AppError>.Success)decryptResult).Value;
        }

        var tags = await _tagRepository.GetAllAsync();
        var allTags = tags.Match(t => t, _ => (IReadOnlyList<Tag>)[]);

        var editor = new NoteEditorViewModel(_noteService, _tagRepository, allTags, note, password);
        editor.SaveCompleted += OnNoteSaved;
        editor.CancelRequested += OnEditorCancelled;
        CurrentEditor = editor;
    }

    private void OnCreateNoteRequested() => CreateNoteCommand.Execute(null);

    private async void OnNoteSaved(Note note, string? password)
    {
        MessageQueue.Enqueue($"Note '{note.Title}' saved successfully.");
        NoteListViewModel.LoadNotesCommand.Execute(null);

        if (CurrentEditor is NoteEditorViewModel existingEditor)
        {
            existingEditor.RefreshAfterSave(note);
        }
        else
        {
            var tags = await _tagRepository.GetAllAsync();
            var allTags = tags.Match(t => t, _ => (IReadOnlyList<Tag>)[]);

            var editor = new NoteEditorViewModel(_noteService, _tagRepository, allTags, note, password);
            editor.SaveCompleted += OnNoteSaved;
            editor.CancelRequested += OnEditorCancelled;
            CurrentEditor = editor;
        }
    }

    // Returning to empty-state leaves the note list intact in the middle pane.
    // Also clear SelectedNote so the user can re-select the same note immediately.
    private void OnEditorCancelled()
    {
        CurrentEditor = null;
        NoteListViewModel.SelectedNote = null;
    }

    private void OnShowMessage(string message) => MessageQueue.Enqueue(message);
}
