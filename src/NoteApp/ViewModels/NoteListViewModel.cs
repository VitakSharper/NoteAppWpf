using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NoteApp.Data.Repositories;
using NoteApp.Domain.Models;
using NoteApp.Domain.ValueObjects;
using NoteApp.Services;

namespace NoteApp.ViewModels;

public sealed record SortOption(string Label, string Field, ListSortDirection Direction)
{
    public override string ToString() => Label;

    public static readonly SortOption UpdatedDesc = new("Updated (newest)", nameof(NoteSummary.UpdatedAt), ListSortDirection.Descending);
    public static readonly SortOption UpdatedAsc = new("Updated (oldest)", nameof(NoteSummary.UpdatedAt), ListSortDirection.Ascending);
    public static readonly SortOption TitleAsc = new("Title (A–Z)", "Title.Value", ListSortDirection.Ascending);
    public static readonly SortOption TitleDesc = new("Title (Z–A)", "Title.Value", ListSortDirection.Descending);
    public static readonly SortOption CreatedDesc = new("Created (newest)", nameof(NoteSummary.CreatedAt), ListSortDirection.Descending);
    public static readonly SortOption CreatedAsc = new("Created (oldest)", nameof(NoteSummary.CreatedAt), ListSortDirection.Ascending);

    public static readonly IReadOnlyList<SortOption> All =
        [UpdatedDesc, UpdatedAsc, TitleAsc, TitleDesc, CreatedDesc, CreatedAsc];
}

public partial class TagFilterItem : ObservableObject
{
    public Tag Tag { get; }
    [ObservableProperty] private bool _isSelected;

    public TagFilterItem(Tag tag) => Tag = tag;
    public string Name => Tag.Name.ToString();
}

// The list works on NoteSummary (no block payloads); the full Note is only
// loaded when a note is opened in the editor.
public partial class NoteListViewModel : ObservableObject
{
    private readonly NoteService _noteService;
    private readonly ITagRepository _tagRepository;
    private readonly AppSettingsService _settingsService;

    [ObservableProperty] private ObservableCollection<NoteSummary> _notes = [];
    [ObservableProperty] private ObservableCollection<TagFilterItem> _allTags = [];
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private NoteTypeFilter _selectedTypeFilter = NoteTypeFilter.All;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private NoteSummary? _selectedNote;
    [ObservableProperty] private SortOption _selectedSort = SortOption.UpdatedDesc;

    public IReadOnlyList<NoteTypeFilter> TypeFilters => NoteTypeFilter.AllFilters;
    public IReadOnlyList<SortOption> SortOptions => SortOption.All;

    // Drives the "clear" button: anything narrowing the list beyond the default view.
    public bool HasActiveFilters =>
        !string.IsNullOrWhiteSpace(SearchText)
        || SelectedTypeFilter != NoteTypeFilter.All
        || AllTags.Any(t => t.IsSelected);

    public event Action<NoteSummary>? EditNoteRequested;
    public event Action? CreateNoteRequested;
    public event Action<string>? ShowMessage;
    // The shell closes the editor when the note it holds has just been deleted.
    public event Action<NoteSummary>? NoteDeleted;

    public NoteListViewModel(
        NoteService noteService,
        ITagRepository tagRepository,
        AppSettingsService settingsService)
    {
        _noteService = noteService;
        _tagRepository = tagRepository;
        _settingsService = settingsService;
    }

    partial void OnSelectedNoteChanged(NoteSummary? value)
    {
        if (value is not null)
            EditNoteRequested?.Invoke(value);
    }

    partial void OnSelectedSortChanged(SortOption value)
    {
        if (Notes.Count > 0)
            ApplySort();
    }

    partial void OnSearchTextChanged(string value) => OnPropertyChanged(nameof(HasActiveFilters));

