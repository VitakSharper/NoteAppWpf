namespace NoteApp.Services;

public enum StartupPage
{
    Notes,
    Tags,
    Settings
}

public sealed record AppSettings(
    bool ConfirmNoteDeletion,
    bool ConfirmTagDeletion,
    StartupPage LaunchPage,
    bool IsDarkMode,
    string BackupPassword,
    string BackupFolderPath,
    // Minutes of inactivity before an open encrypted note is closed again. 0 = never.
    int LockEncryptedNotesAfterMinutes,
    AutoBackupInterval AutoBackup = AutoBackupInterval.Off,
    // Backups kept when an automatic one runs; 0 = keep them all.
    int KeepBackups = 10,
    // Scale of the editor's blocks (Ctrl+wheel), 1 = 100 %.
    double EditorZoom = 1.0,
    // Close an open encrypted note at once when Windows locks or the computer sleeps.
    bool LockEncryptedNotesWhenWindowsLocks = true,
    // Earlier states a save keeps of each note (0 = none).
    int KeepVersions = 10,
    // Closing the window leaves NoteApp running in the notification area.
    bool CloseToTray = false,
    // Ctrl+Alt+N from any application opens a quick note (read at startup).
    bool QuickNoteHotKey = true,
    // The last time reminders were checked (UTC): the ones due since then fire at the next
    // check, so a reminder due while NoteApp was closed still comes.
    DateTime? LastReminderCheckUtc = null,
    // Look for a newer release on GitHub, at most once a day.
    bool CheckForUpdates = true,
    DateTime? LastUpdateCheckUtc = null)
{
    public const double MinEditorZoom = 0.5;
    public const double MaxEditorZoom = 2.5;

    // Computed, not stored: AppPaths.DataFolder is only known once the configuration is read.
    public static string DefaultBackupFolderPath => AppPaths.DefaultBackupFolder;

    public static AppSettings Default => new(
        ConfirmNoteDeletion: true,
        ConfirmTagDeletion: true,
        LaunchPage: StartupPage.Notes,
        IsDarkMode: false,
        BackupPassword: "",
        BackupFolderPath: DefaultBackupFolderPath,
        LockEncryptedNotesAfterMinutes: 5,
        AutoBackup: AutoBackupInterval.Off,
        KeepBackups: 10,
        EditorZoom: 1.0,
        LockEncryptedNotesWhenWindowsLocks: true,
        KeepVersions: 10,
        CloseToTray: false,
        QuickNoteHotKey: true);
}
