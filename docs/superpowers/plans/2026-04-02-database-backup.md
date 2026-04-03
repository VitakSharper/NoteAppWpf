# Database Backup (BACPAC Export) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a Settings UI button that exports the SQL Server database as a password-protected zip containing a BACPAC file, stored in a timestamped folder under `%LocalAppData%/NoteApp/Backups/`.

**Architecture:** A new `BackupService` uses DacFx to export the BACPAC and SharpZipLib to create a password-protected zip. The backup password is stored in `AppSettings`. The feature is triggered from the Settings view via the existing MVVM pattern.

**Tech Stack:** Microsoft.SqlServer.DacFx, SharpZipLib (ICSharpCode.SharpZipLib), WPF + MaterialDesignInXAML, CommunityToolkit.Mvvm

---

## File Map

| Action | File | Responsibility |
|--------|------|---------------|
| Modify | `src/NoteApp/NoteApp.csproj` | Add DacFx + SharpZipLib NuGet packages |
| Modify | `src/NoteApp/Services/AppSettings.cs` | Add `BackupPassword` property |
| Create | `src/NoteApp/Services/BackupService.cs` | BACPAC export + zip pipeline |
| Modify | `src/NoteApp/ViewModels/SettingsViewModel.cs` | Backup command, password binding, progress state |
| Modify | `src/NoteApp/Views/SettingsView.xaml` | Backup UI card with password field + button |
| Modify | `src/NoteApp/App.xaml.cs` | Register BackupService in DI |

---

### Task 1: Add NuGet Packages

**Files:**
- Modify: `src/NoteApp/NoteApp.csproj`

- [ ] **Step 1: Add DacFx and SharpZipLib packages**

Run from repo root:

```bash
cd src/NoteApp && dotnet add package Microsoft.SqlServer.DacFx && dotnet add package SharpZipLib
```

- [ ] **Step 2: Verify the build still succeeds**

```bash
cd /c/Repos/NoteApp && dotnet build NoteApp.slnx
```

Expected: Build succeeded with 0 errors.

---

### Task 2: Add BackupPassword to AppSettings

**Files:**
- Modify: `src/NoteApp/Services/AppSettings.cs`

- [ ] **Step 1: Add BackupPassword parameter to the AppSettings record**

Replace the entire `AppSettings.cs` content with:

```csharp
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
    string BackupPassword)
{
    public static AppSettings Default { get; } = new(
        ConfirmNoteDeletion: true,
        ConfirmTagDeletion: true,
        LaunchPage: StartupPage.Notes,
        IsDarkMode: false,
        BackupPassword: "");
}
```

- [ ] **Step 2: Update SettingsViewModel.Save() to pass BackupPassword**

In `src/NoteApp/ViewModels/SettingsViewModel.cs`, the `Save()` method constructs `AppSettings`. It needs the new parameter. We'll add the full ViewModel changes in Task 4, but for now update `Save()` to compile:

In the `Save()` method, change:

```csharp
_settingsService.Save(new AppSettings(
    ConfirmNoteDeletion,
    ConfirmTagDeletion,
    StartupPage,
    IsDarkMode));
```

to:

```csharp
_settingsService.Save(new AppSettings(
    ConfirmNoteDeletion,
    ConfirmTagDeletion,
    StartupPage,
    IsDarkMode,
    BackupPassword));
```