    // The type chips are a ListBox bound to this property: selecting a chip reloads.
    // A ListBox can push null while its items are being (re)generated — never keep it.
    partial void OnSelectedTypeFilterChanged(NoteTypeFilter value)
    {
        if (value is null)
        {
            SelectedTypeFilter = NoteTypeFilter.All;
            return;
        }

        OnPropertyChanged(nameof(HasActiveFilters));
        LoadNotesCommand.Execute(null);
    }

    [RelayCommand]
    public async Task LoadNotes()
    {
        IsLoading = true;
        try
        {
            var selectedIds = AllTags.Where(t => t.IsSelected).Select(t => t.Tag.Id).ToList();
            var tagIds = selectedIds.Count > 0 ? selectedIds : null;

            var result = await _noteService.SearchAsync(
                string.IsNullOrWhiteSpace(SearchText) ? null : SearchText,
                tagIds,
                SelectedTypeFilter.Value);

            result.Match(
                success: notes => Notes = new ObservableCollection<NoteSummary>(notes),
                failure: error => ShowMessage?.Invoke(error.Message));

            ApplySort();

            var tagsResult = await _tagRepository.GetAllAsync();
            tagsResult.Match(
                success: tags =>
                {
                    var items = tags.Select(t =>
                    {
                        var item = new TagFilterItem(t);
                        item.IsSelected = selectedIds.Contains(t.Id);
                        return item;
                    });
                    AllTags = new ObservableCollection<TagFilterItem>(items);
                },
                failure: _ => { });

            OnPropertyChanged(nameof(HasActiveFilters));
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ApplySort()
    {
        var sorted = SelectedSort.Direction == ListSortDirection.Ascending
            ? SelectedSort.Field switch
            {
                "Title.Value" => Notes.OrderBy(n => n.Title.Value, StringComparer.OrdinalIgnoreCase),
                nameof(NoteSummary.CreatedAt) => Notes.OrderBy(n => n.CreatedAt),
                _ => Notes.OrderBy(n => n.UpdatedAt)
            }
            : SelectedSort.Field switch
            {
                "Title.Value" => Notes.OrderByDescending(n => n.Title.Value, StringComparer.OrdinalIgnoreCase),
                nameof(NoteSummary.CreatedAt) => Notes.OrderByDescending(n => n.CreatedAt),
                _ => Notes.OrderByDescending(n => n.UpdatedAt)
            };

        Notes = new ObservableCollection<NoteSummary>(sorted);
    }

    [RelayCommand]
    private async Task Search() => await LoadNotes();

    [RelayCommand]
    private void ClearSearch()
    {
        SearchText = string.Empty;
        foreach (var t in AllTags) t.IsSelected = false;

        // Changing the type filter reloads by itself; otherwise reload explicitly.
        if (SelectedTypeFilter != NoteTypeFilter.All)
            SelectedTypeFilter = NoteTypeFilter.All;
        else
            LoadNotesCommand.Execute(null);
    }

    [RelayCommand]
    private void ToggleTagFilter(TagFilterItem item)
    {
        item.IsSelected = !item.IsSelected;
        OnPropertyChanged(nameof(HasActiveFilters));
        LoadNotesCommand.Execute(null);
    }

    [RelayCommand]
    private void EditNote(NoteSummary note) => EditNoteRequested?.Invoke(note);

    [RelayCommand]
    private async Task DeleteNote(NoteSummary note)
    {
        if (_settingsService.Current.ConfirmNoteDeletion)
        {
            var confirmation = MessageBox.Show(
                $"Delete note '{note.Title.Value}'?",
                "Delete Note",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirmation != MessageBoxResult.Yes)
                return;
        }

        var result = await _noteService.DeleteNoteAsync(note.Id);
        result.Match(
            success: _ =>
            {
                Notes.Remove(note);
                NoteDeleted?.Invoke(note);
                ShowMessage?.Invoke($"Note '{note.Title}' deleted.");
            },
            failure: error => ShowMessage?.Invoke(error.Message));
    }

    [RelayCommand]
    private void CreateNote() => CreateNoteRequested?.Invoke();

    [RelayCommand]
    private void OpenNote(NoteSummary note) => EditNoteRequested?.Invoke(note);
}
