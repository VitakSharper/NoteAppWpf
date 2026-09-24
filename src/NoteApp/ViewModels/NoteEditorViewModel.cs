using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
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

public partial class ChecklistItemViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLink), nameof(IsLink))]
    private string _text = string.Empty;

    [ObservableProperty] private bool _isDone;

    // The row shows an open button as soon as the item holds an address, and reads as
    // a link when the address is all there is.
    public bool HasLink => TextLinks.Find(Text).Count > 0;
    public bool IsLink => TextLinks.IsLink(Text);
}

public partial class BlockViewModel : ObservableObject
{
    [ObservableProperty] private BlockType _blockType;
    [ObservableProperty] private string _richTextContent = string.Empty;
    [ObservableProperty] private string _plainTextContent = string.Empty;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanOpenLink))]
    private string _linkUrlText = string.Empty;

    [ObservableProperty] private string _linkDescription = string.Empty;
    [ObservableProperty] private string _fileName = string.Empty;
    [ObservableProperty] private long _fileSize;
    [ObservableProperty] private byte[] _fileData = [];
    [ObservableProperty] private string _fileExtension = string.Empty;

    // Secret block. The password is only ever shown masked (or revealed on demand) and
    // copied through SensitiveClipboard.
    [ObservableProperty] private string _secretLabel = string.Empty;
    [ObservableProperty] private string _secretUserName = string.Empty;
    [ObservableProperty] private string _secretPassword = string.Empty;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanOpenSecretUrl))]
    private string _secretUrl = string.Empty;

    public bool CanOpenSecretUrl => LinkUrl.From(SecretUrl).IsSuccess;

    public Guid Id { get; init; } = Guid.NewGuid();

    public ObservableCollection<ChecklistItemViewModel> ChecklistItems { get; } = [];

    public string ChecklistSummary => $"{ChecklistItems.Count(i => i.IsDone)}/{ChecklistItems.Count} done";

    // View state, not content: hiding the ticked items is not an edit (OnBlockPropertyChanged
    // skips it) and is not stored — every note opens with its whole checklist visible.
    [ObservableProperty] private bool _hideDone;

    public bool HasDone => ChecklistItems.Any(i => i.IsDone);

    // The same rule the save applies, so the open button never offers what cannot be stored.
    public bool CanOpenLink => LinkUrl.From(LinkUrlText).IsSuccess;

    // The block watches its own items so the "2/5 done" line stays true. This is
    // separate from the editor's dirty tracking, which watches the same items for
    // a different reason.
    public BlockViewModel()
    {
        ChecklistItems.CollectionChanged += (_, e) =>
        {
            foreach (var item in e.OldItems?.OfType<ChecklistItemViewModel>() ?? [])
                item.PropertyChanged -= OnItemChanged;

            foreach (var item in e.NewItems?.OfType<ChecklistItemViewModel>() ?? [])
                item.PropertyChanged += OnItemChanged;

            OnPropertyChanged(nameof(ChecklistSummary));
            OnPropertyChanged(nameof(HasDone));
        };
    }

    private void OnItemChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ChecklistItemViewModel.IsDone))
        {
            OnPropertyChanged(nameof(ChecklistSummary));
            OnPropertyChanged(nameof(HasDone));
        }
    }

    public string BlockLabel => BlockType switch
    {
        BlockType.Text => "Text",
        BlockType.File => "File",
        BlockType.Link => "Link",
        BlockType.Checklist => "Checklist",
        BlockType.Secret => "Secret",
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

    // The leave guard puts the list selection back on the note it kept open, and the
    // shell's trash button needs the stored note behind the editor (null while new).
    public NoteId? EditedNoteId => _existingNote?.Id;
    public Note? EditedNote => _existingNote;

    // The note-list search that led here, if any: the view opens the find bar of the
    // first text block that contains it, so the match is on screen straight away.
    public string? SearchTerm { get; init; }

    // --- Drafts (see DraftStore) ---

    // A note never saved has no id yet: its draft goes under a key of its own, which a
    // restored draft hands back so the same file keeps being updated.
    private Guid _newNoteDraftKey = Guid.NewGuid();
    public Guid NewNoteDraftKey => _newNoteDraftKey;
    public Guid DraftKey => EditedNoteId?.Value ?? _newNoteDraftKey;

    // Never for an encrypted note, stored or about to be, nor for one holding a secret: the
    // draft is plain JSON on disk, and a password has no business there.
    public bool CanKeepDraft => !IsEncrypted && _existingNote?.IsEncrypted != true
        && Blocks.All(b => b.BlockType != BlockType.Secret);

    // The view pushes the rich text into the blocks without turning addresses into links:
    // a snapshot can land in the middle of an address being typed.
    public event Action? SnapshotRequested;

    public NoteDraft CaptureDraft()
    {
        SnapshotRequested?.Invoke();

        var blocks = Blocks.Select(b => b.BlockType switch
        {
            BlockType.Text => new DraftBlock(BlockType.Text, RichText: b.RichTextContent, PlainText: b.PlainTextContent),
            BlockType.Link => new DraftBlock(BlockType.Link, LinkUrl: b.LinkUrlText, LinkDescription: b.LinkDescription),
            BlockType.File => new DraftBlock(BlockType.File, FileName: b.FileName, FileExtension: b.FileExtension, FileData: b.FileData),
            // Never reached while CanKeepDraft holds, and even then: never the password.
            BlockType.Secret => new DraftBlock(BlockType.Secret, SecretLabel: b.SecretLabel, SecretUserName: b.SecretUserName, SecretUrl: b.SecretUrl),
            _ => new DraftBlock(BlockType.Checklist,
                Items: b.ChecklistItems.Select(i => new DraftChecklistItem(i.Text, i.IsDone)).ToList())
        }).ToList();

        return new NoteDraft(DraftKey, EditedNoteId?.Value, Title, blocks, SelectedTags.Select(t => t.Id).ToList(), DateTime.Now);
    }

    // Replaces what the editor holds with the draft; the note is then modified, as it was
    // when the draft was written. Tags that no longer exist are dropped.
    public void RestoreDraft(NoteDraft draft)
    {
        if (draft.NoteId is null)
            _newNoteDraftKey = draft.Key;

        Title = draft.Title;

        while (Blocks.Count > 0)
            Blocks.RemoveAt(Blocks.Count - 1);

        foreach (var block in draft.Blocks)
        {
            var vm = new BlockViewModel
            {
                BlockType = block.Type,
                RichTextContent = block.RichText ?? string.Empty,
                PlainTextContent = block.PlainText ?? string.Empty,
                LinkUrlText = block.LinkUrl ?? string.Empty,
                LinkDescription = block.LinkDescription ?? string.Empty,
                FileName = block.FileName ?? string.Empty,
                FileExtension = block.FileExtension ?? string.Empty,
                FileData = block.FileData ?? [],
                FileSize = block.FileData?.LongLength ?? 0,
                SecretLabel = block.SecretLabel ?? string.Empty,
                SecretUserName = block.SecretUserName ?? string.Empty,
                SecretUrl = block.SecretUrl ?? string.Empty
            };
            foreach (var item in block.Items ?? [])
                vm.ChecklistItems.Add(new ChecklistItemViewModel { Text = item.Text, IsDone = item.IsDone });
            Blocks.Add(vm);
        }

        while (SelectedTags.Count > 0)
            SelectedTags.RemoveAt(SelectedTags.Count - 1);
        foreach (var tag in AvailableTags.Where(t => draft.TagIds.Contains(t.Id)))
            SelectedTags.Add(tag);

        SelectedBlock = Blocks.FirstOrDefault();
        MarkDirty();
    }

    private bool _isDirty;

    // Set at the source of every change rather than by comparing a snapshot on the way
    // out: TextRange.Save(XamlPackage) is a binary container with no promise of being
    // byte-identical for the same document, so comparing would report phantom edits.
    public bool IsDirty
    {
        get => _isDirty;
        private set => SetProperty(ref _isDirty, value);
    }

    // The rich text lives in the view's RichTextBox and only reaches the block on
    // LostFocus or right before a save — far too late to guard the exits, so the view
    // reports its edits here as they happen.
    public void MarkDirty() => IsDirty = true;

    public void RefreshAfterSave(Note savedNote)
    {
        _existingNote = savedNote;
        OnPropertyChanged(nameof(IsEditing));
        OnPropertyChanged(nameof(EditorTitle));
        IsDirty = false;
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

        // Only now: filling the fields from the stored note is not an edit.
        PropertyChanged += OnEditorPropertyChanged;
        Blocks.CollectionChanged += OnBlocksCollectionChanged;
        SelectedTags.CollectionChanged += OnSelectedTagsCollectionChanged;
        foreach (var block in Blocks)
            Track(block);
    }

    // Title and IsEncrypted are the only editable scalars on this view model; the rest
    // is transient UI state (IsSaving, ErrorMessage, SelectedBlock, NewTagName...).
    private void OnEditorPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(Title) or nameof(IsEncrypted))
            MarkDirty();
    }

    private void OnBlocksCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (var block in e.OldItems?.OfType<BlockViewModel>() ?? [])
            Untrack(block);

        foreach (var block in e.NewItems?.OfType<BlockViewModel>() ?? [])
            Track(block);

        MarkDirty();
    }

    // A checklist edit is three different events — an item added or removed, an item
    // ticked, an item retyped — and none of them touches a property of the block.
    private void Track(BlockViewModel block)
    {
        block.PropertyChanged += OnBlockPropertyChanged;
        block.ChecklistItems.CollectionChanged += OnChecklistItemsChanged;
        foreach (var item in block.ChecklistItems)
            item.PropertyChanged += OnChecklistItemPropertyChanged;
    }

    private void Untrack(BlockViewModel block)
    {
        block.PropertyChanged -= OnBlockPropertyChanged;
        block.ChecklistItems.CollectionChanged -= OnChecklistItemsChanged;
        foreach (var item in block.ChecklistItems)
            item.PropertyChanged -= OnChecklistItemPropertyChanged;
    }

    private void OnChecklistItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (var item in e.OldItems?.OfType<ChecklistItemViewModel>() ?? [])
            item.PropertyChanged -= OnChecklistItemPropertyChanged;

        foreach (var item in e.NewItems?.OfType<ChecklistItemViewModel>() ?? [])
            item.PropertyChanged += OnChecklistItemPropertyChanged;

        MarkDirty();
    }

    private void OnChecklistItemPropertyChanged(object? sender, PropertyChangedEventArgs e) => MarkDirty();

    private void OnSelectedTagsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        MarkDirty();

    // RichTextContent and PlainTextContent are written by the view's SyncBlock, not by
    // the user: that write is a serialization artefact and must not look like an edit.
    private void OnBlockPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(BlockViewModel.RichTextContent) or nameof(BlockViewModel.PlainTextContent)
            or nameof(BlockViewModel.HideDone))
            return;

        MarkDirty();
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
                },
                checklist: c =>
                {
                    var blockVm = new BlockViewModel { Id = block.Id, BlockType = BlockType.Checklist };
                    foreach (var item in c.Items)
                        blockVm.ChecklistItems.Add(new ChecklistItemViewModel { Text = item.Text, IsDone = item.IsDone });
                    return blockVm;
                },
                secret: s => new BlockViewModel
                {
                    Id = block.Id,
                    BlockType = BlockType.Secret,
                    SecretLabel = s.Label,
                    SecretUserName = s.UserName,
                    SecretPassword = s.Password,
                    SecretUrl = s.Url
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
            AddFileBlocks([dialog.FileName]);
    }

    // The File button and a drop from Explorer: one File block per file, in the order
    // given. Folders and files that cannot be read are skipped and reported.
    public void AddFileBlocks(IEnumerable<string> paths)
    {
        var skipped = new List<string>();
        foreach (var path in paths)
        {
            try
            {
                var data = File.ReadAllBytes(path);
                var block = new BlockViewModel
                {
                    BlockType = BlockType.File,
                    FileData = data,
                    FileName = Path.GetFileName(path),
                    FileExtension = Path.GetExtension(path),
                    FileSize = data.LongLength
                };
                Blocks.Add(block);
                SelectedBlock = block;
                BlockAdded?.Invoke(block);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                skipped.Add(Path.GetFileName(path));
            }
        }

        if (skipped.Count > 0)
            ErrorMessage = $"Not attached (a folder, or unreadable): {string.Join(", ", skipped)}";
    }

    // A block dragged by its grip: newIndex is where it lands in the list as it is now.
    public void MoveBlock(BlockViewModel block, int newIndex)
    {
        var index = Blocks.IndexOf(block);
        newIndex = Math.Clamp(newIndex, 0, Blocks.Count - 1);
        if (index >= 0 && index != newIndex)
            Blocks.Move(index, newIndex);
    }

    [RelayCommand]
    private void AddLinkBlock()
    {
        var block = new BlockViewModel { BlockType = BlockType.Link };
        Blocks.Add(block);
        SelectedBlock = block;
        BlockAdded?.Invoke(block);
    }

    // Starts with one empty row: a checklist with no item fails validation on save,
    // and an empty card would give the user nothing to type into.
    [RelayCommand]
    private void AddChecklistBlock()
    {
        var block = new BlockViewModel { BlockType = BlockType.Checklist };
        block.ChecklistItems.Add(new ChecklistItemViewModel());
        Blocks.Add(block);
        SelectedBlock = block;
        BlockAdded?.Invoke(block);
    }

    [RelayCommand]
    private void AddSecretBlock()
    {
        var block = new BlockViewModel { BlockType = BlockType.Secret };
        Blocks.Add(block);
        SelectedBlock = block;
        BlockAdded?.Invoke(block);
    }

    [RelayCommand]
    private void AddChecklistItem(BlockViewModel block) =>
        block.ChecklistItems.Add(new ChecklistItemViewModel());

    // The row menu passes the item, not the block: find its owner rather than make
    // the view keep track of both.
    [RelayCommand]
    private void RemoveChecklistItem(ChecklistItemViewModel item) =>
        Blocks.FirstOrDefault(b => b.ChecklistItems.Contains(item))?.ChecklistItems.Remove(item);

    // Enter inside an item: the view calls this, then focuses what it returns.
    public ChecklistItemViewModel InsertChecklistItemAfter(BlockViewModel block, ChecklistItemViewModel after)
    {
        var item = new ChecklistItemViewModel();
        var index = block.ChecklistItems.IndexOf(after);
        block.ChecklistItems.Insert(index < 0 ? block.ChecklistItems.Count : index + 1, item);
        return item;
    }

    // "[] " typed at the start of a line of a text block (NoteEditorView): that line becomes
    // the first item of a checklist right below the block, and what followed the line moves
    // into a text block of its own below the checklist. A text block left empty goes away.
    public ChecklistItemViewModel SplitIntoChecklist(BlockViewModel textBlock, string firstItem, string? tailRichText, bool dropTextBlock)
    {
        var index = Blocks.IndexOf(textBlock);
        var item = new ChecklistItemViewModel { Text = firstItem };
        var checklist = new BlockViewModel { BlockType = BlockType.Checklist };
        checklist.ChecklistItems.Add(item);

        Blocks.Insert(index + 1, checklist);
        if (tailRichText is not null)
            Blocks.Insert(index + 2, new BlockViewModel { BlockType = BlockType.Text, RichTextContent = tailRichText });
        if (dropTextBlock)
            Blocks.Remove(textBlock);

        SelectedBlock = checklist;
        return item;
    }

    // Alt+Up / Alt+Down in an item: it swaps places with the next visible row that way, so
    // with "Hide done" on it hops over the hidden ticked items instead of seeming stuck.
    // false when it is already at that end of its list.
    public bool MoveChecklistItem(ChecklistItemViewModel item, int offset)
    {
        var block = Blocks.FirstOrDefault(b => b.ChecklistItems.Contains(item));
        if (block is null)
            return false;

        var items = block.ChecklistItems;
        var index = items.IndexOf(item);
        var target = index + offset;
        while (target >= 0 && target < items.Count && block.HideDone && items[target].IsDone)
            target += offset;

        if (target < 0 || target >= items.Count)
            return false;

        items.Move(index, target);
        return true;
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

                case BlockType.Checklist:
                    // Blank rows are the trace of typing, not content: they are dropped
                    // rather than stored, but a checklist of nothing but blanks is a
                    // mistake worth reporting.
                    var items = vm.ChecklistItems
                        .Where(item => !string.IsNullOrWhiteSpace(item.Text))
                        .Select(item => new ChecklistItem(item.Text.Trim(), item.IsDone))
                        .ToList();

                    if (items.Count == 0)
                        return Result<IReadOnlyList<NoteBlock>, AppError>.Fail(
                            AppError.Validation($"Checklist block #{i + 1} has no items."));

                    noteBlocks.Add(new NoteBlock.Checklist(items) { Id = vm.Id, SortOrder = i });
                    break;

                // The address is optional, but one that is there has to be a real one. The
                // password is kept exactly as typed: its spaces may be part of it.
                case BlockType.Secret:
                    var secretUrl = vm.SecretUrl.Trim();
                    if (secretUrl.Length > 0 && !LinkUrl.From(secretUrl).TryGet(out _, out var secretUrlError))
                        return Result<IReadOnlyList<NoteBlock>, AppError>.Fail(
                            AppError.Validation($"Secret block #{i + 1}: {secretUrlError.Message}"));

                    var secret = new NoteBlock.Secret(vm.SecretLabel.Trim(), vm.SecretUserName.Trim(), vm.SecretPassword, secretUrl)
                        { Id = vm.Id, SortOrder = i };
                    if (secret.PlainText.Length == 0 && secret.Password.Length == 0)
                        return Result<IReadOnlyList<NoteBlock>, AppError>.Fail(
                            AppError.Validation($"Secret block #{i + 1} is empty."));

                    noteBlocks.Add(secret);
                    break;
            }
        }

        return Result<IReadOnlyList<NoteBlock>, AppError>.Ok(noteBlocks);
    }

    [RelayCommand]
    private void Cancel() => CancelRequested?.Invoke();
}