Also add the field and `LoadFrom` line (we'll wire the full UI in Task 4, but this keeps it compiling):

Add the observable property after the existing ones:

```csharp
[ObservableProperty] private string _backupPassword = "";
```

In `LoadFrom()`, add at the end:

```csharp
BackupPassword = settings.BackupPassword;
```

In `ResetDefaults()` — no change needed since it calls `LoadFrom(AppSettings.Default)` then `Save()`.

- [ ] **Step 3: Verify the build succeeds**

```bash
cd /c/Repos/NoteApp && dotnet build NoteApp.slnx
```

Expected: Build succeeded with 0 errors.

---

### Task 3: Create BackupService

**Files:**
- Create: `src/NoteApp/Services/BackupService.cs`

- [ ] **Step 1: Create the BackupService class**

Create `src/NoteApp/Services/BackupService.cs` with this content:

```csharp
using System.IO;
using ICSharpCode.SharpZipLib.Zip;
using Microsoft.SqlServer.Dac;
using NoteApp.Domain.Functional;

namespace NoteApp.Services;

public sealed class BackupService(string connectionString, string databaseName)
{
    private static readonly string BackupBasePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "NoteApp",
        "Backups");

    public async Task<Result<string, AppError>> ExportAsync(string password)
    {
        try
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
            var folderName = $"NoteApp_{timestamp}";
            var folderPath = Path.Combine(BackupBasePath, folderName);

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
            return Result<string, AppError>.Fail(
                AppError.Database($"BACPAC export failed: {ex.Message}"));
        }
        catch (IOException ex)
        {
            return Result<string, AppError>.Fail(
                AppError.Io($"File operation failed: {ex.Message}"));
        }
        catch (Exception ex)
        {
            return Result<string, AppError>.Fail(
                AppError.Io($"Backup failed: {ex.Message}"));
        }
    }
}
```

- [ ] **Step 2: Verify the build succeeds**

```bash
cd /c/Repos/NoteApp && dotnet build NoteApp.slnx
```

Expected: Build succeeded with 0 errors.

---

### Task 4: Update SettingsViewModel with Backup Command

**Files:**
- Modify: `src/NoteApp/ViewModels/SettingsViewModel.cs`

- [ ] **Step 1: Add BackupService dependency and backup command**

Add the `BackupService` field to the class. Update the constructor to accept it:

Change the constructor from:

```csharp
public SettingsViewModel(AppSettingsService settingsService)
{
    _settingsService = settingsService;
    LoadFrom(_settingsService.Current);
}
```

to:

```csharp
private readonly BackupService _backupService;

public SettingsViewModel(AppSettingsService settingsService, BackupService backupService)
{
    _settingsService = settingsService;
    _backupService = backupService;
    LoadFrom(_settingsService.Current);
}
```

- [ ] **Step 2: Add IsBackingUp property and Backup command**

Add the observable property after the existing ones:

```csharp
[ObservableProperty] private bool _isBackingUp;
```

Add the async relay command:

```csharp
[RelayCommand]
private async Task BackupAsync()
{
    if (string.IsNullOrWhiteSpace(BackupPassword))
    {
        ShowMessage?.Invoke("Set a backup password before exporting.");
        return;
    }

    // Save current settings first so the password is persisted
    Save();

    IsBackingUp = true;
    try
    {
        var result = await _backupService.ExportAsync(BackupPassword);
        result.Match(
            success: path => ShowMessage?.Invoke($"Backup saved to {path}"),
            failure: error => ShowMessage?.Invoke($"Backup failed: {error.Message}"));
    }
    finally
    {
        IsBackingUp = false;
    }
}
```

Add the `using NoteApp.Services;` import at the top if not already present (it already is via the `AppSettingsService` usage, but verify the namespace covers `BackupService` — it does since both are in `NoteApp.Services`).

- [ ] **Step 3: Verify the build succeeds**

```bash
cd /c/Repos/NoteApp && dotnet build NoteApp.slnx
```

Expected: Build succeeded with 0 errors.

---

### Task 5: Update Settings View with Backup UI

**Files:**
- Modify: `src/NoteApp/Views/SettingsView.xaml`

- [ ] **Step 1: Add a new row to the Grid for the Backup card**

In the `<Grid.RowDefinitions>` section, add a fourth row:

Change:

```xml
<Grid.RowDefinitions>
    <RowDefinition Height="Auto" />
    <RowDefinition Height="Auto" />
    <RowDefinition Height="Auto" />
</Grid.RowDefinitions>
```

to:

```xml
<Grid.RowDefinitions>
    <RowDefinition Height="Auto" />
    <RowDefinition Height="Auto" />
    <RowDefinition Height="Auto" />
    <RowDefinition Height="Auto" />
</Grid.RowDefinitions>
```

- [ ] **Step 2: Add the Backup card after the Storage card**

Insert the following XAML after the closing `</materialDesign:Card>` of the Storage card (Grid.Row="2"), before the closing `</Grid>`:

```xml
<materialDesign:Card Grid.Row="3"
                     Padding="20"
                     Margin="0,16,0,0"
                     materialDesign:ElevationAssist.Elevation="Dp2">
    <StackPanel>
        <TextBlock Text="Database Backup"
                   Style="{StaticResource MaterialDesignHeadline6TextBlock}"
                   Margin="0,0,0,16" />

        <TextBlock Text="Export the database as a password-protected BACPAC archive."
                   Margin="0,0,0,16"
                   Foreground="{DynamicResource MaterialDesignBodyLight}" />

        <StackPanel Margin="0,0,0,16">
            <TextBlock Text="Backup password"
                       FontSize="14"
                       FontWeight="Medium"
                       Margin="0,0,0,8" />
            <PasswordBox materialDesign:HintAssist.Hint="Enter backup password"
                         materialDesign:PasswordBoxAssist.Password="{Binding BackupPassword, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}"
                         Style="{StaticResource MaterialDesignOutlinedPasswordBox}"
                         MaxWidth="400"
                         HorizontalAlignment="Left" />
            <TextBlock Text="This password protects the zip archive. Store it safely — it cannot be recovered."
                       Margin="0,8,0,0"
                       Foreground="{DynamicResource MaterialDesignBodyLight}" />
        </StackPanel>

        <StackPanel Orientation="Horizontal"
                    Margin="0,8,0,0">
            <Button Content="Backup Now"
                    Style="{StaticResource MaterialDesignRaisedButton}"
                    Command="{Binding BackupCommand}"
                    IsEnabled="{Binding IsBackingUp, Converter={StaticResource InvertBooleanConverter}}"
                    Margin="0,0,16,0" />
            <ProgressBar Style="{StaticResource MaterialDesignCircularProgressBar}"
                         IsIndeterminate="True"
                         Visibility="{Binding IsBackingUp, Converter={StaticResource BooleanToVisibilityConverter}}"
                         Width="24" Height="24"
                         VerticalAlignment="Center" />
        </StackPanel>
    </StackPanel>
</materialDesign:Card>
```

- [ ] **Step 3: Verify the `InvertBooleanConverter` and `BooleanToVisibilityConverter` exist**

These are standard WPF / MaterialDesign converters. Check if they are defined in `App.xaml` resource dictionaries. If `InvertBooleanConverter` is not available, use an alternative approach — bind `IsEnabled` with a style trigger instead, or add the converter.

If `InvertBooleanConverter` is not found, replace:

```xml
IsEnabled="{Binding IsBackingUp, Converter={StaticResource InvertBooleanConverter}}"
```

with a simple negated property. Add to `SettingsViewModel.cs`:

```csharp
public bool CanBackup => !IsBackingUp;
```

And add to the `OnIsBackingUpChanged` partial method:

```csharp
partial void OnIsBackingUpChanged(bool value) => OnPropertyChanged(nameof(CanBackup));
```

Then use in XAML:

```xml
IsEnabled="{Binding CanBackup}"
```

- [ ] **Step 4: Verify the PasswordBox binding works with MaterialDesign**

The `materialDesign:PasswordBoxAssist.Password` attached property enables binding on `PasswordBox` (which WPF doesn't natively support). This is a MaterialDesignInXAML feature. Verify it compiles.

- [ ] **Step 5: Build and verify**

```bash
cd /c/Repos/NoteApp && dotnet build NoteApp.slnx
```

Expected: Build succeeded with 0 errors. If converter errors occur, apply the fallback from Step 3.

---

### Task 6: Register BackupService in DI

**Files:**
- Modify: `src/NoteApp/App.xaml.cs`

- [ ] **Step 1: Register BackupService with the connection string**

In `App.xaml.cs`, after the existing service registrations (after `services.AddScoped<SearchService>();`), add:

```csharp
// Extract database name from connection string for DacFx
var builder = new System.Data.SqlClient.SqlConnectionStringBuilder(connectionString);
var databaseName = builder.InitialCatalog;

services.AddSingleton(new BackupService(connectionString, databaseName));
```

Wait — `System.Data.SqlClient` may not be referenced. The project uses `Microsoft.Data.SqlClient` (via EF Core SqlServer). Use that instead:

```csharp
var sqlBuilder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connectionString);
services.AddSingleton(new BackupService(connectionString, sqlBuilder.InitialCatalog));
```

Add the `using NoteApp.Services;` import if not already present (it should be, since `AppSettingsService` is in the same namespace).

- [ ] **Step 2: Build and verify**

```bash
cd /c/Repos/NoteApp && dotnet build NoteApp.slnx
```

Expected: Build succeeded with 0 errors.

---

### Task 7: Manual Smoke Test

- [ ] **Step 1: Run the application**

```bash
cd /c/Repos/NoteApp && dotnet run --project src/NoteApp
```

- [ ] **Step 2: Test the backup flow**

1. Navigate to Settings
2. Scroll to the "Database Backup" card
3. Enter a backup password
4. Click "Backup Now"
5. Verify the SnackBar shows a success message with the file path
6. Verify the zip file exists at the reported path
7. Verify the zip is password-protected (try opening it — it should prompt for password)
8. Verify the folder structure: `%LocalAppData%/NoteApp/Backups/NoteApp_YYYY-MM-DD_HHmmss/NoteApp_YYYY-MM-DD_HHmmss.zip`

- [ ] **Step 3: Test error case — empty password**

1. Clear the backup password field
2. Click "Backup Now"
3. Verify SnackBar shows "Set a backup password before exporting."

---
