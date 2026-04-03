using NoteApp.Domain.Functional;

namespace NoteApp.Domain.ValueObjects;

public sealed record NoteTitle
{
    public string Value { get; }

    private NoteTitle(string value) => Value = value;

    public static Result<NoteTitle, AppError> From(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? Result<NoteTitle, AppError>.Fail(AppError.Validation("Note title cannot be empty."))
            : value.Length > 500
                ? Result<NoteTitle, AppError>.Fail(AppError.Validation("Note title cannot exceed 500 characters."))
                : Result<NoteTitle, AppError>.Ok(new NoteTitle(value.Trim()));

    public override string ToString() => Value;
}
