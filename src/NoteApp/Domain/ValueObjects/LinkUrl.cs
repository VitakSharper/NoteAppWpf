using NoteApp.Domain.Functional;

namespace NoteApp.Domain.ValueObjects;

public sealed record LinkUrl
{
    public Uri Value { get; }

    private LinkUrl(Uri value) => Value = value;

    public static Result<LinkUrl, AppError> From(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? Result<LinkUrl, AppError>.Fail(AppError.Validation("URL cannot be empty."))
            : Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
                ? Result<LinkUrl, AppError>.Ok(new LinkUrl(uri))
                : Result<LinkUrl, AppError>.Fail(AppError.Validation("URL must be a valid HTTP or HTTPS address."));

    public override string ToString() => Value.ToString();
}
