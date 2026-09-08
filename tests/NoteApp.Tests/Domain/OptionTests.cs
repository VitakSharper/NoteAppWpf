using NoteApp.Domain.Extensions;
using NoteApp.Domain.Functional;

namespace NoteApp.Tests.Domain;

public class OptionTests
{
    [Fact]
    public void Of_WrapsNullAsNone_AndValueAsSome()
    {
        Assert.True(Option<string>.Of(null).IsNone);
        Assert.True(Option<string>.Of("x").IsSome);
    }

    [Fact]
    public void Map_And_Bind_SkipNone()
    {
        var none = Option<int>.Empty();

        Assert.True(none.Map(v => v + 1).IsNone);
        Assert.True(none.Bind(v => Option<int>.Of(v + 1)).IsNone);
        Assert.Equal(3, Option<int>.Of(2).Map(v => v + 1).GetValueOrDefault(0));
    }

    [Fact]
    public void GetValueOrDefault_UsesTheFallbackForNone()
    {
        Assert.Equal("fallback", Option<string>.Empty().GetValueOrDefault("fallback"));
        Assert.Equal("lazy", Option<string>.Empty().GetValueOrDefault(() => "lazy"));
        Assert.Equal("some", Option<string>.Of("some").GetValueOrDefault("fallback"));
    }

    [Fact]
    public void Where_FiltersSomeIntoNone()
    {
        Assert.True(Option<int>.Of(5).Where(v => v > 10).IsNone);
        Assert.True(Option<int>.Of(50).Where(v => v > 10).IsSome);
    }

    [Fact]
    public void ToResult_MapsNoneToTheProvidedError()
    {
        var result = Option<int>.Empty().ToResult(() => AppError.NotFound("nothing"));

        Assert.Equal("nothing", result.Match(_ => "", e => e.Message));
        Assert.Equal(7, Option<int>.Of(7).ToResult(() => AppError.NotFound("x")).Unwrap());
    }

    [Fact]
    public void When_WrapsOnlyWhenThePredicateHolds()
    {
        Assert.True("abc".When(s => s.Length == 3).IsSome);
        Assert.True("abc".When(s => s.Length == 4).IsNone);
    }
}
