using System.IO;
using System.Text.Json;
using NoteApp.Domain.Models;

namespace NoteApp.Services;

public sealed record DraftChecklistItem(string Text, bool IsDone);

// One block as the editor holds it, valid or not: a draft keeps a half-typed link too.
public sealed record DraftBlock(
    BlockType Type,
    string? RichText = null,
    string? PlainText = null,
    string? LinkUrl = null,
    string? LinkDescription = null,
    string? FileName = null,
    string? FileExtension = null,
    byte[]? FileData = null,
    IReadOnlyList<DraftChecklistItem>? Items = null,
    // A secret never reaches a draft (NoteEditorViewModel.CanKeepDraft); should it, its
    // password stays out.
    string? SecretLabel = null,
    string? SecretUserName = null,
    string? SecretUrl = null);

// The unsaved state of the open editor. Key is the note's id, or an id of its own for a
// note never saved; NoteId is null for the latter.
public sealed record NoteDraft(
    Guid Key,
    Guid? NoteId,
    string Title,
    IReadOnlyList<DraftBlock> Blocks,
    IReadOnlyList<Guid> TagIds,
    DateTime SavedAt);

// Drafts\<key>.json in the data folder: written while a note has unsaved changes, deleted
// when they are saved or discarded, so a file still there at startup is work a crash (or a
// power cut) would otherwise have lost. Encrypted notes never get one — see MainViewModel.
public sealed class DraftStore(string folder)
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = false };

    public DraftStore() : this(AppPaths.DraftsFolder)
    {
    }

    public string Folder { get; } = folder;

    // Through a temporary file: a crash in the middle of a write must not destroy the
    // previous draft, which is the whole point of having one.
    public void Save(NoteDraft draft)
    {
        Directory.CreateDirectory(Folder);
        var path = PathOf(draft.Key);
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(draft, Options));
        File.Move(temporary, path, overwrite: true);
    }

    public void Delete(Guid key)
    {
        try
        {
            File.Delete(PathOf(key));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Left for the next startup, which offers it again.
        }
    }

    // Newest first. A file that cannot be read is skipped, not deleted: it may still be
    // someone's text, and a person can open it by hand.
    public IReadOnlyList<NoteDraft> LoadAll()
    {
        if (!Directory.Exists(Folder))
            return [];

        var drafts = new List<NoteDraft>();
        foreach (var path in Directory.GetFiles(Folder, "*.json"))
        {
            try
            {
                if (JsonSerializer.Deserialize<NoteDraft>(File.ReadAllText(path), Options) is { } draft)
                    drafts.Add(draft);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
            }
        }

        return drafts.OrderByDescending(d => d.SavedAt).ToList();
    }

    private string PathOf(Guid key) => Path.Combine(Folder, $"{key}.json");
}
