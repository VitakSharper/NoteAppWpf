# NoteApp Codebase Map

## Overview

- Application type: WPF desktop app targeting `net10.0-windows`
- UI stack: WPF + MaterialDesignInXAML
- State pattern: MVVM with `CommunityToolkit.Mvvm`
- Data stack: EF Core 10 + SQL Server
- Core model: notes contain ordered `NoteBlock` items of type text, file, or link
- Security model: notes can be persisted as encrypted block payloads

## Trust These Sources First

- Trust `README.md` for the current product summary and setup commands.
- Trust the code over `plan.md`.
- Treat `plan.md` as historical context only.

Current mismatches:

- `plan.md` still describes the older `NoteType` and `NoteContent` design.
- The actual code uses `Note.Blocks` and `NoteBlock`.
- `Domain/Models/NoteContent.cs` now defines `NoteBlock`; the file name no longer matches the type.
- The current code includes encryption support through `EncryptionService`, `PasswordDialog`, and the `20260320155037_AddNoteEncryption` migration.

## Key Files

- App startup and DI: `src/NoteApp/App.xaml.cs`
- Project and package references: `src/NoteApp/NoteApp.csproj`
- Functional primitives: `src/NoteApp/Domain/Functional/`
- Domain note model: `src/NoteApp/Domain/Models/Note.cs`
- Domain block model: `src/NoteApp/Domain/Models/NoteContent.cs`
- Value objects: `src/NoteApp/Domain/ValueObjects/`
- EF context: `src/NoteApp/Data/NoteDbContext.cs`
- EF entities: `src/NoteApp/Data/Entities/`
- EF configurations: `src/NoteApp/Data/Configurations/`
- Repositories: `src/NoteApp/Data/Repositories/`
- Entity-domain mapping: `src/NoteApp/Services/Mapping/NoteMapper.cs`
- Note orchestration: `src/NoteApp/Services/NoteService.cs`
- Encryption flow: `src/NoteApp/Services/EncryptionService.cs`
- Note list behavior: `src/NoteApp/ViewModels/NoteListViewModel.cs`
- Note editor behavior: `src/NoteApp/ViewModels/NoteEditorViewModel.cs`
- Main shell behavior: `src/NoteApp/ViewModels/MainViewModel.cs`
- Note editor UI: `src/NoteApp/Views/NoteEditorView.xaml`
- Password prompt UI: `src/NoteApp/Views/PasswordDialog.xaml`
- Latest schema snapshot: `src/NoteApp/Migrations/NoteDbContextModelSnapshot.cs`

## Common Change Map

### Add or change a block type

- Domain: `Domain/Models/Note.cs`, `Domain/Models/NoteContent.cs`
- Persistence: `Data/Entities/NoteBlockEntity.cs`, `Data/Configurations/NoteConfiguration.cs`, migrations
- Mapping: `Services/Mapping/NoteMapper.cs`
- Repository logic: `Data/Repositories/NoteRepository.cs`
- Encryption serialization: `Services/EncryptionService.cs`
- Editor VM and view: `ViewModels/NoteEditorViewModel.cs`, `Views/NoteEditorView.xaml`, `Views/NoteEditorView.xaml.cs`
- List rendering and filters if applicable: `ViewModels/NoteListViewModel.cs`, `Views/NoteListView.xaml`

### Change encryption behavior

- `Services/EncryptionService.cs`
- `Services/NoteService.cs`
- `Data/Repositories/NoteRepository.cs`
- `ViewModels/NoteEditorViewModel.cs`
- `ViewModels/MainViewModel.cs`
- `Views/PasswordDialog.xaml`
- `Views/PasswordDialog.xaml.cs`
- Latest migration and model snapshot if schema changes

### Change search or filtering

- `ViewModels/NoteListViewModel.cs`
- `Services/NoteService.cs`
- `Data/Repositories/NoteRepository.cs`
- `Views/NoteListView.xaml`

### Change tags

- `Domain/Models/Tag.cs`
- `Domain/ValueObjects/TagName.cs`
- `Data/Repositories/TagRepository.cs`
- `ViewModels/TagManagerViewModel.cs`
- `ViewModels/NoteEditorViewModel.cs`
- `Views/TagManagerView.xaml`

## Commands

Run from the repo root unless noted otherwise.

```powershell
$env:DOTNET_CLI_HOME = "C:/Repos/NoteApp/.dotnet"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
dotnet build NoteApp.slnx
```

Run from `src/NoteApp` for secrets and EF commands.

```powershell
dotnet user-secrets set "ConnectionStrings:NoteDb" "Server=YOUR_SERVER;Database=noteDb;User Id=YOUR_USER;Password=YOUR_PASSWORD;TrustServerCertificate=True"
dotnet ef database update
dotnet run
```

## Environment Caveats

- The solution file in this repo is `NoteApp.slnx`.
- A `git status` check failed in this workspace snapshot because there is no `.git` directory at the repo root.
- In this sandbox, `dotnet build` attempted to reach NuGet repository-signature endpoints even with local assets present, so build verification may require network access or a differently configured local cache.
