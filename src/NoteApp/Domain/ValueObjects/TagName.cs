using NoteApp.Domain.Functional;

namespace NoteApp.Domain.ValueObjects;

public sealed record TagName
{
    public string Value { get; }

    private TagName(string value) => Value = value;

    public static Result<TagName, AppError> From(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? Result<TagName, AppError>.Fail(AppError.Validation("Tag name cannot be empty."))
            : value.Length > 100
                ? Result<TagName, AppError>.Fail(AppError.Validation("Tag name cannot exceed 100 characters."))
                : Result<TagName, AppError>.Ok(new TagName(value.Trim().ToLowerInvariant()));

    public override string ToString() => Value;
}
