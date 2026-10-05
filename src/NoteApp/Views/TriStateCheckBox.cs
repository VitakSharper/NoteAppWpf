using System.Windows.Controls;

namespace NoteApp.Views;

// A checklist item's box: not started → in progress (the indeterminate mark) → done → not
// started. WPF's own three-state CheckBox goes through done before the indeterminate state,
// which would make "started" the step after "finished".
// An implicit style keyed on CheckBox does not reach a subclass: give it one explicitly.
public sealed class TriStateCheckBox : CheckBox
{
    public TriStateCheckBox() => IsThreeState = true;

    protected override void OnToggle() =>
        SetCurrentValue(IsCheckedProperty, IsChecked switch
        {
            false => null,
            null => true,
            true => (bool?)false
        });
}
