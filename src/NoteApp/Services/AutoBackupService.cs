using NoteApp.Domain.Functional;

namespace NoteApp.Services;

public abstract record AutoBackupOutcome
{
    public sealed record NotDue : AutoBackupOutcome;
    // Due, but there is no backup password to protect the archive with.
    public sealed record NoPassword : AutoBackupOutcome;
    public sealed record Saved(string ZipPath, int Pruned) : AutoBackupOutcome;
    public sealed record Failed(AppError Error) : AutoBackupOutcome;
}

// The automatic backup: checked at startup and then every hour by MainViewModel, so a
// daily backup also happens for an app that stays open for days. Uses the same folder,
// password and archive as "Backup Now".
public sealed class AutoBackupService(AppSettingsService settingsService, BackupService backupService)
{
    private bool _isRunning;

    public async Task<AutoBackupOutcome> RunIfDueAsync(DateTime now)
    {
        var settings = settingsService.Current;
        if (_isRunning || !BackupSchedule.IsDue(settings.AutoBackup, BackupService.ExistingBackups(settings.BackupFolderPath), now))
            return new AutoBackupOutcome.NotDue();

        if (string.IsNullOrWhiteSpace(settings.BackupPassword))
            return new AutoBackupOutcome.NoPassword();

        _isRunning = true;
        try
        {
            var result = await backupService.ExportAsync(settings.BackupPassword, settings.BackupFolderPath);
            return result.Match<AutoBackupOutcome>(
                success: zip => new AutoBackupOutcome.Saved(zip, BackupService.Prune(settings.BackupFolderPath, settings.KeepBackups)),
                failure: error => new AutoBackupOutcome.Failed(error));
        }
        finally
        {
            _isRunning = false;
        }
    }
}
