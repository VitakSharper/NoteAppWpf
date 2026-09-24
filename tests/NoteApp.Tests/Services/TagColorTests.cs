using NoteApp.Data.Entities;
using NoteApp.Data.Repositories;
using NoteApp.Domain.Functional;
using NoteApp.Domain.Models;
using NoteApp.Domain.ValueObjects;
using NoteApp.Services;
using NoteApp.Services.Mapping;
using NoteApp.Theme;
using NoteApp.ViewModels;

namespace NoteApp.Tests.Services;

public class TagColorTests
{
    [Fact]
    public void A_colour_survives_the_mapper() =>
        Assert.Equal(TagColor.Teal, TagMapper.ToDomain(TagMapper.ToEntity(new Tag(Guid.NewGuid(), Name("t"), TagColor.Teal))).Color);

    // Tags stored before colours existed have NULL; a hand-edited value may be anything.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Magenta")]
    [InlineData("42")]
    public void An_unknown_or_missing_colour_is_the_old_violet(string? stored) =>
        Assert.Equal(TagColor.Violet, TagMapper.ToDomain(new TagEntity { Id = Guid.NewGuid(), Name = "t", Color = stored }).Color);

    [Fact]
    public void Every_colour_has_a_swatch()
    {
        Assert.Equal(Enum.GetValues<TagColor>(), TagPalette.All.Select(s => s.Color));
        Assert.All(TagPalette.All, s => Assert.True(s.Background.IsFrozen));
    }

    [Fact]
    public async Task The_tag_editor_saves_the_picked_colour()
    {
        var tag = new Tag(Guid.NewGuid(), Name("work"));
        var repo = new RecordingTags(tag);
        var vm = new TagManagerViewModel(repo, new AppSettingsService(System.IO.Path.GetTempFileName()));
        await vm.LoadTags();

        vm.BeginEditCommand.Execute(tag);
        Assert.Equal(TagColor.Violet, vm.EditTagColor);
        vm.PickColorCommand.Execute(TagColor.Amber);
        await vm.SaveEditCommand.ExecuteAsync(null);

        Assert.Equal(TagColor.Amber, repo.Updated!.Color);
        Assert.Equal("work", repo.Updated.Name.Value);
        Assert.Equal(TagColor.Amber, Assert.Single(vm.Tags).Color);
    }

    private sealed class RecordingTags(Tag tag) : ITagRepository
    {
        public Tag? Updated { get; private set; }
        public Task<Result<IReadOnlyList<Tag>, AppError>> GetAllAsync() => Task.FromResult(Result<IReadOnlyList<Tag>, AppError>.Ok([tag]));
        public Task<Result<Tag, AppError>> UpdateAsync(Tag t) { Updated = t; return Task.FromResult(Result<Tag, AppError>.Ok(t)); }
        public Task<Result<Tag, AppError>> CreateAsync(Tag t) => throw new NotSupportedException();
        public Task<Result<Unit, AppError>> DeleteAsync(Guid id) => throw new NotSupportedException();
    }
}
