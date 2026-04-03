namespace NoteApp.Domain.Functional;

public abstract record Option<T>
{
    public sealed record Some(T Value) : Option<T>;
    public sealed record None : Option<T>;

    public static Option<T> Of(T? value) =>
        value is not null ? new Some(value) : new None();

    public static Option<T> Empty() => new None();

    public TResult Match<TResult>(Func<T, TResult> some, Func<TResult> none) =>
        this switch
        {
            Some s => some(s.Value),
            _ => none()
        };

    public void Match(Action<T> some, Action none)
    {
        switch (this)
        {
            case Some s:
                some(s.Value);
                break;
            default:
                none();
                break;
        }
    }

    public Option<TResult> Map<TResult>(Func<T, TResult> map) =>
        this switch
        {
            Some s => new Option<TResult>.Some(map(s.Value)),
            _ => new Option<TResult>.None()
        };

    public Option<TResult> Bind<TResult>(Func<T, Option<TResult>> bind) =>
        this switch
        {
            Some s => bind(s.Value),
            _ => new Option<TResult>.None()
        };

    public T GetValueOrDefault(T defaultValue) =>
        this switch
        {
            Some s => s.Value,
            _ => defaultValue
        };

    public T GetValueOrDefault(Func<T> defaultFactory) =>
        this switch
        {
            Some s => s.Value,
            _ => defaultFactory()
        };

    public bool IsSome => this is Some;
    public bool IsNone => this is None;
}
