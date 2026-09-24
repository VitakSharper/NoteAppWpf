using System.Windows.Data;
using NoteApp.Domain.Models;
using NoteApp.Domain.ValueObjects;
using NoteApp.ViewModels;

namespace NoteApp.Tests.ViewModels;

public class NoteGroupsTests
{
    private static readonly DateTime Now = new(2026, 9, 24, 15, 0, 0, DateTimeKind.Local);

    [Theory]
    [InlineData(0, "Today")]
    [InlineData(1, "Yesterday")]
    [InlineData(6, "Previous 7 days")]
    [InlineData(7, "Previous 30 days")]
    [InlineData(29, "Previous 30 days")]
    [InlineData(40, "August 2026")]
    [InlineData(400, "August 2025")]
    public void Dates_fall_under_relative_headings_then_months(int daysAgo, string expected) =>
        Assert.Equal(expected, NoteGroups.Label(Summary(updatedLocal: Now.AddDays(-daysAgo)), SortOption.UpdatedDesc, Now));

    // Stored dates are UTC: a note written at 00:30 local time is from today, not yesterday.
    [Fact]
    public void Days_are_the_users_not_utcs()
    {
        var justAfterMidnight = new DateTime(2026, 9, 24, 0, 30, 0, DateTimeKind.Local);
        var stored = DateTime.SpecifyKind(justAfterMidnight.ToUniversalTime(), DateTimeKind.Unspecified);

        Assert.Equal("Today", NoteGroups.Label(Summary(updatedStored: stored), SortOption.UpdatedDesc, Now));
    }

    [Fact]
    public void Pinned_notes_have_their_own_heading() =>
        Assert.Equal(NoteGroups.Pinned, NoteGroups.Label(Summary(updatedLocal: Now) with { IsPinned = true }, SortOption.UpdatedDesc, Now));

    [Fact]
    public void The_created_sort_groups_by_creation()
    {
        var note = Summary(updatedLocal: Now) with { CreatedAt = Now.AddDays(-1).ToUniversalTime() };

        Assert.Equal("Yesterday", NoteGroups.Label(note, SortOption.CreatedDesc, Now));
    }

    [Fact]
    public void A_title_sort_has_no_headings()
    {
        Assert.Null(NoteGroups.Label(Summary(updatedLocal: Now), SortOption.TitleAsc, Now));
        Assert.False(NoteGroups.IsGrouped(SortOption.TitleAsc));
        Assert.True(NoteGroups.IsGrouped(SortOption.CreatedAsc));
    }

    private static NoteSummary Summary(DateTime? updatedLocal = null, DateTime? updatedStored = null)
    {
        var updated = updatedStored ?? DateTime.SpecifyKind(updatedLocal!.Value.ToUniversalTime(), DateTimeKind.Unspecified);
        return new NoteSummary(NoteId.New(), Title("n"), "", [], false, true, false, false, false, updated, updated);
    }
}
