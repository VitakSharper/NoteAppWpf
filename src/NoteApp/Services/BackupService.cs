using System.IO;
using ICSharpCode.SharpZipLib.Zip;
using Microsoft.SqlServer.Dac;
using NoteApp.Domain.Functional;

namespace NoteApp.Services;

public sealed class BackupService(string connectionString, string databaseName)
{
    public async Task<Result<string, AppError>> ExportAsync(string password, string backupFolderPath)
    {
        var folderName = BackupSchedule.FolderName(DateTime.Now);
        var folderPath = Path.Combine(ResolveFolder(backupFolderPath), folderName);

        try
        {

            Directory.CreateDirectory(folderPath);

            var bacpacFileName = $"{folderName}.bacpac";
            var bacpacPath = Path.Combine(folderPath, bacpacFileName);
            var zipPath = Path.Combine(folderPath, $"{folderName}.zip");

            // Export BACPAC using DacFx (CPU-bound, offload to thread pool)
            await Task.Run(() =>
            {
                var dacServices = new DacServices(connectionString);
                dacServices.ExportBacpac(bacpacPath, databaseName);
            });

            // Create password-protected zip
            await Task.Run(() =>
            {
                using var zipStream = new ZipOutputStream(File.Create(zipPath));
                zipStream.Password = password;
                zipStream.SetLevel(9); // Max compression

                var entry = new ZipEntry(bacpacFileName)
                {
                    AESKeySize = 256
                };
                zipStream.PutNextEntry(entry);

                using var fileStream = File.OpenRead(bacpacPath);
                fileStream.CopyTo(zipStream);

                zipStream.CloseEntry();
            });

            // Clean up intermediate BACPAC
            File.Delete(bacpacPath);

            return Result<string, AppError>.Ok(zipPath);
        }
        catch (DacServicesException ex)
        {
            TryCleanupFolder(folderPath);
            return Result<string, AppError>.Fail(
                AppError.Database($"BACPAC export failed: {ex.Message}"));
        }
        catch (IOException ex)
        {
            TryCleanupFolder(folderPath);
            return Result<string, AppError>.Fail(
                AppError.Io($"File operation failed: {ex.Message}"));
        }
        catch (Exception ex)
        {
            TryCleanupFolder(folderPath);
            return Result<string, AppError>.Fail(AppError.Io(Describe(ex)));
        }
    }

    // Names of the backup folders already there (BackupSchedule decides which are ours).
    public static IReadOnlyList<string> ExistingBackups(string backupFolderPath)
    {
        var folder = ResolveFolder(backupFolderPath);
        try
        {
            return Directory.Exists(folder)
                ? Directory.GetDirectories(folder).Select(d => Path.GetFileName(d)).ToList()
                : [];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    // Deletes the backups beyond the newest `keep`. A folder is only removed when it holds
    // nothing but its own archive: one the user put something else into is left alone.
    public static int Prune(string backupFolderPath, int keep)
    {
        var folder = ResolveFolder(backupFolderPath);
        var deleted = 0;

        foreach (var name in BackupSchedule.ToDelete(ExistingBackups(folder), keep))
        {
            var path = Path.Combine(folder, name);
            try
            {
                var entries = Directory.GetFileSystemEntries(path);
                if (entries.Length != 1 || Path.GetFileName(entries[0]) != $"{name}.zip")
                    continue;

                Directory.Delete(path, recursive: true);
                deleted++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Locked or gone: try again after the next backup.
            }
        }

        return deleted;
    }

    private static string ResolveFolder(string backupFolderPath) =>
        string.IsNullOrWhiteSpace(backupFolderPath) ? AppSettings.DefaultBackupFolderPath : backupFolderPath;

    // The caller shows this in a one-line snackbar, so name the exception type and
    // unwrap to the innermost cause: a bare "The path is empty. (Parameter 'path')"
    // says nothing about which layer threw it.
    private static string Describe(Exception ex)
    {
        var root = ex;
        while (root.InnerException is not null)
            root = root.InnerException;

        return ReferenceEquals(root, ex)
            ? $"{ex.GetType().Name}: {ex.Message}"
            : $"{ex.GetType().Name}: {ex.Message} -> {root.GetType().Name}: {root.Message}";
    }

    private static void TryCleanupFolder(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, recursive: true); }
        catch { /* best effort */ }
    }
}
