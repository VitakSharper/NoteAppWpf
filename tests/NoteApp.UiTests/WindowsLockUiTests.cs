using NoteApp.Services;
using NoteApp.ViewModels;
using static NoteApp.UiTests.Notes;

namespace NoteApp.UiTests;

// Win+L, a remote disconnect, sleep: App.xaml.cs turns those SystemEvents into
// LockForAbsenceAsync, which closes an open encrypted note at once.
public class WindowsLockUiTests
{
    [Fact]
    public void Windows_locking_closes_an_open_encrypted_note() => Wpf.Run(() =>
    {
        var shell = FocusModeUiTests.Shell();
        shell.CurrentEditor = EncryptedEditor();

        shell.LockForAbsenceAsync("when Windows was locked").GetAwaiter().GetResult();

        Assert.Null(shell.CurrentEditor);
    });

    [Fact]
    public void A_plain_note_stays_open() => Wpf.Run(() =>
    {
        var shell = FocusModeUiTests.Shell();
        var editor = new NoteEditorViewModel(new NoteService(new UnusedNoteRepository()), new UnusedTagRepository(), [], With(Text("plain")));
        shell.CurrentEditor = editor;

        shell.LockForAbsenceAsync("when Windows was locked").GetAwaiter().GetResult();

        Assert.Same(editor, shell.CurrentEditor);
    });

    [Fact]
    public void Nothing_is_locked_when_the_setting_is_off() => Wpf.Run(() =>
    {
        var shell = FocusModeUiTests.Shell();
        shell.SettingsViewModel.LockWhenWindowsLocks = false;
        shell.SettingsViewModel.SaveCommand.Execute(null);
        var editor = EncryptedEditor();
        shell.CurrentEditor = editor;

        shell.LockForAbsenceAsync("when Windows was locked").GetAwaiter().GetResult();

        Assert.Same(editor, shell.CurrentEditor);
    });

    private static NoteEditorViewModel EncryptedEditor() =>
        new(new NoteService(new UnusedNoteRepository()), new UnusedTagRepository(), [], With(Text("secret")) with { IsEncrypted = true }, "pw");
}
