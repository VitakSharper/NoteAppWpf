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

    public static readonly SortOption UpdatedDesc = new("Updated (newest)", nameof(Note.UpdatedAt), ListSortDirection.Descending);
    public static readonly SortOption UpdatedAsc = new("Updated (oldest)", nameof(Note.UpdatedAt), ListSortDirection.Ascending);
    public static readonly SortOption TitleAsc = new("Title (A–Z)", "Title.Value", ListSortDirection.Ascending);
    public static readonly SortOption TitleDesc = new("Title (Z–A)", "Title.Value", ListSortDirection.Descending);
    public static readonly SortOption CreatedDesc = new("Created (newest)", nameof(Note.CreatedAt), ListSortDirection.Descending);
    public static readonly SortOption CreatedAsc = new("Created (oldest)", nameof(Note.CreatedAt), ListSortDirection.Ascending);

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

public partial class NoteListViewModel : ObservableObject
{
    private readonly NoteService _noteService;
    private readonly ITagRepository _tagRepository;
    private readonly AppSettingsService _settingsService;

    [ObservableProperty] private ObservableCollection<Note> _notes = [];
    [ObservableProperty] private ObservableCollection<TagFilterItem> _allTags = [];
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private NoteTypeFilter _selectedTypeFilter = NoteTypeFilter.All;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private Note? _selectedNote;
    [ObservableProperty] private bool _isListView;
    [ObservableProperty] private SortOption _selectedSort = SortOption.UpdatedDesc;

    public IReadOnlyList<NoteTypeFilter> TypeFilters => NoteTypeFilter.AllFilters;
    public IReadOnlyList<SortOption> SortOptions => SortOption.All;

    public event Action<Note>? EditNoteRequested;
    public event Action? CreateNoteRequested;
    public event Action<string>? ShowMessage;

    public NoteListViewModel(
        NoteService noteService,
        ITagRepository tagRepository,
        AppSettingsService settingsService)
    {
        _noteService = noteService;
        _tagRepository = tagRepository;
        _settingsService = settingsService;
    }

    [RelayCommand]
    private void ToggleViewMode() => IsListView = !IsListView;

    partial void OnSelectedSortChanged(SortOption value)
    {
        if (Notes.Count > 0)
            ApplySort();
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
                success: notes => Notes = new ObservableCollection<Note>(notes),
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
                nameof(Note.CreatedAt) => Notes.OrderBy(n => n.CreatedAt),
                _ => Notes.OrderBy(n => n.UpdatedAt)
            }
            : SelectedSort.Field switch
            {
                "Title.Value" => Notes.OrderByDescending(n => n.Title.Value, StringComparer.OrdinalIgnoreCase),
                nameof(Note.CreatedAt) => Notes.OrderByDescending(n => n.CreatedAt),
                _ => Notes.OrderByDescending(n => n.UpdatedAt)
            };

        Notes = new ObservableCollection<Note>(sorted);
    }

    [RelayCommand]
    private async Task Search() => await LoadNotes();

    [RelayCommand]
    private void ClearSearch()
    {
        SearchText = string.Empty;
        foreach (var t in AllTags) t.IsSelected = false;
        SelectedTypeFilter = NoteTypeFilter.All;
        LoadNotesCommand.Execute(null);
    }

    [RelayCommand]
    private void ToggleTagFilter(TagFilterItem item)
    {
        item.IsSelected = !item.IsSelected;
        LoadNotesCommand.Execute(null);
    }

    [RelayCommand]
    private void SelectTypeFilter(NoteTypeFilter filter)
    {
        SelectedTypeFilter = filter;
        LoadNotesCommand.Execute(null);
    }

    [RelayCommand]
    private void EditNote(Note note) => EditNoteRequested?.Invoke(note);

    [RelayCommand]
    private async Task DeleteNote(Note note)
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
                ShowMessage?.Invoke($"Note '{note.Title}' deleted.");
            },
            failure: error => ShowMessage?.Invoke(error.Message));
    }

    [RelayCommand]
    private void CreateNote() => CreateNoteRequested?.Invoke();

    [RelayCommand]
    private void OpenNote(Note note) => EditNoteRequested?.Invoke(note);

    public static string GetBlockSummary(Note note)
    {
        var parts = new List<string>();
        var textCount = note.Blocks.Count(b => b is NoteBlock.Text);
        var fileCount = note.Blocks.Count(b => b is NoteBlock.File);
        var linkCount = note.Blocks.Count(b => b is NoteBlock.Link);
        if (textCount > 0) parts.Add($"{textCount} text");
        if (fileCount > 0) parts.Add($"{fileCount} file{(fileCount > 1 ? "s" : "")}");
        if (linkCount > 0) parts.Add($"{linkCount} link{(linkCount > 1 ? "s" : "")}");
        return string.Join(" · ", parts);
    }
}
