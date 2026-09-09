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
    public static string DefaultBackupFolderPath { get; } = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "NoteApp",
        "Backups");

    public static AppSettings Default { get; } = new(
        ConfirmNoteDeletion: true,
        ConfirmTagDeletion: true,
        LaunchPage: StartupPage.Notes,
        IsDarkMode: false,
        BackupPassword: "",
        BackupFolderPath: DefaultBackupFolderPath,
        LockEncryptedNotesAfterMinutes: 5);
}
