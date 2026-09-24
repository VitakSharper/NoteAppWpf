using NoteApp.Domain.ValueObjects;

namespace NoteApp.ViewModels;

// The notes opened one after the other, the way a browser keeps pages: opening a note adds it
// (and drops whatever was "forward" of the current one), Back and Forward move through it.
// The shell moves first and moves back if the note could not be opened — so opening the
// entry it moved to is not a new visit.
public sealed class NavigationHistory
{
    public const int Capacity = 50;

    private List<NoteId> _entries = [];
    private int _index = -1;

    public bool CanGoBack => _index > 0;
    public bool CanGoForward => _index >= 0 && _index < _entries.Count - 1;
    public NoteId? Current => _index >= 0 ? _entries[_index] : null;

    public void Visit(NoteId id)
    {
        if (Current == id)
            return;

        _entries.RemoveRange(_index + 1, _entries.Count - _index - 1);
        _entries.Add(id);
        if (_entries.Count > Capacity)
            _entries.RemoveAt(0);
        _index = _entries.Count - 1;
    }

    public NoteId? Back() => CanGoBack ? _entries[--_index] : null;

    public NoteId? Forward() => CanGoForward ? _entries[++_index] : null;

    // A deleted note leaves the history, and the notes on either side of it, if they are the
    // same, become one visit. The current entry becomes the last one kept up to where it was.
    public void Forget(NoteId id)
    {
        var kept = new List<NoteId>();
        var index = -1;
        for (var i = 0; i < _entries.Count; i++)
        {
            var entry = _entries[i];
            if (entry != id && (kept.Count == 0 || kept[^1] != entry))
                kept.Add(entry);
            if (i <= _index)
                index = kept.Count - 1;
        }

        _entries = kept;
        _index = kept.Count == 0 ? -1 : Math.Max(index, 0);
    }
}
