using NoteApp.Domain.ValueObjects;
using NoteApp.ViewModels;

namespace NoteApp.Tests.ViewModels;

public class NavigationTests
{
    private static readonly NoteId A = NoteId.New(), B = NoteId.New(), C = NoteId.New();

    [Fact]
    public void Back_and_forward_walk_the_notes_opened()
    {
        var history = new NavigationHistory();
        history.Visit(A);
        history.Visit(B);
        history.Visit(C);

        Assert.Equal(B, history.Back());
        Assert.Equal(A, history.Back());
        Assert.Null(history.Back());
        Assert.Equal(B, history.Forward());
        Assert.True(history.CanGoForward);
    }

    // As in a browser: going somewhere new from the middle drops what was forward.
    [Fact]
    public void Opening_a_note_from_the_middle_drops_the_forward_part()
    {
        var history = new NavigationHistory();
        history.Visit(A);
        history.Visit(B);
        history.Back();

        history.Visit(C);

        Assert.False(history.CanGoForward);
        Assert.Equal(A, history.Back());
    }

    [Fact]
    public void Opening_the_note_already_current_is_not_a_new_visit()
    {
        var history = new NavigationHistory();
        history.Visit(A);
        history.Visit(A);

        Assert.False(history.CanGoBack);
    }

    [Fact]
    public void A_deleted_note_leaves_the_history_and_its_neighbours_merge()
    {
        var history = new NavigationHistory();
        history.Visit(A);
        history.Visit(B);
        history.Visit(A);
        history.Visit(C);

        history.Forget(B);

        Assert.Equal(C, history.Current);
        Assert.Equal(A, history.Back());
        Assert.False(history.CanGoBack);
    }

    [Fact]
    public void Forgetting_the_current_note_makes_the_one_before_it_current()
    {
        var history = new NavigationHistory();
        history.Visit(A);
        history.Visit(B);

        history.Forget(B);

        Assert.Equal(A, history.Current);
        history.Forget(A);
        Assert.Null(history.Current);
    }

    [Fact]
    public void The_history_keeps_its_last_fifty_notes()
    {
        var history = new NavigationHistory();
        var first = NoteId.New();
        history.Visit(first);
        for (var i = 0; i < NavigationHistory.Capacity; i++)
            history.Visit(NoteId.New());

        var back = 0;
        while (history.Back() is not null)
            back++;
        Assert.Equal(NavigationHistory.Capacity - 1, back);
        Assert.NotEqual(first, history.Current);
    }

    [Theory]
    [InlineData("wee", "Weekly review")]
    [InlineData("rev", "Weekly review")]
    [InlineData("wkr", "Weekly review")]
    [InlineData("weekly rev", "Weekly review")]
    public void The_switcher_finds_a_title_by_its_start_its_words_or_its_letters(string typed, string expected) =>
        Assert.Equal(expected, QuickSwitch.Rank(typed, ["Groceries", "Weekly review", "Router passwords"], s => s).First());

    [Fact]
    public void The_start_of_a_title_beats_a_word_inside_one_which_beats_scattered_letters()
    {
        var ranked = QuickSwitch.Rank("pa", ["Space plan", "Party", "Wifi passwords", "Map app"], s => s);

        Assert.Equal(["Party", "Wifi passwords", "Space plan", "Map app"], ranked);
    }

    [Fact]
    public void Nothing_typed_keeps_the_order_given_and_a_miss_gives_nothing()
    {
        Assert.Equal(["b", "a"], QuickSwitch.Rank(" ", ["b", "a"], s => s));
        Assert.Empty(QuickSwitch.Rank("zzz", ["b", "a"], s => s));
    }
}
