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
    // Trash view: the same list, search and filters, over the soft-deleted notes only.
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EmptyTrashCommand))]
    private bool _isTrashView;

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
    // A snackbar with an action button: the message, and what UNDO does.
    public event Action<string, Action>? ShowUndoableMessage;
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

    // Trash rows never open: saving a trashed note from the editor would quietly
    // resurrect it. They are restored or purged from the row menu instead.
    partial void OnSelectedNoteChanged(NoteSummary? value)
    {
        if (value is { IsDeleted: false })
            EditNoteRequested?.Invoke(value);
    }

    partial void OnIsTrashViewChanged(bool value)
    {
        SelectedNote = null;
        LoadNotesCommand.Execute(null);
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
                SelectedTypeFilter.Value,
                deletedOnly: IsTrashView);

            result.Match(
                success: notes => Notes = new ObservableCollection<NoteSummary>(notes),
                failure: error => ShowMessage?.Invoke(error.Message));

            ApplySort();
            EmptyTrashCommand.NotifyCanExecuteChanged();

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

    // Soft delete: no confirmation, the snackbar's UNDO is the safety net.
    [RelayCommand]
    private async Task DeleteNote(NoteSummary note)
    {
        if (!(await _noteService.DeleteNoteAsync(note.Id)).TryGet(out _, out var error))
        {
            ShowMessage?.Invoke(error.Message);
            return;
        }

        Notes.Remove(note);
        NoteDeleted?.Invoke(note);
        ShowUndoableMessage?.Invoke($"Note '{note.Title}' moved to trash.", () => RestoreNoteCommand.Execute(note));
    }

    // UNDO and the trash row menu. Reloads rather than re-inserting the row: the
    // current sort and filters decide where — and whether — the note shows up.
    [RelayCommand]
    private async Task RestoreNote(NoteSummary note)
    {
        if (!(await _noteService.RestoreNoteAsync(note.Id)).TryGet(out _, out var error))
        {
            ShowMessage?.Invoke(error.Message);
            return;
        }

        await LoadNotes();
        ShowMessage?.Invoke($"Note '{note.Title}' restored.");
    }

    [RelayCommand]
    private async Task PurgeNote(NoteSummary note)
    {
        if (!ConfirmPermanentDeletion($"Delete note '{note.Title.Value}' forever? This cannot be undone."))
            return;

        if (!(await _noteService.PurgeNoteAsync(note.Id)).TryGet(out _, out var error))
        {
            ShowMessage?.Invoke(error.Message);
            return;
        }

        Notes.Remove(note);
        EmptyTrashCommand.NotifyCanExecuteChanged();
        ShowMessage?.Invoke($"Note '{note.Title}' deleted forever.");
    }

    private bool CanEmptyTrash => IsTrashView && Notes.Count > 0;

    // Purges every trashed note, not only the rows the current search/filters show.
    [RelayCommand(CanExecute = nameof(CanEmptyTrash))]
    private async Task EmptyTrash()
    {
        if (!ConfirmPermanentDeletion("Delete every note in the trash forever — not only the ones currently shown? This cannot be undone."))
            return;

        if (!(await _noteService.EmptyTrashAsync()).TryGet(out var count, out var error))
        {
            ShowMessage?.Invoke(error.Message);
            return;
        }

        Notes.Clear();
        EmptyTrashCommand.NotifyCanExecuteChanged();
        ShowMessage?.Invoke($"Trash emptied: {count} note(s) deleted forever.");
    }

    // The ConfirmNoteDeletion setting guards the permanent gestures only: moving
    // a note to the trash is undoable and asks nothing.
    private bool ConfirmPermanentDeletion(string question) =>
        !_settingsService.Current.ConfirmNoteDeletion
        || MessageBox.Show(question, "Delete forever", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;

    [RelayCommand]
    private void CreateNote() => CreateNoteRequested?.Invoke();

    [RelayCommand]
    private void OpenNote(NoteSummary note) => EditNoteRequested?.Invoke(note);
}
