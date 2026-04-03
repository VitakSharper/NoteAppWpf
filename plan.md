# NoteApp — Implementation Plan

## Problem Statement

Build a WPF desktop application (.NET 10) backed by SQL Server for managing notes. Notes can be **text** (rich text), **files** (PDF, images, etc. stored as BLOBs), or **internet links**. Users can tag notes, search, and perform full CRUD operations.

## Tech Stack & Approach

- **UI**: WPF with MaterialDesignInXAML Toolkit
- **Framework**: .NET 10
- **Database**: SQL Server with SQL Authentication
- **ORM**: Entity Framework Core 10 (SQL Server provider)
- **MVVM**: CommunityToolkit.Mvvm
- **Rich Text**: WPF RichTextBox with FlowDocument (XAML serialization)
- **Coding Style**: Functional programming (Zoran Horvat style)
  - Immutable records for domain models
  - `Option<T>` monad for optional values (no nulls)
  - `Result<T, TError>` monad for operation outcomes (no exceptions for flow)
  - Sealed class hierarchies as discriminated unions for note content types
  - Extension methods for fluent pipelines
  - Value objects with validation (NoteTitle, TagName, etc.)
  - Pure functions at the core, side effects pushed to edges
  - Pattern matching via `switch` expressions

## Database Schema

```
noteDb
├── Notes
│   ├── Id                UNIQUEIDENTIFIER PK
│   ├── Title             NVARCHAR(500) NOT NULL
│   ├── NoteType          INT NOT NULL (0=Text, 1=File, 2=Link)
│   ├── TextContent       NVARCHAR(MAX) NULL
│   ├── FileData          VARBINARY(MAX) NULL
│   ├── FileName          NVARCHAR(500) NULL
│   ├── FileExtension     NVARCHAR(50) NULL
│   ├── FileSizeBytes     BIGINT NULL
│   ├── LinkUrl           NVARCHAR(2000) NULL
│   ├── LinkDescription   NVARCHAR(1000) NULL
│   ├── CreatedAt         DATETIME2 NOT NULL
│   └── UpdatedAt         DATETIME2 NOT NULL
│
├── Tags
│   ├── Id                UNIQUEIDENTIFIER PK
│   └── Name              NVARCHAR(100) NOT NULL UNIQUE
│
└── NoteTags
    ├── NoteId            UNIQUEIDENTIFIER FK → Notes(Id) CASCADE
    └── TagId             UNIQUEIDENTIFIER FK → Tags(Id) CASCADE
```

## Project Structure

```
NoteApp/
├── NoteApp.sln
└── src/NoteApp/
    ├── NoteApp.csproj
    ├── App.xaml / App.xaml.cs
    ├── MainWindow.xaml / MainWindow.xaml.cs
    │
    ├── Domain/                          Pure, no dependencies
    │   ├── Functional/
    │   │   ├── Option.cs                Option<T> monad (None/Some)
    │   │   ├── Result.cs                Result<T, TError> monad
    │   │   └── Unit.cs                  Unit type for void returns
    │   ├── ValueObjects/
    │   │   ├── NoteId.cs                Guid wrapper
    │   │   ├── NoteTitle.cs             Validated string (1-500 chars)
    │   │   ├── TagName.cs               Validated string (1-100 chars)
    │   │   ├── LinkUrl.cs               Validated URI
    │   │   └── NoteTypeFilter.cs        Search filter enum
    │   ├── Models/
    │   │   ├── NoteContent.cs           Sealed hierarchy: TextContent | FileContent | LinkContent
    │   │   ├── Note.cs                  Immutable record
    │   │   └── Tag.cs                   Immutable record
    │   └── Extensions/
    │       ├── OptionExtensions.cs      Map, Bind, Match, ToResult, etc.
    │       └── ResultExtensions.cs      Map, Bind, Match, Tap, ToUnit, etc.
    │
    ├── Data/                            EF Core, side effects boundary
    │   ├── NoteDbContext.cs
    │   ├── NoteDbContextFactory.cs      Design-time factory for migrations
    │   ├── Entities/
    │   │   ├── NoteEntity.cs            Mutable EF entity
    │   │   ├── TagEntity.cs
    │   │   └── NoteTagEntity.cs
    │   ├── Configurations/
    │   │   ├── NoteConfiguration.cs     Fluent API config
    │   │   ├── TagConfiguration.cs
    │   │   └── NoteTagConfiguration.cs
    │   └── Repositories/
    │       ├── INoteRepository.cs       Returns Result<T, AppError>
    │       ├── NoteRepository.cs
    │       ├── ITagRepository.cs
    │       └── TagRepository.cs
    │
    ├── Services/                        Orchestration layer
    │   ├── NoteService.cs               CRUD + file handling
    │   ├── SearchService.cs             Full-text + tag search
    │   └── Mapping/
    │       └── NoteMapper.cs            Entity ↔ Domain mapping (pure functions)
    │
    ├── ViewModels/                      MVVM ViewModels
    │   ├── MainViewModel.cs             Navigation shell
    │   ├── NoteListViewModel.cs         List + search
    │   ├── NoteEditorViewModel.cs       Create/Edit note
    │   └── TagManagerViewModel.cs       CRUD tags
    │
    ├── Views/                           XAML views
    │   ├── NoteListView.xaml            Note list with search bar
    │   ├── NoteEditorView.xaml          Tabbed editor (text/file/link)
    │   └── TagManagerView.xaml          Tag chip management
    │
    └── Converters/
        └── Converters.cs                NoteTypeToIcon, FileSize, BoolToVisibility
```

