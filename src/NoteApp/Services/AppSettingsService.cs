using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NoteApp.Services;

public sealed class AppSettingsService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public string SettingsFilePath { get; }
    public AppSettings Current { get; private set; }

    public AppSettingsService() : this(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "NoteApp",
        "settings.json"))
    {
    }

    // Tests point this at a temporary file; the app uses the parameterless overload.
    public AppSettingsService(string settingsFilePath)
    {
        SettingsFilePath = settingsFilePath;
        Current = Load();
    }

    public void Save(AppSettings settings)
    {
        var directory = Path.GetDirectoryName(SettingsFilePath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllText(SettingsFilePath, JsonSerializer.Serialize(PersistedSettings.From(settings), SerializerOptions));
        Current = settings;
    }

    private AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsFilePath))
                return AppSettings.Default;

            var persisted = JsonSerializer.Deserialize<PersistedSettings>(File.ReadAllText(SettingsFilePath), SerializerOptions);
            return persisted?.ToSettings() ?? AppSettings.Default;
        }
        catch
        {
            return AppSettings.Default;
        }
    }

    // On-disk shape. The backup password is protected with DPAPI (current user)
    // rather than written in clear; the legacy `BackupPassword` field is still
    // read so existing files keep working and get upgraded on the next save.
    private sealed record PersistedSettings(
        bool ConfirmNoteDeletion,
        bool ConfirmTagDeletion,
        StartupPage LaunchPage,
        bool IsDarkMode,
        string? BackupFolderPath,
        string? BackupPasswordProtected,
        string? BackupPassword)
    {
        public static PersistedSettings From(AppSettings settings) => new(
            settings.ConfirmNoteDeletion,
            settings.ConfirmTagDeletion,
            settings.LaunchPage,
            settings.IsDarkMode,
            settings.BackupFolderPath,
            Protect(settings.BackupPassword),
            BackupPassword: null);

        public AppSettings ToSettings() => new(
            ConfirmNoteDeletion,
            ConfirmTagDeletion,
            LaunchPage,
            IsDarkMode,
            Unprotect(BackupPasswordProtected) ?? BackupPassword ?? string.Empty,
            string.IsNullOrWhiteSpace(BackupFolderPath) ? AppSettings.DefaultBackupFolderPath : BackupFolderPath);

        private static string? Protect(string value) =>
            string.IsNullOrEmpty(value)
                ? null
                : Convert.ToBase64String(ProtectedData.Protect(
                    Encoding.UTF8.GetBytes(value), optionalEntropy: null, DataProtectionScope.CurrentUser));

        private static string? Unprotect(string? value)
        {
            if (string.IsNullOrEmpty(value))
                return null;

            try
            {
                return Encoding.UTF8.GetString(ProtectedData.Unprotect(
                    Convert.FromBase64String(value), optionalEntropy: null, DataProtectionScope.CurrentUser));
            }
            catch (Exception ex) when (ex is CryptographicException or FormatException)
            {
                // Another Windows account or a tampered file: the password has to be re-entered.
                return null;
            }
        }
    }
}
