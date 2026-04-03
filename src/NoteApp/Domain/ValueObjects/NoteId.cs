using NoteApp.Domain.Functional;

namespace NoteApp.Domain.ValueObjects;

public readonly record struct NoteId(Guid Value)
{
    public static NoteId New() => new(Guid.NewGuid());

    public static Result<NoteId, AppError> From(Guid value) =>
        value == Guid.Empty
            ? Result<NoteId, AppError>.Fail(AppError.Validation("Note ID cannot be empty."))
            : Result<NoteId, AppError>.Ok(new NoteId(value));

    public override string ToString() => Value.ToString();
}
