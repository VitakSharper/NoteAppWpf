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
    int LockEncryptedNotesAfterMinutes)
{
    // Computed, not stored: AppPaths.DataFolder is only known once the configuration is read.
    public static string DefaultBackupFolderPath => AppPaths.DefaultBackupFolder;

    public static AppSettings Default => new(
        ConfirmNoteDeletion: true,
        ConfirmTagDeletion: true,
        LaunchPage: StartupPage.Notes,
        IsDarkMode: false,
        BackupPassword: "",
        BackupFolderPath: DefaultBackupFolderPath,
        LockEncryptedNotesAfterMinutes: 5);
}