## Implementation Phases

### Phase 1: Project Scaffolding & Domain ✅
1. **scaffold-project** — Created WPF .NET 10 solution, installed NuGet packages, configured Material Design theme
2. **domain-functional** — Implemented `Option<T>`, `Result<T, TError>`, `Unit` with extension methods
3. **domain-value-objects** — Created `NoteId`, `NoteTitle`, `TagName`, `LinkUrl` with factory methods returning `Result`
4. **domain-models** — Created sealed hierarchy `NoteContent` (Text, File, Link), `Note` record, `Tag` record

### Phase 2: Database & Data Access ✅
5. **database-setup** — Created `noteDb` database, configured connection string via .NET User Secrets
6. **ef-entities** — Created EF entity classes with fluent API configurations
7. **ef-dbcontext** — Implemented `NoteDbContext` with design-time factory
8. **ef-migration** — Generated and applied `InitialCreate` migration
9. **repositories** — Implemented `NoteRepository` and `TagRepository` returning `Result<T, AppError>`
10. **entity-mapping** — Pure mapping functions in `NoteMapper` and `TagMapper`

### Phase 3: Services ✅
11. **note-service** — Full CRUD: create text/file/link notes, update, delete, search
12. **search-service** — Search by title/content, filter by tags, filter by note type

### Phase 4: UI — Shell & Navigation ✅
13. **main-window** — Material Design shell with navigation drawer, toolbar, snackbar
14. **navigation** — ViewModel-first navigation via DataTemplates

### Phase 5: UI — Note List & Search ✅
15. **note-list-view** — Card layout with title, type icon, tags, date, edit/delete buttons
16. **search-ui** — Search bar with text input, tag filter chips, type filter buttons

### Phase 6: UI — Note Editor ✅
17. **note-editor-view** — Tabbed editor for text/file/link types
18. **rich-text-editor** — RichTextBox with formatting toolbar (bold, italic, underline, bullets)
19. **file-handling** — File browse picker with file info display
20. **tag-assignment** — Toggle buttons for tag selection

### Phase 7: UI — Tag Management ✅
21. **tag-manager-view** — List tags, create new, rename (overlay dialog), delete

### Phase 8: Polish & Wiring ✅
22. **dependency-injection** — DI container in App.xaml.cs
23. **error-handling-ui** — Snackbar notifications mapped from Result errors
24. **final-testing** — Clean build, zero errors, zero warnings

## Key Design Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| File storage | `VARBINARY(MAX)` in DB | Simpler deployment, no file path management |
| Rich text format | FlowDocument serialized as XAML | Native WPF, no third-party editor |
| Error handling | `Result<T, AppError>` everywhere | No exceptions for flow control (Zoran Horvat style) |
| Credentials | .NET User Secrets | Never committed to source |
| ORM | EF Core with repository pattern | Migrations, LINQ search, isolated from domain |

## NuGet Packages

| Package | Purpose |
|---------|---------|
| MaterialDesignThemes | Material Design UI components |
| MaterialDesignColors | Color palette |
| Microsoft.EntityFrameworkCore.SqlServer | EF Core SQL Server provider |
| Microsoft.EntityFrameworkCore.Tools | EF migrations CLI |
| Microsoft.EntityFrameworkCore.Design | Design-time EF support |
| CommunityToolkit.Mvvm | MVVM source generators |
| Microsoft.Extensions.DependencyInjection | DI container |
| Microsoft.Extensions.Configuration.UserSecrets | Secure connection string |
| Microsoft.Extensions.Configuration.Json | appsettings.json support |
