using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaterialDesignThemes.Wpf;
using NoteApp.Data.Repositories;
using NoteApp.Domain.Functional;
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

    [ObservableProperty] private ObservableObject? _currentView;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isDrawerOpen;

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
        settingsViewModel.CloseRequested += () => CurrentView = NoteListViewModel;

        CurrentView = _settingsService.Current.LaunchPage switch
        {
            StartupPage.Tags => TagManagerViewModel,
            StartupPage.Settings => SettingsViewModel,
            _ => NoteListViewModel
        };
    }

    [RelayCommand]
    private void NavigateToNotes()
    {
        CurrentView = NoteListViewModel;
        IsDrawerOpen = false;
    }

    [RelayCommand]
    private void NavigateToTags()
    {
        CurrentView = TagManagerViewModel;
        IsDrawerOpen = false;
        TagManagerViewModel.LoadTagsCommand.Execute(null);
    }

    [RelayCommand]
    private void NavigateToSettings()
    {
        CurrentView = SettingsViewModel;
        IsDrawerOpen = false;
    }

    [RelayCommand]
    private async Task CreateNote()
    {
        IsDrawerOpen = false;
        var tags = await _tagRepository.GetAllAsync();
        var allTags = tags.Match(t => t, _ => (IReadOnlyList<Tag>)[]);

        var editor = new NoteEditorViewModel(_noteService, _tagRepository, allTags);
        editor.SaveCompleted += OnNoteSaved;
        editor.CancelRequested += OnEditorCancelled;
        CurrentView = editor;
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
            if (dialog.ShowDialog() != true) return;
            password = dialog.Password;

            var decryptResult = await _noteService.UnlockNoteAsync(note.Id, password);
            if (decryptResult.IsFailure)
            {
                var error = ((Result<Note, AppError>.Failure)decryptResult).Error;
                MessageQueue.Enqueue(error.Message);
                return;
            }
            note = ((Result<Note, AppError>.Success)decryptResult).Value;
        }

        var tags = await _tagRepository.GetAllAsync();
        var allTags = tags.Match(t => t, _ => (IReadOnlyList<Tag>)[]);

        var editor = new NoteEditorViewModel(_noteService, _tagRepository, allTags, note, password);
        editor.SaveCompleted += OnNoteSaved;
        editor.CancelRequested += OnEditorCancelled;
        CurrentView = editor;
    }

    private void OnCreateNoteRequested() => CreateNoteCommand.Execute(null);

    private async void OnNoteSaved(Note note, string? password)
    {
        MessageQueue.Enqueue($"Note '{note.Title}' saved successfully.");
        NoteListViewModel.LoadNotesCommand.Execute(null);

        if (CurrentView is NoteEditorViewModel existingEditor)
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
            CurrentView = editor;
        }
    }

    private void OnEditorCancelled()
    {
        CurrentView = NoteListViewModel;
    }

    private void OnShowMessage(string message)
    {
        MessageQueue.Enqueue(message);
    }
}
