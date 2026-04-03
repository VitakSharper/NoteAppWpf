namespace NoteApp.Domain.Extensions;

using NoteApp.Domain.Functional;

public static class ResultExtensions
{
    public static async Task<Result<TResult, TError>> MapAsync<T, TResult, TError>(
        this Result<T, TError> result, Func<T, Task<TResult>> map) =>
        result switch
        {
            Result<T, TError>.Success s => Result<TResult, TError>.Ok(await map(s.Value)),
            Result<T, TError>.Failure f => Result<TResult, TError>.Fail(f.Error),
            _ => throw new InvalidOperationException()
        };

    public static async Task<Result<TResult, TError>> BindAsync<T, TResult, TError>(
        this Result<T, TError> result, Func<T, Task<Result<TResult, TError>>> bind) =>
        result switch
        {
            Result<T, TError>.Success s => await bind(s.Value),
            Result<T, TError>.Failure f => Result<TResult, TError>.Fail(f.Error),
            _ => throw new InvalidOperationException()
        };

    public static async Task<Result<TResult, TError>> MapAsync<T, TResult, TError>(
        this Task<Result<T, TError>> resultTask, Func<T, TResult> map) =>
        (await resultTask).Map(map);

    public static async Task<Result<TResult, TError>> BindAsync<T, TResult, TError>(
        this Task<Result<T, TError>> resultTask, Func<T, Result<TResult, TError>> bind) =>
        (await resultTask) switch
        {
            Result<T, TError>.Success s => bind(s.Value),
            Result<T, TError>.Failure f => Result<TResult, TError>.Fail(f.Error),
            _ => throw new InvalidOperationException()
        };

    public static Result<T, TError> Tap<T, TError>(
        this Result<T, TError> result, Action<T> action)
    {
        if (result is Result<T, TError>.Success s) action(s.Value);
        return result;
    }

    public static Result<Unit, TError> ToUnit<T, TError>(this Result<T, TError> result) =>
        result.Map(_ => Unit.Value);
}
