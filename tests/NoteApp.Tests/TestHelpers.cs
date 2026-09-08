using NoteApp.Domain.Functional;
using NoteApp.Domain.Models;
using NoteApp.Domain.ValueObjects;
using Xunit.Sdk;

namespace NoteApp.Tests;

internal static class TestHelpers
{
    // Fails the test with the domain error instead of hiding it behind a cast.
    public static T Unwrap<T>(this Result<T, AppError> result) =>
        result.Match(value => value, error => throw new XunitException($"{error.Code}: {error.Message}"));

    public static NoteTitle Title(string value) => NoteTitle.From(value).Unwrap();
    public static TagName Name(string value) => TagName.From(value).Unwrap();
    public static LinkUrl Url(string value) => LinkUrl.From(value).Unwrap();

    public static Note SampleNote(params NoteBlock[] blocks) =>
        Note.Create(Title("Sample"), blocks, [Tag.Create(Name("work"))]).Unwrap();

    public static NoteBlock[] SampleBlocks() =>
    [
        new NoteBlock.Text("<rich/>", "plain text") { SortOrder = 0 },
        new NoteBlock.File([1, 2, 3], "a.bin", ".bin", 3) { SortOrder = 1 },
        new NoteBlock.Link(Url("https://example.com/page"), "example") { SortOrder = 2 }
    ];
}
