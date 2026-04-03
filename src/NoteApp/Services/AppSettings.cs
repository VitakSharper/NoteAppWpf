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
    string BackupFolderPath)
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
        BackupFolderPath: DefaultBackupFolderPath);
}
