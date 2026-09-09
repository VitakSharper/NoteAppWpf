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

```bash
# Tests (from repo root) — xUnit on the pure layers (Domain, mapper, encryption, preview); no DB, no UI thread
dotnet test NoteApp.slnx
```

PDF export of text blocks is implemented (`Services/PdfExportService.cs`, QuestPDF, wired from the editor toolbar).

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

DI is configured in `App.xaml.cs`. Connection string loaded via .NET User Secrets (key: `ConnectionStrings:NoteDb`). `NoteDbContext` is registered with `AddDbContextFactory`; repositories take `IDbContextFactory<NoteDbContext>` and open one short-lived context per operation (everything is resolved from the root provider, so a scoped context would live as long as the app). Unhandled dispatcher exceptions are appended to `%LocalAppData%\NoteApp\crash.log` and shown in a message box instead of killing the app.

### Key Patterns

- **Result<T, AppError> monad** instead of exceptions for control flow. All repository/service methods return `Result`. Use `Match`, `Bind`, `Map` for composition; `TryGet(out value, out error)` is the imperative escape hatch for early returns — never cast to `.Success`/`.Failure`.
- **Option<T> monad** instead of nulls.
- **Immutable domain records** with `with` expressions for updates (e.g., `note with { Title = newTitle }`).
- **Value objects** with private constructors and factory methods returning `Result` (NoteTitle, TagName, LinkUrl, NoteId).
- **Sealed discriminated unions** for note block types: `NoteBlock.Text | NoteBlock.File | NoteBlock.Link` — defined in `Domain/Models/NoteContent.cs` (filename doesn't match type name).
- **`PlainText` next to rich text** — `NoteBlock.Text(RichText, PlainText)`: `RichText` is a Base64 XamlPackage (not searchable), so the editor also writes the plain text (`NoteEditorView.SyncBlock`) and the `NoteBlocks.PlainText` column drives search and the list preview (`Services/RichTextPreview.cs`). Rows saved before the column exists have `PlainText = NULL` and fall back to decoding the rich payload.
- **`NoteSummary` read model** (`Domain/Models/NoteSummary.cs`) for the note list: `INoteRepository.SearchSummariesAsync` projects `Data/Queries/NoteSummaryRow` (no block payloads, no `FileData`), `NoteService.SearchAsync` turns rows into summaries with a precomputed `Preview`. `NoteListViewModel` only holds summaries; `MainViewModel` loads the full `Note` by id when one is opened.
- **Unsaved-changes guard** — `NoteEditorViewModel.IsDirty` is set at the source of each change (its own `Title`/`IsEncrypted`, `Blocks` and `SelectedTags` collection changes, any `BlockViewModel` property **except** `RichTextContent`/`PlainTextContent`, which `NoteEditorView.SyncBlock` writes), and cleared by `RefreshAfterSave`. Rich text is invisible to the view model until a sync, so the view calls `MarkDirty()` from `RichTextBox.TextChanged` — with `WithoutDirtyTracking(...)` bracketing every write the app makes itself (loading a block, search highlights), otherwise opening the search bar would mark the note edited. Do **not** replace this with a snapshot comparison: `TextRange.Save(XamlPackage)` gives no byte-identical guarantee for the same document. Every exit goes through `MainViewModel.ConfirmLeaveEditorAsync()` (open another note, New note, Cancel, `MainWindow.OnClosing`); it re-runs `SaveCommand` for "Yes" and keeps the editor open when the save fails validation (`IsDirty` stays true). Deleting the open note closes its editor without prompting (`NoteListViewModel.NoteDeleted`).
- **Pure mapper functions** in `Services/Mapping/NoteMapper.cs` converting between EF entities and domain models.
- **MVVM** with `[ObservableProperty]` and `[RelayCommand]` source generators from CommunityToolkit.Mvvm.
- **TagFilterItem wrapper** in `NoteListViewModel.cs` — wraps `Tag` with observable `IsSelected` for tag filter UI state. `AllTags` is `ObservableCollection<TagFilterItem>`, not raw `Tag`.
- **Three-pane shell** in `MainWindow.xaml`: 64px navy icon rail (always `#20283D`, both themes) · `MiddlePaneContent` `ContentControl` that swaps between `NoteListViewModel` and `TagManagerViewModel` · `CurrentEditor` `ContentControl` for the active editor or empty-state; Settings is hosted in `materialDesign:DialogHost` (`RootDialog`) as a modal overlay, not a separate pane.
- **Comfortable single-column note list** in `NoteListView.xaml`: a `ListBox` with card-style rows showing title + `Preview` snippet + tag chips + date. The old card/DataGrid toggle was removed. Tag chips use a static violet palette (`#E6E8FF` fill / `#3A3F8F` text) intentionally legible on both light and dark card surfaces. The type filter (All/Text/File/Link) is a second `ListBox` styled as MD3 filter chips (`FilterChipItemStyle`) whose `SelectedItem` binds `SelectedTypeFilter`; the reload happens in `OnSelectedTypeFilterChanged` (there is no select command), and `HasActiveFilters` drives the single "clear" button next to the search field.
- **Editor rich text never scrolls itself** — the `RichTextBox` has `VerticalScrollBarVisibility=Disabled` and grows with its content; only `BlocksScrollViewer` scrolls, and `OnRichTextBoxPreviewMouseWheel` forwards the wheel to it (a nested ScrollViewer would otherwise swallow it). Scrollbars app-wide use `MaterialDesignScrollBarMinimal` (implicit style in `App.xaml`).
- **Stored rich text carries the theme's text colour** — `TextRange.Save` bakes the inherited `Foreground` into the XamlPackage, so `DeserializeIntoRichTextBox` calls `ClearForeground` on load; without it a note written in dark mode is white-on-white in the light theme. The editor has no text-colour feature, so stripping every `Foreground` is safe.
- **Modern Violet MD3 theme** in `Theme/ModernViolet.xaml`: static brushes for the rail (`RailBackgroundBrush`, `RailForegroundBrush`, `RailSelectedBrush`), accent (`AccentBrush` `#6C79FF`), and tag chips (`TagChipBackground/Foreground/Border`), plus `CornerRadius` tokens (`CardCornerRadius`, `ControlCornerRadius`). All other surfaces use MDIX `{DynamicResource MaterialDesign*}` brushes and auto-switch on dark-mode toggle.

### Note Model

Notes contain ordered `NoteBlock` items (multi-block). Each block is Text (rich text), File (binary stored as VARBINARY(MAX)), or Link (validated URL). Block ordering uses `SortOrder`. Encrypted notes serialize blocks to JSON, encrypt with AES-GCM + PBKDF2, store in `EncryptedContent` column, and clear plaintext blocks from storage.

### Cross-Layer Change Coordination

When modifying note structure or block types, update across all layers:
1. `Domain/Models/Note.cs` + `Domain/Models/NoteContent.cs` (+ `NoteSummary.cs` if the list shows it)
2. `Data/Entities/NoteBlockEntity.cs` + EF configuration
3. `Services/Mapping/NoteMapper.cs`
4. `Data/Repositories/NoteRepository.cs` (+ the `NoteSummaryRow` projection in `SearchSummariesAsync`)
5. `Services/EncryptionService.cs` (if it affects serialization — the `BlockDto` JSON inside encrypted notes)
6. `ViewModels/NoteEditorViewModel.cs` + `Views/NoteEditorView.xaml(.cs)`
7. Add EF migration if schema changed
8. `tests/NoteApp.Tests` — mapper round-trip and encryption round-trip tests cover every block field

### Database Backup

`BackupService` exports a SQL Server BACPAC via DacFx, then packages it into a password-protected AES-256 zip (SharpZipLib). Triggered from `SettingsViewModel.BackupAsync()`. Output: `NoteApp_<timestamp>.zip` in the configured backup folder (default `AppSettings.DefaultBackupFolderPath`). Intermediate `.bacpac` is deleted after zipping. Returns `Result<string, AppError>`.

Settings live in `%LocalAppData%\NoteApp\settings.json` (`AppSettingsService`). The backup password is stored DPAPI-protected for the current Windows user (`BackupPasswordProtected`); a legacy clear-text `BackupPassword` field is still read and upgraded on the next save.

### Stale Documentation Warning

`plan.md` is partially stale — it describes the older single-content-note model. The current code uses multi-block notes and includes encryption support not reflected in the plan. Trust code and README over plan.md.
