using System.Windows;
using System.Windows.Controls;
using NoteApp.Domain.Models;
using NoteApp.ViewModels;
using static NoteApp.UiTests.Notes;

namespace NoteApp.UiTests;

public class SecretBlockUiTests
{
    [Fact]
    public void The_password_shows_masked_and_edits_reach_the_block() => Wpf.Run(() =>
    {
        using var editor = new OpenEditor(With(new NoteBlock.Secret("Support site", "vbanard", "hunter2", "https://support.example.com/")));
        var block = editor.ViewModel.Blocks[0];

        var passwordBox = Assert.Single(editor.Find<PasswordBox>());
        Assert.Equal("hunter2", passwordBox.Password);
        var texts = editor.Find<TextBox>().Where(t => ReferenceEquals(t.Tag, block)).Select(t => t.Text).ToList();
        Assert.Contains("Support site", texts);
        Assert.Contains("vbanard", texts);
        Assert.Contains("https://support.example.com/", texts);
        Assert.DoesNotContain("hunter2", texts); // not revealed until the eye is clicked
        Assert.False(editor.ViewModel.IsDirty);

        passwordBox.Password = "hunter3";
        Wpf.Pump();

        Assert.Equal("hunter3", block.SecretPassword);
        Assert.True(editor.ViewModel.IsDirty);
    });

    [Fact]
    public void A_note_that_is_not_encrypted_warns_that_the_password_is_stored_as_plain_text() => Wpf.Run(() =>
    {
        using var editor = new OpenEditor(With(new NoteBlock.Secret("Wifi", "", "hunter2")));
        var warning = editor.Find<TextBlock>().Single(t => t.Text.StartsWith("This note is not encrypted", StringComparison.Ordinal));
        Assert.True(warning.IsVisible);

        editor.ViewModel.IsEncrypted = true;
        Wpf.Pump();

        Assert.False(warning.IsVisible);
    });
}
