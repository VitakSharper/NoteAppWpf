# NoteApp

A WPF desktop application for managing notes, built with .NET 10, SQL Server, and Material Design.

## Features

- **Multi-block notes** — Each note can contain multiple content blocks: rich text, files, and links — all in one note
- **Rich text editing** — Bold, italic, underline, bullet lists via a toolbar
- **File attachments** — Browse and attach files stored directly in the database
- **Block ordering** — Drag-free move up/down to reorder content blocks within a note
- **Tagging system** — Create, rename, delete tags and assign them to notes
- **Search & filter** — Search by title/content, filter by tags or note type
- **Full CRUD** — Create, read, update, delete notes and tags
- **Material Design UI** — Navigation drawer, cards, chips, snackbar notifications

## Tech Stack

| Layer | Technology |
|-------|-----------|
| UI | WPF + [MaterialDesignInXAML](https://github.com/MaterialDesignInXAML/MaterialDesignInXamlToolkit) |
| Framework | .NET 10 |
| MVVM | [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) |
| ORM | Entity Framework Core 10 |
| Database | SQL Server |
| Coding Style | Functional programming (Zoran Horvat style) |

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- SQL Server instance (tested with SQL Server 2022)

## Getting Started

### 1. Clone the repository

```bash
git clone <repo-url>
cd NoteApp
```

### 2. Configure the database connection

The connection string is stored securely using .NET User Secrets (never committed to source):

```bash
cd src/NoteApp
dotnet user-secrets set "ConnectionStrings:NoteDb" "Server=YOUR_SERVER;Database=noteDb;User Id=YOUR_USER;Password=YOUR_PASSWORD;TrustServerCertificate=True"
```

### 3. Create the database and apply migrations

```bash
# Create the database (if it doesn't exist)
sqlcmd -S "YOUR_SERVER" -U YOUR_USER -P "YOUR_PASSWORD" -Q "CREATE DATABASE noteDb" -C

# Apply EF Core migrations
cd src/NoteApp
dotnet ef database update
```

### 4. Run the application

```bash
dotnet run --project src/NoteApp
```

## Project Structure

```
NoteApp/
├── NoteApp.sln
└── src/NoteApp/
    ├── Domain/                     Pure domain layer, no dependencies
    │   ├── Functional/             Option<T>, Result<T,E>, Unit monads
    │   ├── ValueObjects/           NoteId, NoteTitle, TagName, LinkUrl
    │   ├── Models/                 Note, Tag, NoteBlock (sealed hierarchy)
    │   └── Extensions/             Map/Bind/Match extension methods
    ├── Data/                       EF Core data access
    │   ├── Entities/               Mutable EF entity classes
    │   ├── Configurations/         Fluent API table configurations
    │   └── Repositories/           INoteRepository, ITagRepository
    ├── Services/                   Business logic orchestration
    │   └── Mapping/                Entity ↔ Domain pure mapping functions
    ├── ViewModels/                 MVVM ViewModels (CommunityToolkit.Mvvm)
    ├── Views/                      XAML views
    └── Converters/                 WPF value converters
```

## Architecture & Coding Style

This project follows **functional programming patterns** inspired by [Zoran Horvat](https://www.youtube.com/@zaboravljen):

- **Immutable records** for all domain models
- **`Option<T>`** monad instead of nulls
- **`Result<T, TError>`** monad instead of exceptions for control flow
- **Sealed class hierarchies** as discriminated unions (`NoteBlock.Text | File | Link`)
- **Value objects** with factory methods returning `Result` for validation
- **Pure functions** at the core, side effects pushed to the boundaries (repositories, UI)
- **Pattern matching** via `switch` expressions and `Match` methods

## Database Schema

```
noteDb
├── Notes        — Id, Title, CreatedAt, UpdatedAt
├── NoteBlocks   — Id, NoteId (FK), BlockType (Text/File/Link), SortOrder,
│                  TextContent, FileData, FileName, FileExtension,
│                  FileSizeBytes, LinkUrl, LinkDescription
├── Tags         — Id, Name (unique)
└── NoteTags     — NoteId (FK), TagId (FK) — many-to-many junction table
```

Each note can have multiple blocks, ordered by `SortOrder`. This allows mixing text, files, and links freely.

Files are stored as `VARBINARY(MAX)` directly in the database for simpler deployment.

## License

MIT
