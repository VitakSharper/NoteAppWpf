using System.IO;
using ICSharpCode.SharpZipLib.Zip;
using Microsoft.SqlServer.Dac;
using NoteApp.Domain.Functional;

namespace NoteApp.Services;

public sealed class BackupService(string connectionString, string databaseName)
{
    public async Task<Result<string, AppError>> ExportAsync(string password, string backupFolderPath)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
        var folderName = $"NoteApp_{timestamp}";
        var basePath = string.IsNullOrWhiteSpace(backupFolderPath)
            ? AppSettings.DefaultBackupFolderPath
            : backupFolderPath;
        var folderPath = Path.Combine(basePath, folderName);

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
            return Result<string, AppError>.Fail(
                AppError.Io($"Backup failed: {ex.Message}"));
        }
    }

    private static void TryCleanupFolder(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, recursive: true); }
        catch { /* best effort */ }
    }
}
