# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run Commands

```bash
# Build (from repo root)
dotnet build NoteApp.slnx

# Run (from repo root)
dotnet run --project src/NoteApp

# EF Core migrations (from src/NoteApp)
cd src/NoteApp
dotnet ef migrations add MigrationName
dotnet ef database update

# User secrets (from src/NoteApp)
cd src/NoteApp
dotnet user-secrets set "ConnectionStrings:NoteDb" "Server=YOUR_SERVER;Database=noteDb;User Id=YOUR_USER;Password=YOUR_PASSWORD;TrustServerCertificate=True"
```

No test project exists yet. QuestPDF is referenced but PDF export is not yet implemented.

## Repository

GitHub: https://github.com/VitakSharper/NoteAppWpf.git (remote `origin`, branch `master`).

## Architecture

WPF desktop app (.NET 10) with functional programming patterns (Zoran Horvat style). SQL Server backend via EF Core 10. Solution file is `NoteApp.slnx` (not `.sln`).

### Layer Dependency Flow

```
Domain (pure, zero dependencies)
  -> Data (EF Core entities, configurations, repositories)
    -> Services (orchestration, mapping, encryption)
      -> ViewModels (CommunityToolkit.Mvvm)
        -> Views (WPF XAML + MaterialDesignInXAML)
```

DI is configured in `App.xaml.cs`. Connection string loaded via .NET User Secrets (key: `ConnectionStrings:NoteDb`).

### Key Patterns

- **Result<T, AppError> monad** instead of exceptions for control flow. All repository/service methods return `Result`. Use `Match`, `Bind`, `Map` for composition.
- **Option<T> monad** instead of nulls.
- **Immutable domain records** with `with` expressions for updates (e.g., `note with { Title = newTitle }`).
- **Value objects** with private constructors and factory methods returning `Result` (NoteTitle, TagName, LinkUrl, NoteId).
- **Sealed discriminated unions** for note block types: `NoteBlock.Text | NoteBlock.File | NoteBlock.Link` — defined in `Domain/Models/NoteContent.cs` (filename doesn't match type name).
- **Pure mapper functions** in `Services/Mapping/NoteMapper.cs` converting between EF entities and domain models.
- **MVVM** with `[ObservableProperty]` and `[RelayCommand]` source generators from CommunityToolkit.Mvvm.
- **TagFilterItem wrapper** in `NoteListViewModel.cs` — wraps `Tag` with observable `IsSelected` for tag filter UI state. `AllTags` is `ObservableCollection<TagFilterItem>`, not raw `Tag`.
- **Three-pane shell** in `MainWindow.xaml`: 64px navy icon rail (always `#20283D`, both themes) · `MiddlePaneContent` `ContentControl` that swaps between `NoteListViewModel` and `TagManagerViewModel` · `CurrentEditor` `ContentControl` for the active editor or empty-state; Settings is hosted in `materialDesign:DialogHost` (`RootDialog`) as a modal overlay, not a separate pane.
- **Comfortable single-column note list** in `NoteListView.xaml`: a `ListBox` with card-style rows showing title + `NotePreviewConverter` snippet + tag chips + date. The old card/DataGrid toggle was removed. Tag chips use a static violet palette (`#E6E8FF` fill / `#3A3F8F` text) intentionally legible on both light and dark card surfaces.
- **Modern Violet MD3 theme** in `Theme/ModernViolet.xaml`: static brushes for the rail (`RailBackgroundBrush`, `RailForegroundBrush`, `RailSelectedBrush`), accent (`AccentBrush` `#6C79FF`), and tag chips (`TagChipBackground/Foreground/Border`), plus `CornerRadius` tokens (`CardCornerRadius`, `ControlCornerRadius`). All other surfaces use MDIX `{DynamicResource MaterialDesign*}` brushes and auto-switch on dark-mode toggle.

### Note Model

Notes contain ordered `NoteBlock` items (multi-block). Each block is Text (rich text), File (binary stored as VARBINARY(MAX)), or Link (validated URL). Block ordering uses `SortOrder`. Encrypted notes serialize blocks to JSON, encrypt with AES-GCM + PBKDF2, store in `EncryptedContent` column, and clear plaintext blocks from storage.

### Cross-Layer Change Coordination

When modifying note structure or block types, update across all layers:
1. `Domain/Models/Note.cs` + `Domain/Models/NoteContent.cs`
2. `Data/Entities/NoteBlockEntity.cs` + EF configuration
3. `Services/Mapping/NoteMapper.cs`
4. `Data/Repositories/NoteRepository.cs`
5. `Services/EncryptionService.cs` (if it affects serialization)
6. `ViewModels/NoteEditorViewModel.cs` + `Views/NoteEditorView.xaml`
7. Add EF migration if schema changed

### Database Backup

`BackupService` exports a SQL Server BACPAC via DacFx, then packages it into a password-protected AES-256 zip (SharpZipLib). Triggered from `SettingsViewModel.BackupAsync()`. Output: `NoteApp_<timestamp>.zip` in the configured backup folder (default `AppSettings.DefaultBackupFolderPath`). Intermediate `.bacpac` is deleted after zipping. Returns `Result<string, AppError>`.

### Stale Documentation Warning

`plan.md` is partially stale — it describes the older single-content-note model. The current code uses multi-block notes and includes encryption support not reflected in the plan. Trust code and README over plan.md.
