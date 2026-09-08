using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using NoteApp.Data.Repositories;
using NoteApp.Domain.Extensions;
using NoteApp.Domain.Functional;
using NoteApp.Domain.Models;
using NoteApp.Domain.ValueObjects;
using NoteApp.Services;

namespace NoteApp.ViewModels;

public partial class BlockViewModel : ObservableObject
{
    [ObservableProperty] private BlockType _blockType;
    [ObservableProperty] private string _richTextContent = string.Empty;
    [ObservableProperty] private string _plainTextContent = string.Empty;
    [ObservableProperty] private string _linkUrlText = string.Empty;
    [ObservableProperty] private string _linkDescription = string.Empty;
    [ObservableProperty] private string _fileName = string.Empty;
    [ObservableProperty] private long _fileSize;
    [ObservableProperty] private byte[] _fileData = [];
    [ObservableProperty] private string _fileExtension = string.Empty;

    public Guid Id { get; init; } = Guid.NewGuid();

    public string BlockLabel => BlockType switch
    {
        BlockType.Text => "Text",
        BlockType.File => "File",
        BlockType.Link => "Link",
        _ => "Block"
    };
}

public partial class NoteEditorViewModel : ObservableObject
{
    private readonly NoteService _noteService;
    private readonly ITagRepository _tagRepository;
    private Note? _existingNote;

    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private ObservableCollection<BlockViewModel> _blocks = [];
    [ObservableProperty] private BlockViewModel? _selectedBlock;
    [ObservableProperty] private bool _isSaving;
    [ObservableProperty] private string _errorMessage = string.Empty;
    [ObservableProperty] private ObservableCollection<Tag> _availableTags = [];
    [ObservableProperty] private ObservableCollection<Tag> _selectedTags = [];
    [ObservableProperty] private string _newTagName = string.Empty;
    [ObservableProperty] private bool _isEncrypted;

    private string? _password;

    public bool IsEditing => _existingNote is not null;
    public string EditorTitle => IsEditing ? "Edit Note" : "New Note";

    public void RefreshAfterSave(Note savedNote)
    {
        _existingNote = savedNote;
        OnPropertyChanged(nameof(IsEditing));
        OnPropertyChanged(nameof(EditorTitle));
    }

    public event Action<Note, string?>? SaveCompleted;
    public event Action? CancelRequested;
    public event Func<string?>? PasswordRequested;

    // Events for the view to sync RichTextBox content
    public event Action<BlockViewModel>? BlockAdded;
    public event Action? SyncAllBlocksRequested;

    public NoteEditorViewModel(NoteService noteService, ITagRepository tagRepository, IReadOnlyList<Tag> allTags, Note? existingNote = null, string? password = null)
    {
        _noteService = noteService;
        _tagRepository = tagRepository;
        _existingNote = existingNote;
        _password = password;
        AvailableTags = new ObservableCollection<Tag>(allTags);

        if (existingNote is not null)
            LoadFromNote(existingNote);
    }

    public bool IsTagSelected(Tag tag) =>
        SelectedTags.Any(t => t.Id == tag.Id);

    private void LoadFromNote(Note note)
    {
        Title = note.Title.Value;
        IsEncrypted = note.IsEncrypted;
        SelectedTags = new ObservableCollection<Tag>(note.Tags);

        foreach (var block in note.Blocks)
        {
            var vm = block.Match(
                text: t => new BlockViewModel
                {
                    Id = block.Id,
                    BlockType = BlockType.Text,
                    RichTextContent = t.RichText,
                    PlainTextContent = t.PlainText
                },
                file: f => new BlockViewModel
                {
                    Id = block.Id,
                    BlockType = BlockType.File,
                    FileName = f.FileName,
                    FileSize = f.SizeBytes,
                    FileData = f.Data,
                    FileExtension = f.Extension
                },
                link: l => new BlockViewModel
                {
                    Id = block.Id,
                    BlockType = BlockType.Link,
                    LinkUrlText = l.Url.Value.ToString(),
                    LinkDescription = l.Description
                });
            Blocks.Add(vm);
        }

        if (Blocks.Count > 0)
            SelectedBlock = Blocks[0];
    }

