using NoteApp.Data.Entities;
using NoteApp.Data.Queries;
using NoteApp.Domain.Models;
using NoteApp.Services.Mapping;

namespace NoteApp.Tests.Services;

public class NoteMapperTests
{
    [Fact]
    public void ToEntity_ThenToDomain_RoundTripsEveryBlockKindInOrder()
    {
        var note = SampleNote(SampleBlocks());

        var entity = NoteMapper.ToEntity(note);
        // The repository adds the join rows; mirror that (with the Tag navigation loaded).
        entity.NoteTags = note.Tags
            .Select(t => new NoteTagEntity { NoteId = entity.Id, TagId = t.Id, Tag = new TagEntity { Id = t.Id, Name = t.Name.Value } })
            .ToList();

        var mapped = NoteMapper.ToDomain(entity).Unwrap();

        Assert.Equal(note.Id, mapped.Id);
        Assert.Equal("Sample", mapped.Title.Value);
        Assert.Equal(4, mapped.Blocks.Count);

        var text = Assert.IsType<NoteBlock.Text>(mapped.Blocks[0]);
        Assert.Equal("<rich/>", text.RichText);
        Assert.Equal("plain text", text.PlainText);

        var file = Assert.IsType<NoteBlock.File>(mapped.Blocks[1]);
        Assert.Equal([1, 2, 3], file.Data);
        Assert.Equal("a.bin", file.FileName);

        var link = Assert.IsType<NoteBlock.Link>(mapped.Blocks[2]);
        Assert.Equal("https://example.com/page", link.Url.ToString());
        Assert.Equal("example", link.Description);

        var checklist = Assert.IsType<NoteBlock.Checklist>(mapped.Blocks[3]);
        Assert.Equal(["buy milk", "call the bank"], checklist.Items.Select(i => i.Text));
        Assert.Equal([true, false], checklist.Items.Select(i => i.IsDone));

        Assert.Equal("work", Assert.Single(mapped.Tags).Name.Value);
    }

    // The checklist's searchable form has to reach the column search reads, or a
    // note's tasks would be invisible to it.
    [Fact]
    public void ToBlockEntities_WritesChecklistItemsAsJsonAndFillsPlainText()
    {
        var note = SampleNote(
            new NoteBlock.Checklist([new ChecklistItem("buy milk", true), new ChecklistItem("call the bank", false)]));

        var entity = Assert.Single(NoteMapper.ToBlockEntities(note));

        Assert.Equal(BlockType.Checklist, entity.BlockType);
        Assert.Equal("buy milk\ncall the bank", entity.PlainText);
        Assert.Equal("""[{"t":"buy milk","d":true},{"t":"call the bank"}]""", entity.ChecklistJson);
    }

    [Fact]
    public void ToBlockEntities_ReassignsSortOrderFromListPosition()
    {
        var note = SampleNote(
            new NoteBlock.Text("a") { SortOrder = 5 },
            new NoteBlock.Text("b") { SortOrder = 9 });

        var entities = NoteMapper.ToBlockEntities(note);

        Assert.Equal([0, 1], entities.Select(e => e.SortOrder));
        Assert.All(entities, e => Assert.Equal(note.Id.Value, e.NoteId));
    }

    [Fact]
    public void ToDomain_OrdersBlocksBySortOrder_WhateverTheStorageOrder()
    {
        var entity = new NoteEntity
        {
            Id = Guid.NewGuid(),
            Title = "T",
            Blocks =
            [
                new NoteBlockEntity { Id = Guid.NewGuid(), BlockType = BlockType.Text, SortOrder = 1, TextContent = "second" },
                new NoteBlockEntity { Id = Guid.NewGuid(), BlockType = BlockType.Text, SortOrder = 0, TextContent = "first" }
            ]
        };

        var mapped = NoteMapper.ToDomain(entity).Unwrap();

        Assert.Equal(["first", "second"], mapped.Blocks.Cast<NoteBlock.Text>().Select(b => b.RichText));
    }

    [Fact]
    public void ToDomain_FailsOnAnInvalidStoredLink()
    {
        var entity = new NoteEntity
        {
            Id = Guid.NewGuid(),
            Title = "T",
            Blocks = [new NoteBlockEntity { Id = Guid.NewGuid(), BlockType = BlockType.Link, LinkUrl = "not a url" }]
        };

        Assert.True(NoteMapper.ToDomain(entity).IsFailure);
    }

    [Fact]
    public void ToDomain_FailsOnAnUnknownBlockType()
    {
        var entity = new NoteEntity
        {
            Id = Guid.NewGuid(),
            Title = "T",
            Blocks = [new NoteBlockEntity { Id = Guid.NewGuid(), BlockType = (BlockType)99 }]
        };

        Assert.True(NoteMapper.ToDomain(entity).IsFailure);
    }

    [Fact]
    public void ToSummary_CarriesFlagsTagsAndTheGivenPreview()
    {
        var row = new NoteSummaryRow
        {
            Id = Guid.NewGuid(),
            Title = "Summary",
            IsEncrypted = false,
            HasText = true,
            HasFiles = false,
            HasLinks = true,
            Tags = [new TagEntity { Id = Guid.NewGuid(), Name = "work" }]
        };

        var summary = NoteMapper.ToSummary(row, "preview…").Unwrap();

        Assert.Equal("Summary", summary.Title.Value);
        Assert.Equal("preview…", summary.Preview);
        Assert.True(summary.HasText);
        Assert.False(summary.HasFiles);
        Assert.True(summary.HasLinks);
        Assert.False(summary.HasChecklists);
        Assert.Equal("work", Assert.Single(summary.Tags).Name.Value);
    }

    [Fact]
    public void ToSummary_FailsOnABlankTitle()
    {
        var row = new NoteSummaryRow { Id = Guid.NewGuid(), Title = "   " };

        Assert.True(NoteMapper.ToSummary(row, "").IsFailure);
    }

    // The trash view is the only place that gets rows with DeletedAt set; it must
    // survive the mapping so the list can tell a trashed row from a live one.
    [Fact]
    public void ToSummary_CarriesDeletedAt()
    {
        var deletedAt = new DateTime(2026, 9, 9, 10, 0, 0, DateTimeKind.Utc);
        var live = new NoteSummaryRow { Id = Guid.NewGuid(), Title = "Live" };
        var trashed = new NoteSummaryRow { Id = Guid.NewGuid(), Title = "Trashed", DeletedAt = deletedAt };

        Assert.False(NoteMapper.ToSummary(live, "").Unwrap().IsDeleted);

        var summary = NoteMapper.ToSummary(trashed, "").Unwrap();
        Assert.True(summary.IsDeleted);
        Assert.Equal(deletedAt, summary.DeletedAt);
    }
}
