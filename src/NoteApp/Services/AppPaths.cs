using System.IO;

namespace NoteApp.Services;

// Where NoteApp keeps its own files: settings.json, crash.log, unsaved drafts and the
// default backup folder. %LocalAppData%\NoteApp, unless the DataFolder setting (the
// NOTEAPP_DataFolder variable or --DataFolder=) moves it: a test instance pointed at a
// scratch database must not read or overwrite the real instance's files.
public static class AppPaths
{
    public static string DefaultDataFolder { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NoteApp");

    public static string DataFolder { get; private set; } = DefaultDataFolder;

    public static string SettingsFile => Path.Combine(DataFolder, "settings.json");
    public static string CrashLog => Path.Combine(DataFolder, "crash.log");
    public static string DraftsFolder => Path.Combine(DataFolder, "Drafts");
    public static string DefaultBackupFolder => Path.Combine(DataFolder, "Backups");

    // Once, at startup, before anything reads the paths above.
    public static void UseDataFolder(string? folder)
    {
        if (!string.IsNullOrWhiteSpace(folder))
            DataFolder = Path.GetFullPath(folder);
    }
}
