namespace NoteApp.Services;

// Idle countdown for the decrypted note on screen. The clock is a parameter rather
// than DateTime.UtcNow inside, so the rule can be tested without waiting minutes;
// MainViewModel is the one that reads the real clock.
public sealed class IdleLock
{
    private DateTime _lastActivity;

    // Zero (the "Never" setting) disables the lock without disarming it, so turning
    // the setting back on while a note is open takes effect immediately.
    public TimeSpan Timeout { get; set; }

    public bool IsEnabled => Timeout > TimeSpan.Zero;

    // Armed means: an encrypted note is open and its content is on screen.
    public bool IsArmed { get; private set; }

    public void Arm(DateTime now)
    {
        IsArmed = true;
        _lastActivity = now;
    }

    public void Disarm() => IsArmed = false;

    public void NotifyActivity(DateTime now)
    {
        if (IsArmed)
            _lastActivity = now;
    }

    public bool HasExpired(DateTime now) =>
        IsArmed && IsEnabled && now - _lastActivity >= Timeout;
}
