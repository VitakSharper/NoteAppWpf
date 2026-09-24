# NoteApp

[![CI](https://github.com/VitakSharper/NoteAppWpf/actions/workflows/ci.yml/badge.svg)](https://github.com/VitakSharper/NoteAppWpf/actions/workflows/ci.yml)

A WPF desktop application for managing notes, built with .NET 10, SQL Server, and Material Design.

## Features

- **Multi-block notes** — Each note can contain multiple content blocks: rich text, files, links, checklists, code and secrets — all in one note
- **Checklists** — Tickable items inside a note: Enter adds the next one, `Alt+Up/Down` move one, done items are struck through (or hidden with Hide done), the block counts progress, and item texts are searchable
- **Rich text editing** — Bold, italic, underline, strikethrough, highlighter, inline code, headings (H1–H3), bullet and numbered lists via a toolbar; Markdown line starters work as you type (`# `…`### ` heading, `- ` bullets, `1. ` numbering, `[] ` turns the line into a checklist block), and every export keeps the formatting
- **Code blocks** — Monospace text kept exactly as typed (tabs included, never turned into links) for commands, SQL or configuration, with a copy button; searchable, and exported as a fenced block / a monospace listing
- **Secret blocks** — A login inside a note: what it is for, the user name, the password (masked; the eye reveals it) and an address. The copy buttons put the user name or the password on the clipboard kept out of Windows' clipboard history and cloud clipboard, and wipe it after 30 s. The password is never searched, previewed, exported or kept in a draft, and a secret in a note that is not encrypted says that its password is stored as plain text
- **Clickable links** — Web addresses (`http://`, `https://`, `www.`) in text blocks become links as soon as a space or a new line follows them; a click opens them in the browser (`Alt+Click` edits the link text). In checklist items and link blocks, where a click edits, `Ctrl+Click` or the open button does
- **Links between notes** — Type `[[` in a text block and pick a note by its title: the link opens that note (a click, like a web link), and the note it points to lists the notes linking to it under its title ("Linked from")
- **File attachments** — Browse and attach files stored directly in the database
- **Block ordering** — Drag a block by its grip, or use the up/down arrows, to reorder the blocks of a note
- **Drop files** — Drop files from Explorer onto an open note to attach them as file blocks
- **Pinned notes** — Right-click a note › Pin to top: it stays first in the list whatever the sort
- **Tagging system** — Create, rename, colour and delete tags and assign them to notes; each tag's chip shows its colour in the list and the filters
- **Date headings** — Under a date sort the list is grouped: Pinned, Today, Yesterday, Previous 7 / 30 days, then by month
- **Search & filter** — Search by title/content, filter by tags or note type
- **Full CRUD** — Create, read, update, delete notes and tags
- **Trash with undo** — Deleting a note moves it to the trash and the snackbar offers UNDO; the Trash view restores or deletes forever, and only the permanent gestures ask for confirmation
- **Draft recovery** — While a note has unsaved changes a draft is kept every 20 s (never for encrypted notes, nor for notes holding a secret); after a crash NoteApp offers to restore it at the next start
- **Unsaved-changes guard** — Leaving a modified note (opening another one, New note, Cancel, closing the window) offers to save, discard, or stay in the editor
- **Keyboard shortcuts** — `Ctrl+N` new note, `Ctrl+S` save, `Esc` close the editor (or the Settings dialog), `Ctrl+F` search the text block under the cursor or, from anywhere else, the note list; `Ctrl+B/I/U` are native to the rich text box, `Ctrl+Shift+X/H/C` strike, highlight, code; a click (text blocks) or `Ctrl+Click` (anywhere) opens a link; `F11` focus mode (the editor takes the whole window); `Ctrl+wheel`, `Ctrl+Plus/Minus` zoom the blocks, `Ctrl+0` resets
- **In-app help** — `F1` or the `?` in the rail opens a non-modal help window: getting started, the shortcut table, blocks, search and tags, encryption, backup, and where settings and logs live
- **Encrypted notes** — Optional per-note password; blocks are stored AES-GCM encrypted (PBKDF2 key derivation). Text copied out of an encrypted note stays out of the clipboard history and is wiped after 30 s
- **Automatic lock** — An open encrypted note closes itself after a configurable idle period (default 5 minutes, `Never` to disable), and at once when Windows locks (`Win+L`), the remote session disconnects or the computer goes to sleep (a setting too) — saving unsaved changes first and forgetting the password
- **PDF, Word & Markdown export** — Export a note to PDF (QuestPDF), `.docx` (Open XML SDK) or `.md` (CommonMark, images in a folder beside it): text with formatting and images, checklists as ticked boxes, clickable links (link blocks, and addresses in text and checklist items), attachments listed by name and size, and secrets without their password
- **Encrypted backups** — BACPAC export of the database packed into an AES-256 zip, by hand or automatically (daily or weekly, checked at startup and hourly, keeping the last N)
- **Material Design UI** — Three-pane layout (icon rail · note list / tags · editor), cards, chips, snackbar, light/dark theme

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

### 5. Run the tests

```bash
dotnet test NoteApp.slnx
```

`NoteApp.Tests` covers the pure layers (functional core, value objects, mapping, encryption, preview, view models) and needs neither a database nor a UI thread. `NoteApp.UiTests` drives the real WPF views off-screen on a dedicated STA thread — typing, links, lists — still without a database.

### 6. Run a test instance (optional)

Environment variables prefixed `NOTEAPP_` and command-line arguments override the user secrets, so a second instance can run against a scratch database without touching your notes:

```powershell
$env:NOTEAPP_ConnectionStrings__NoteDb = "Server=YOUR_SERVER;Database=noteDb_test;...;TrustServerCertificate=True"
$env:NOTEAPP_DataFolder = "C:\Temp\NoteAppTest"   # its own settings.json, crash.log, drafts and backups
dotnet ef database update --project src/NoteApp  # the design-time factory reads the same variable
dotnet run --project src/NoteApp                 # or: NoteApp.exe --ConnectionStrings:NoteDb="..." --DataFolder="..."
```

The window title then reads `NoteApp — noteDb_test (test instance)`.

## Project Structure

```
NoteApp/
├── NoteApp.slnx
├── src/NoteApp/
│   ├── Domain/                     Pure domain layer, no dependencies
│   │   ├── Functional/             Option<T>, Result<T,E>, Unit monads
│   │   ├── ValueObjects/           NoteId, NoteTitle, TagName, LinkUrl
│   │   ├── Models/                 Note, NoteSummary, Tag, NoteBlock (sealed hierarchy)
│   │   └── Extensions/             Map/Bind/Match extension methods
│   ├── Data/                       EF Core data access
│   │   ├── Entities/               Mutable EF entity classes
│   │   ├── Configurations/         Fluent API table configurations
│   │   ├── Queries/                Read-model rows projected for the list
│   │   └── Repositories/           INoteRepository, ITagRepository
│   ├── Services/                   Orchestration, encryption, backup, PDF export
│   │   └── Mapping/                Entity ↔ Domain pure mapping functions
│   ├── ViewModels/                 MVVM ViewModels (CommunityToolkit.Mvvm)
│   ├── Views/                      XAML views
│   ├── Theme/                      Modern Violet MD3 resources
│   └── Converters/                 WPF value converters
└── tests/NoteApp.Tests/            xUnit tests for the pure layers
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
├── Notes        — Id, Title, IsEncrypted, EncryptedContent, CreatedAt, UpdatedAt, DeletedAt (soft delete)
├── NoteBlocks   — Id, NoteId (FK), BlockType (Text/File/Link/Checklist/Secret/Code), SortOrder,
│                  TextContent (rich), PlainText (searchable), FileData, FileName,
│                  FileExtension, FileSizeBytes, LinkUrl, LinkDescription
├── Tags         — Id, Name (unique)
└── NoteTags     — NoteId (FK), TagId (FK) — many-to-many junction table
```

Each note can have multiple blocks, ordered by `SortOrder`. This allows mixing text, files, and links freely.

Files are stored as `VARBINARY(MAX)` directly in the database for simpler deployment. Encrypted notes keep their blocks as an AES-GCM payload in `Notes.EncryptedContent` and have no `NoteBlocks` rows.

## License

MIT
