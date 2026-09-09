using NoteApp.Services;

namespace NoteApp.Tests.Services;

// The rule behind the automatic lock of encrypted notes. The clock is injected, so
// the five-minute behaviour is checked at the second rather than waited out.
public class IdleLockTests
{
    private static readonly DateTime Start = new(2026, 9, 9, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void An_unarmed_lock_never_expires()
    {
        var lock5 = new IdleLock { Timeout = TimeSpan.FromMinutes(5) };

        Assert.False(lock5.IsArmed);
        Assert.False(lock5.HasExpired(Start.AddHours(1)));
    }

    [Fact]
    public void Does_not_expire_before_the_timeout()
    {
        var lock5 = Armed(TimeSpan.FromMinutes(5));

        Assert.False(lock5.HasExpired(Start.AddMinutes(4).AddSeconds(59)));
    }

    [Fact]
    public void Expires_once_the_timeout_is_reached()
    {
        var lock5 = Armed(TimeSpan.FromMinutes(5));

        Assert.True(lock5.HasExpired(Start.AddMinutes(5)));
        Assert.True(lock5.HasExpired(Start.AddMinutes(30)));
    }

    // Typing in the note has to postpone the lock, or it would fire mid-sentence.
    [Fact]
    public void Activity_restarts_the_countdown()
    {
        var lock5 = Armed(TimeSpan.FromMinutes(5));

        lock5.NotifyActivity(Start.AddMinutes(4));

        Assert.False(lock5.HasExpired(Start.AddMinutes(8)));
        Assert.True(lock5.HasExpired(Start.AddMinutes(9)));
    }

    [Fact]
    public void Activity_on_an_unarmed_lock_does_not_arm_it()
    {
        var lock5 = new IdleLock { Timeout = TimeSpan.FromMinutes(5) };

        lock5.NotifyActivity(Start);

        Assert.False(lock5.IsArmed);
        Assert.False(lock5.HasExpired(Start.AddHours(1)));
    }

    // "Never" in Settings.
    [Fact]
    public void A_zero_timeout_disables_the_lock_even_when_armed()
    {
        var lockNever = Armed(TimeSpan.Zero);

        Assert.True(lockNever.IsArmed);
        Assert.False(lockNever.IsEnabled);
        Assert.False(lockNever.HasExpired(Start.AddDays(1)));
    }

    // Switching the setting back on must not need the note to be reopened.
    [Fact]
    public void Raising_the_timeout_from_never_takes_effect_on_the_armed_lock()
    {
        var idle = Armed(TimeSpan.Zero);

        idle.Timeout = TimeSpan.FromMinutes(5);

        Assert.True(idle.HasExpired(Start.AddMinutes(5)));
    }

    [Fact]
    public void Disarming_stops_it_expiring()
    {
        var lock5 = Armed(TimeSpan.FromMinutes(5));

        lock5.Disarm();

        Assert.False(lock5.HasExpired(Start.AddMinutes(10)));
    }

    // Opening another encrypted note starts its own five minutes.
    [Fact]
    public void Arming_again_restarts_the_countdown()
    {
        var lock5 = Armed(TimeSpan.FromMinutes(5));

        lock5.Arm(Start.AddMinutes(4));

        Assert.False(lock5.HasExpired(Start.AddMinutes(8)));
        Assert.True(lock5.HasExpired(Start.AddMinutes(9)));
    }

    private static IdleLock Armed(TimeSpan timeout)
    {
        var idle = new IdleLock { Timeout = timeout };
        idle.Arm(Start);
        return idle;
    }
}
