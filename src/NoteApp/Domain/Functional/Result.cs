using System.Diagnostics.CodeAnalysis;

namespace NoteApp.Domain.Functional;

public abstract record Result<T, TError>
{
    public sealed record Success(T Value) : Result<T, TError>;
    public sealed record Failure(TError Error) : Result<T, TError>;

    public static Result<T, TError> Ok(T value) => new Success(value);
    public static Result<T, TError> Fail(TError error) => new Failure(error);

    public TResult Match<TResult>(Func<T, TResult> success, Func<TError, TResult> failure) =>
        this switch
        {
            Success s => success(s.Value),
            Failure f => failure(f.Error),
            _ => throw new InvalidOperationException("Unexpected result state")
        };

    public void Match(Action<T> success, Action<TError> failure)
    {
        switch (this)
        {
            case Success s:
                success(s.Value);
                break;
            case Failure f:
                failure(f.Error);
                break;
        }
    }

    public Result<TResult, TError> Map<TResult>(Func<T, TResult> map) =>
        this switch
        {
            Success s => new Result<TResult, TError>.Success(map(s.Value)),
            Failure f => new Result<TResult, TError>.Failure(f.Error),
            _ => throw new InvalidOperationException("Unexpected result state")
        };

    public Result<TResult, TError> Bind<TResult>(Func<T, Result<TResult, TError>> bind) =>
        this switch
        {
            Success s => bind(s.Value),
            Failure f => new Result<TResult, TError>.Failure(f.Error),
            _ => throw new InvalidOperationException("Unexpected result state")
        };

    public Result<T, TNewError> MapError<TNewError>(Func<TError, TNewError> mapError) =>
        this switch
        {
            Success s => new Result<T, TNewError>.Success(s.Value),
            Failure f => new Result<T, TNewError>.Failure(mapError(f.Error)),
            _ => throw new InvalidOperationException("Unexpected result state")
        };

    // Imperative escape hatch for early returns, so callers never have to cast
    // to Success/Failure: `if (!r.TryGet(out var v, out var e)) return Fail(e);`
    public bool TryGet([MaybeNullWhen(false)] out T value, [MaybeNullWhen(true)] out TError error)
    {
        switch (this)
        {
            case Success s:
                value = s.Value;
                error = default!;
                return true;
            case Failure f:
                value = default!;
                error = f.Error;
                return false;
            default:
                throw new InvalidOperationException("Unexpected result state");
        }
    }

    public bool IsSuccess => this is Success;
    public bool IsFailure => this is Failure;
}

public record AppError(string Code, string Message)
{
    public static AppError Validation(string message) => new("VALIDATION", message);
    public static AppError NotFound(string message) => new("NOT_FOUND", message);
    public static AppError Database(string message) => new("DATABASE", message);
    public static AppError Io(string message) => new("IO", message);
}
