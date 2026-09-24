using System.Windows.Controls;
using System.Windows.Media;
using NoteApp.Domain.Models;
using NoteApp.Services;
using static NoteApp.UiTests.Notes;

namespace NoteApp.UiTests;

public class CodeBlockUiTests
{
    [Fact]
    public void Code_shows_in_a_monospace_box_and_edits_reach_the_block() => Wpf.Run(() =>
    {
        using var editor = new OpenEditor(With(new NoteBlock.Code("dotnet test\n\tNoteApp.slnx")));
        var block = editor.ViewModel.Blocks[0];

        var box = editor.Find<TextBox>().Single(t => ReferenceEquals(t.Tag, block));
        Assert.Equal("dotnet test\n\tNoteApp.slnx", box.Text);
        Assert.Equal(new FontFamily("Consolas"), box.FontFamily);
        Assert.True(box.AcceptsTab);
        Assert.False(editor.ViewModel.IsDirty);

        box.Text = "curl https://example.com/api ";
        Wpf.Pump();

        Assert.Equal("curl https://example.com/api ", block.CodeText);
        Assert.True(editor.ViewModel.IsDirty);
    });

    // A draft of a note with code restores the code: its text travels as PlainText.
    [Fact]
    public void Code_survives_a_draft() => Wpf.Run(() =>
    {
        using var editor = new OpenEditor(With(new NoteBlock.Code("ls -la")));
        var draft = editor.ViewModel.CaptureDraft();

        editor.ViewModel.Blocks[0].CodeText = "changed";
        editor.ViewModel.RestoreDraft(draft);

        Assert.Equal("ls -la", Assert.Single(editor.ViewModel.Blocks).CodeText);
    });
}