    [RelayCommand]
    private void AddTextBlock()
    {
        var block = new BlockViewModel { BlockType = BlockType.Text };
        Blocks.Add(block);
        SelectedBlock = block;
        BlockAdded?.Invoke(block);
    }

    [RelayCommand]
    private void AddFileBlock()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "All Files (*.*)|*.*|Images (*.png;*.jpg;*.jpeg;*.gif;*.bmp)|*.png;*.jpg;*.jpeg;*.gif;*.bmp|PDF Files (*.pdf)|*.pdf",
            Title = "Select a file to attach"
        };

        if (dialog.ShowDialog() == true)
        {
            var data = File.ReadAllBytes(dialog.FileName);
            var block = new BlockViewModel
            {
                BlockType = BlockType.File,
                FileData = data,
                FileName = Path.GetFileName(dialog.FileName),
                FileExtension = Path.GetExtension(dialog.FileName),
                FileSize = data.LongLength
            };
            Blocks.Add(block);
            SelectedBlock = block;
            BlockAdded?.Invoke(block);
        }
    }

    [RelayCommand]
    private void AddLinkBlock()
    {
        var block = new BlockViewModel { BlockType = BlockType.Link };
        Blocks.Add(block);
        SelectedBlock = block;
        BlockAdded?.Invoke(block);
    }

    [RelayCommand]
    private void RemoveBlock(BlockViewModel block)
    {
        Blocks.Remove(block);
        if (SelectedBlock == block)
            SelectedBlock = Blocks.LastOrDefault();
    }

    [RelayCommand]
    private void MoveBlockUp(BlockViewModel block)
    {
        var index = Blocks.IndexOf(block);
        if (index > 0)
            Blocks.Move(index, index - 1);
    }

    [RelayCommand]
    private void MoveBlockDown(BlockViewModel block)
    {
        var index = Blocks.IndexOf(block);
        if (index < Blocks.Count - 1)
            Blocks.Move(index, index + 1);
    }

    [RelayCommand]
    private void ToggleTag(Tag tag)
    {
        if (SelectedTags.Any(t => t.Id == tag.Id))
            SelectedTags.Remove(SelectedTags.First(t => t.Id == tag.Id));
        else
            SelectedTags.Add(tag);
    }

    [RelayCommand]
    private async Task CreateTagInline()
    {
        if (string.IsNullOrWhiteSpace(NewTagName)) return;

        var nameResult = TagName.From(NewTagName);
        await nameResult.Match<Task>(
            success: async name =>
            {
                // Check if tag already exists
                var existing = AvailableTags.FirstOrDefault(t =>
                    t.Name.Value.Equals(name.Value, StringComparison.OrdinalIgnoreCase));
                if (existing is not null)
                {
                    if (!SelectedTags.Any(t => t.Id == existing.Id))
                        SelectedTags.Add(existing);
                    NewTagName = string.Empty;
                    return;
                }

                var tag = Tag.Create(name);
                var result = await _tagRepository.CreateAsync(tag);
                result.Match(
                    success: created =>
                    {
                        SelectedTags.Add(created);
                        AvailableTags.Add(created);
                        NewTagName = string.Empty;
                    },
                    failure: error => ErrorMessage = error.Message);
            },
            failure: error =>
            {
                ErrorMessage = error.Message;
                return Task.CompletedTask;
            });
    }

    [RelayCommand]
    private void BrowseFileForBlock(BlockViewModel block)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "All Files (*.*)|*.*|Images (*.png;*.jpg;*.jpeg;*.gif;*.bmp)|*.png;*.jpg;*.jpeg;*.gif;*.bmp|PDF Files (*.pdf)|*.pdf",
            Title = "Select a file"
        };

        if (dialog.ShowDialog() == true)
        {
            block.FileData = File.ReadAllBytes(dialog.FileName);
            block.FileName = Path.GetFileName(dialog.FileName);
            block.FileExtension = Path.GetExtension(dialog.FileName);
            block.FileSize = block.FileData.LongLength;
        }
    }

    [RelayCommand]
    private void SaveFileToDisk(BlockViewModel block)
    {
        if (block.FileData.Length == 0) return;

        var dialog = new SaveFileDialog
        {
            FileName = block.FileName,
            Filter = $"Original format (*{block.FileExtension})|*{block.FileExtension}|All Files (*.*)|*.*",
            Title = "Save file to disk"
        };

        if (dialog.ShowDialog() == true)
        {
            File.WriteAllBytes(dialog.FileName, block.FileData);
        }
    }

    [RelayCommand]
    private async Task Save()
    {
        ErrorMessage = string.Empty;
        IsSaving = true;

        // Ask the view to sync all RichTextBox content before saving
        SyncAllBlocksRequested?.Invoke();

        try
        {
            var tags = SelectedTags.ToList() as IReadOnlyList<Tag>;

            if (!BuildBlocks().TryGet(out var blocks, out var blocksError))
            {
                ErrorMessage = blocksError.Message;
                return;
            }

            // Handle password for encryption
            string? savePassword = null;
            if (IsEncrypted)
            {
                if (_password is not null)
                {
                    savePassword = _password;
                }
                else
                {
                    savePassword = PasswordRequested?.Invoke();
                    if (savePassword is null)
                    {
                        ErrorMessage = "Password is required for encrypted notes.";
                        return;
                    }
                    _password = savePassword;
                }
            }

            Result<Note, AppError> result;
            if (IsEditing)
            {
                var titleResult = NoteTitle.From(Title);
                result = await titleResult.Match<Task<Result<Note, AppError>>>(
                    success: async title =>
                    {
                        var updated = _existingNote! with
                        {
                            Title = title,
                            Blocks = blocks,
                            Tags = tags,
                            IsEncrypted = IsEncrypted,
                            UpdatedAt = DateTime.UtcNow
                        };
                        return await _noteService.UpdateNoteAsync(updated, savePassword);
                    },
                    failure: e => Task.FromResult(Result<Note, AppError>.Fail(e)));
            }
            else
            {
                result = await _noteService.CreateNoteAsync(Title, blocks, tags, savePassword);
            }

            result.Match(
                success: note => SaveCompleted?.Invoke(note, _password),
                failure: error => ErrorMessage = error.Message);
        }
        finally
        {
            IsSaving = false;
        }
    }

    private Result<IReadOnlyList<NoteBlock>, AppError> BuildBlocks()
    {
        if (Blocks.Count == 0)
            return Result<IReadOnlyList<NoteBlock>, AppError>.Fail(
                AppError.Validation("A note must have at least one content block."));

        var noteBlocks = new List<NoteBlock>();

        for (var i = 0; i < Blocks.Count; i++)
        {
            var vm = Blocks[i];
            switch (vm.BlockType)
            {
                case BlockType.Text:
                    noteBlocks.Add(new NoteBlock.Text(vm.RichTextContent, vm.PlainTextContent)
                        { Id = vm.Id, SortOrder = i });
                    break;

                case BlockType.File:
                    if (vm.FileData.Length == 0)
                        return Result<IReadOnlyList<NoteBlock>, AppError>.Fail(
                            AppError.Validation($"File block #{i + 1} has no file selected."));
                    noteBlocks.Add(new NoteBlock.File(vm.FileData, vm.FileName, vm.FileExtension, vm.FileSize)
                        { Id = vm.Id, SortOrder = i });
                    break;

                case BlockType.Link:
                    if (!LinkUrl.From(vm.LinkUrlText).TryGet(out var url, out var urlError))
                        return Result<IReadOnlyList<NoteBlock>, AppError>.Fail(urlError);
                    noteBlocks.Add(new NoteBlock.Link(url, vm.LinkDescription)
                        { Id = vm.Id, SortOrder = i });
                    break;
            }
        }

        return Result<IReadOnlyList<NoteBlock>, AppError>.Ok(noteBlocks);
    }

    [RelayCommand]
    private void Cancel() => CancelRequested?.Invoke();
}
