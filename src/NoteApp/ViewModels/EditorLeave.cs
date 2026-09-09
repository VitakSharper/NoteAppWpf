using System.Windows;

namespace NoteApp.ViewModels;

public enum LeaveChoice
{
    Save,
    Discard,
    Stay
}

public static class EditorLeave
{
    // Only an explicit Yes may write to the note. A message box can hand back
    // something else entirely — None, or a raw 0 when the dialog could not be shown
    // over a window that is already closing — and reading anything unrecognised as
    // "save" once cost a real note its title. Unknown means stay in the editor.
    public static LeaveChoice Choose(MessageBoxResult answer) => answer switch
    {
        MessageBoxResult.Yes => LeaveChoice.Save,
        MessageBoxResult.No => LeaveChoice.Discard,
        _ => LeaveChoice.Stay
    };
}
