namespace NoteApp.Domain.Extensions;

using NoteApp.Domain.Functional;

public static class OptionExtensions
{
    public static Option<T> ToOption<T>(this T? value) =>
        Option<T>.Of(value);

    public static Option<T> When<T>(this T value, Func<T, bool> predicate) =>
        predicate(value) ? new Option<T>.Some(value) : Option<T>.Empty();

    public static Result<T, TError> ToResult<T, TError>(this Option<T> option, Func<TError> errorFactory) =>
        option.Match<Result<T, TError>>(
            some: value => Result<T, TError>.Ok(value),
            none: () => Result<T, TError>.Fail(errorFactory()));

    public static Option<T> Where<T>(this Option<T> option, Func<T, bool> predicate) =>
        option.Bind(value => predicate(value) ? new Option<T>.Some(value) : Option<T>.Empty());

    public static async Task<Option<TResult>> MapAsync<T, TResult>(
        this Option<T> option, Func<T, Task<TResult>> map) =>
        option switch
        {
            Option<T>.Some s => new Option<TResult>.Some(await map(s.Value)),
            _ => new Option<TResult>.None()
        };
}
