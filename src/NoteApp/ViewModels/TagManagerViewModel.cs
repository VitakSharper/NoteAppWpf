using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NoteApp.Data.Repositories;
using NoteApp.Domain.Models;
using NoteApp.Domain.ValueObjects;
using NoteApp.Services;

namespace NoteApp.ViewModels;

public partial class TagManagerViewModel : ObservableObject
{
    private readonly ITagRepository _tagRepository;
    private readonly AppSettingsService _settingsService;

    [ObservableProperty] private ObservableCollection<Tag> _tags = [];
    [ObservableProperty] private string _newTagName = string.Empty;
    [ObservableProperty] private Tag? _editingTag;
    [ObservableProperty] private string _editTagName = string.Empty;
    [ObservableProperty] private bool _isEditing;
    [ObservableProperty] private bool _isLoading;

    public event Action<string>? ShowMessage;

    public TagManagerViewModel(ITagRepository tagRepository, AppSettingsService settingsService)
    {
        _tagRepository = tagRepository;
        _settingsService = settingsService;
    }

    [RelayCommand]
    public async Task LoadTags()
    {
        IsLoading = true;
        try
        {
            var result = await _tagRepository.GetAllAsync();
            result.Match(
                success: tags => Tags = new ObservableCollection<Tag>(tags),
                failure: error => ShowMessage?.Invoke(error.Message));
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task CreateTag()
    {
        var nameResult = TagName.From(NewTagName);
        await nameResult.Match<Task>(
            success: async name =>
            {
                var tag = Tag.Create(name);
                var result = await _tagRepository.CreateAsync(tag);
                result.Match(
                    success: created =>
                    {
                        Tags.Add(created);
                        NewTagName = string.Empty;
                        ShowMessage?.Invoke($"Tag '{created.Name}' created.");
                    },
                    failure: error => ShowMessage?.Invoke(error.Message));
            },
            failure: error =>
            {
                ShowMessage?.Invoke(error.Message);
                return Task.CompletedTask;
            });
    }

    [RelayCommand]
    private void BeginEdit(Tag tag)
    {
        EditingTag = tag;
        EditTagName = tag.Name.Value;
        IsEditing = true;
    }

    [RelayCommand]
    private void CancelEdit()
    {
        EditingTag = null;
        EditTagName = string.Empty;
        IsEditing = false;
    }

    [RelayCommand]
    private async Task SaveEdit()
    {
        if (EditingTag is null) return;

        var nameResult = TagName.From(EditTagName);
        await nameResult.Match<Task>(
            success: async name =>
            {
                var updated = EditingTag with { Name = name };
                var result = await _tagRepository.UpdateAsync(updated);
                result.Match(
                    success: _ =>
                    {
                        var index = Tags.IndexOf(EditingTag);
                        if (index >= 0) Tags[index] = updated;
                        CancelEdit();
                        ShowMessage?.Invoke($"Tag renamed to '{name}'.");
                    },
                    failure: error => ShowMessage?.Invoke(error.Message));
            },
            failure: error =>
            {
                ShowMessage?.Invoke(error.Message);
                return Task.CompletedTask;
            });
    }

    [RelayCommand]
    private async Task DeleteTag(Tag tag)
    {
        if (_settingsService.Current.ConfirmTagDeletion)
        {
            var confirmation = MessageBox.Show(
                $"Delete tag '{tag.Name.Value}'?",
                "Delete Tag",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirmation != MessageBoxResult.Yes)
                return;
        }

        var result = await _tagRepository.DeleteAsync(tag.Id);
        result.Match(
            success: _ =>
            {
                Tags.Remove(tag);
                ShowMessage?.Invoke($"Tag '{tag.Name}' deleted.");
            },
            failure: error => ShowMessage?.Invoke(error.Message));
    }
}
