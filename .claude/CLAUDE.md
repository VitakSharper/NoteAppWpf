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

### Stale Documentation Warning

`plan.md` is partially stale — it describes the older single-content-note model. The current code uses multi-block notes and includes encryption support not reflected in the plan. Trust code and README over plan.md.
