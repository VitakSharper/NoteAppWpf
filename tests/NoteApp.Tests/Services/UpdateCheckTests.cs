using NoteApp.Domain.Functional;
using NoteApp.Services;

namespace NoteApp.Tests.Services;

public class UpdateCheckTests
{
    [Theory]
    [InlineData("v1.2.3", "1.2.3")]
    [InlineData("1.2", "1.2.0")]
    [InlineData(" V2.0.1 ", "2.0.1")]
    public void A_release_tag_reads_as_a_version(string tag, string version) =>
        Assert.Equal(Version.Parse(version), UpdateCheck.VersionOf(tag));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("latest")]
    [InlineData("v1.x")]
    public void Anything_else_is_no_version(string? tag) => Assert.Null(UpdateCheck.VersionOf(tag));

    [Fact]
    public void Only_a_higher_version_is_newer_revision_numbers_aside()
    {
        Assert.True(UpdateCheck.IsNewer(new Version(1, 2, 0), new Version(1, 1, 9)));
        Assert.False(UpdateCheck.IsNewer(new Version(1, 1, 0), new Version(1, 1, 0, 0)));
        Assert.False(UpdateCheck.IsNewer(new Version(1, 0, 5), new Version(1, 1, 0)));
    }

    [Fact]
    public void GitHub_is_asked_at_most_once_a_day()
    {
        var now = new DateTime(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc);

        Assert.True(UpdateCheck.IsDue(null, now));
        Assert.False(UpdateCheck.IsDue(now.AddHours(-23), now));
        Assert.True(UpdateCheck.IsDue(now.AddHours(-24), now));
    }

    [Fact]
    public void The_latest_release_is_read_from_the_github_answer()
    {
        var release = UpdateCheck.Parse("""
            { "tag_name": "v1.3.0", "html_url": "https://github.com/VitakSharper/NoteAppWpf/releases/tag/v1.3.0",
              "draft": false, "prerelease": false, "assets": [] }
            """);

        var latest = Assert.IsType<Option<LatestRelease>.Some>(release).Value;
        Assert.Equal(new Version(1, 3, 0), latest.Version);
        Assert.Equal("https://github.com/VitakSharper/NoteAppWpf/releases/tag/v1.3.0", latest.Page.AbsoluteUri);
    }

    [Theory]
    [InlineData("""{ "tag_name": "v9.0.0", "html_url": "https://github.com/x", "draft": true }""")]
    [InlineData("""{ "tag_name": "v9.0.0", "html_url": "https://github.com/x", "prerelease": true }""")]
    [InlineData("""{ "tag_name": "nightly", "html_url": "https://github.com/x" }""")]
    [InlineData("""{ "tag_name": "v9.0.0", "html_url": "http://github.com/x" }""")]
    [InlineData("""{ "message": "Not Found" }""")]
    [InlineData("not json")]
    public void Drafts_prereleases_and_anything_odd_are_no_release(string json) =>
        Assert.True(UpdateCheck.Parse(json).IsNone);
}
