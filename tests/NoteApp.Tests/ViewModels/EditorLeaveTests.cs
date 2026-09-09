using System.Windows;
using NoteApp.ViewModels;

namespace NoteApp.Tests.ViewModels;

public class EditorLeaveTests
{
    [Fact]
    public void Yes_saves() =>
        Assert.Equal(LeaveChoice.Save, EditorLeave.Choose(MessageBoxResult.Yes));

    [Fact]
    public void No_discards() =>
        Assert.Equal(LeaveChoice.Discard, EditorLeave.Choose(MessageBoxResult.No));

    [Fact]
    public void Cancel_stays() =>
        Assert.Equal(LeaveChoice.Stay, EditorLeave.Choose(MessageBoxResult.Cancel));

    // A dialog that could not be shown reports None (0). Writing to the note on that
    // basis is exactly the accident this guard exists to prevent.
    [Theory]
    [InlineData(MessageBoxResult.None)]
    [InlineData(MessageBoxResult.OK)]
    [InlineData((MessageBoxResult)42)]
    public void Anything_unrecognised_stays_in_the_editor(MessageBoxResult answer) =>
        Assert.Equal(LeaveChoice.Stay, EditorLeave.Choose(answer));
}
