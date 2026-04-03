# Database Backup (BACPAC Export) — Design Spec

## Goal

Allow the user to export the SQL Server database as a password-protected zip containing a BACPAC file, stored in a timestamped folder under a fixed local directory.

## Trigger

A "Backup Now" button in the Settings view. No scheduled or automatic backups.

## Output Structure

```
%LocalAppData%/NoteApp/Backups/
  └── NoteApp_2026-04-02_143025/
        └── NoteApp_2026-04-02_143025.zip
            └── NoteApp_2026-04-02_143025.bacpac
```

- Folder name format: `NoteApp_yyyy-MM-dd_HHmmss`
- Zip file name matches the folder name
- BACPAC file name matches the folder name
- Base path: `%LocalAppData%/NoteApp/Backups/` (fixed, not configurable)

## Components

### 1. BackupService (`Services/BackupService.cs`)

New service responsible for the entire export pipeline.

**Interface:**

```csharp
Task<Result<string, AppError>> ExportAsync(string connectionString, string password)
```

Returns the path to the created zip file on success.

**Pipeline:**

1. Generate timestamp string (`yyyy-MM-dd_HHmmss`) from `DateTime.Now`
2. Create folder: `{basePath}/NoteApp_{timestamp}/`
3. Use `Microsoft.SqlServer.DacFx` (`DacServices.ExportBacpac`) to export the database to a temp `.bacpac` file in that folder
4. Use `SharpZipLib` to create a password-protected zip (AES-256 encryption) containing the `.bacpac`
5. Delete the intermediate `.bacpac` file
6. Return the zip file path wrapped in `Result<string, AppError>`

**Error handling:** All failures (DacFx errors, IO errors, zip errors) are caught and returned as `AppError`. No exceptions escape the service.

### 2. AppSettings Changes (`Services/AppSettings.cs`)

Add a `BackupPassword` property (type `string`, default empty string) to the existing `AppSettings` record. Persisted in the existing `%LocalAppData%/NoteApp/settings.json`.

The password is stored in plaintext in the local settings file. This is acceptable because:
- It protects the zip contents, not the app itself
- The settings file is local to the user's machine
- It follows the same pattern as other app settings

### 3. SettingsViewModel Changes (`ViewModels/SettingsViewModel.cs`)

Add:
- `BackupPassword` observable property bound to the settings
- `BackupCommand` (async relay command) that:
  1. Validates the password is set (non-empty) — shows SnackBar error if not
  2. Calls `BackupService.ExportAsync`
  3. Shows SnackBar with success (includes file path) or error message
- `IsBackingUp` observable property to disable the button and show progress during export

### 4. Settings View Changes (`Views/SettingsView.xaml`)

Add a "Backup" section (Material Design card) below the existing settings, containing:
- A password field for the backup password (with show/hide toggle)
- A "Backup Now" button (disabled when password is empty or backup is in progress)
- A progress indicator visible during backup

### 5. DI Registration (`App.xaml.cs`)

Register `BackupService` as scoped in the service collection. The connection string is already available via configuration — pass it to the service or inject `IConfiguration`.

## NuGet Packages

| Package | Purpose |
|---------|---------|
| `Microsoft.SqlServer.DacFx` | BACPAC export via `DacServices` |
| `SharpZipLib` | Password-protected zip with AES-256 |

## Scope Exclusions

- No restore/import functionality
- No configurable backup path
- No scheduled/automatic backups
- No backup history or management UI
- No compression-level configuration
