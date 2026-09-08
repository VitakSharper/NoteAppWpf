using NoteApp.Domain.Models;

namespace NoteApp.Tests.Domain;

public class NoteTests
{
    [Fact]
    public void Create_RequiresAtLeastOneBlock()
    {
        var result = Note.Create(Title("Empty"), [], []);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Create_DefaultsToUnencrypted_AndStampsUtcDates()
    {
        var note = SampleNote(new NoteBlock.Text("<rich/>"));

        Assert.False(note.IsEncrypted);
        Assert.Equal(DateTimeKind.Utc, note.CreatedAt.Kind);
        Assert.Equal(note.CreatedAt, note.UpdatedAt);
    }

    [Fact]
    public void WithTitle_ReturnsAnUpdatedCopy_AndLeavesTheOriginalAlone()
    {
        var note = SampleNote(new NoteBlock.Text("<rich/>"));

        var renamed = note.WithTitle(Title("Renamed"));

        Assert.Equal("Sample", note.Title.Value);
        Assert.Equal("Renamed", renamed.Title.Value);
        Assert.True(renamed.UpdatedAt >= note.UpdatedAt);
    }

    [Fact]
    public void HasText_HasFiles_HasLinks_ReflectTheBlocks()
    {
        var mixed = SampleNote(SampleBlocks());
        var textOnly = SampleNote(new NoteBlock.Text("<rich/>"));

        Assert.True(mixed.HasText);
        Assert.True(mixed.HasFiles);
        Assert.True(mixed.HasLinks);
        Assert.True(textOnly.HasText);
        Assert.False(textOnly.HasFiles);
        Assert.False(textOnly.HasLinks);
    }

    [Fact]
    public void Text_PlainTextDefaultsToEmpty()
    {
        var text = new NoteBlock.Text("<rich/>");

        Assert.Equal(string.Empty, text.PlainText);
        Assert.Equal(BlockType.Text, text.Type);
    }
}
