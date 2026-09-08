using NoteApp.Domain.Extensions;
using NoteApp.Domain.Functional;

namespace NoteApp.Tests.Domain;

public class ResultTests
{
    [Fact]
    public void Ok_IsSuccess_AndMatchesTheSuccessBranch()
    {
        var result = Result<int, AppError>.Ok(42);

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal(42, result.Match(v => v, _ => -1));
    }

    [Fact]
    public void Fail_IsFailure_AndMatchesTheFailureBranch()
    {
        var result = Result<int, AppError>.Fail(AppError.Validation("nope"));

        Assert.True(result.IsFailure);
        Assert.Equal("VALIDATION", result.Match(_ => "", e => e.Code));
    }

    [Fact]
    public void Map_TransformsSuccess_AndPropagatesFailureUntouched()
    {
        var mapped = Result<int, AppError>.Ok(42).Map(v => v.ToString());
        Assert.Equal("42", mapped.Unwrap());

        var error = AppError.NotFound("missing");
        var failed = Result<int, AppError>.Fail(error).Map(v => v.ToString());
        Assert.Same(error, failed.Match<AppError?>(_ => null, e => e));
    }

    [Fact]
    public void Bind_Chains_AndShortCircuitsWithoutCallingTheContinuation()
    {
        static Result<int, AppError> Positive(int v) =>
            v > 0 ? Result<int, AppError>.Ok(v) : Result<int, AppError>.Fail(AppError.Validation("negative"));

        Assert.True(Result<int, AppError>.Ok(1).Bind(Positive).IsSuccess);
        Assert.True(Result<int, AppError>.Ok(-1).Bind(Positive).IsFailure);

        var calls = 0;
        Result<int, AppError>.Fail(AppError.Validation("x")).Bind(v => { calls++; return Positive(v); });
        Assert.Equal(0, calls);
    }

    [Fact]
    public void TryGet_YieldsTheValueOnSuccess()
    {
        var ok = Result<string, AppError>.Ok("value").TryGet(out var value, out var error);

        Assert.True(ok);
        Assert.Equal("value", value);
        Assert.Null(error);
    }

    [Fact]
    public void TryGet_YieldsTheErrorOnFailure()
    {
        var ok = Result<string, AppError>.Fail(AppError.Io("boom")).TryGet(out var value, out var error);

        Assert.False(ok);
        Assert.Null(value);
        Assert.Equal("boom", error?.Message);
    }

    [Fact]
    public void MapError_ChangesTheErrorType()
    {
        var mapped = Result<int, AppError>.Fail(AppError.Database("db")).MapError(e => e.Code);

        Assert.Equal("DATABASE", mapped.Match(_ => "", e => e));
    }

    [Fact]
    public void Tap_RunsTheSideEffectOnlyOnSuccess()
    {
        var seen = new List<int>();

        Result<int, AppError>.Ok(1).Tap(seen.Add);
        Result<int, AppError>.Fail(AppError.Io("x")).Tap(seen.Add);

        Assert.Equal([1], seen);
    }

    [Fact]
    public async Task BindAsync_OnTask_ComposesLikeBind()
    {
        var result = await Task.FromResult(Result<int, AppError>.Ok(2))
            .BindAsync(v => Result<int, AppError>.Ok(v * 10));

        Assert.Equal(20, result.Unwrap());
    }
}
