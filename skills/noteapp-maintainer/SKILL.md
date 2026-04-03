---
name: noteapp-maintainer
description: Maintain or extend the NoteApp WPF desktop application in this repository. Use when Codex needs to add features, fix bugs, review code, explain architecture, or update documentation for the .NET 10, WPF, EF Core, and SQL Server codebase, especially around multi-block notes, encrypted notes, MVVM view-models, Material Design XAML views, functional domain models, repositories, mappings, or EF migrations.
---

# NoteApp Maintainer

## Overview

Work from the real code and keep the existing architectural boundaries intact. Preserve the functional domain model, the multi-block note model, and the coordination between WPF views, view-models, services, repositories, mappings, entities, and migrations.

## Start Here

1. Read `README.md` for the high-level feature set and setup flow.
2. Read `references/codebase-map.md` for the actual file map, change impact areas, and command caveats.
3. Prefer the code and the README over `plan.md`.

`plan.md` is partially stale: it still describes the older single-content-note model and does not capture the encryption changes that are present in the current code.

## Preserve These Invariants

- Keep `Domain/` pure and immutable.
- Use `Option<T>` and `Result<T, AppError>` for expected outcomes instead of exceptions for normal flow.
- Treat `NoteBlock` as the source of truth for note content types, even though the file is still named `Domain/Models/NoteContent.cs`.
- Preserve block ordering through `SortOrder` and keep repository and mapper ordering aligned.
- Keep side effects at the boundaries: repositories, services, dialogs, file I/O, and WPF event bridges.
- Keep encrypted-note behavior consistent: encrypted notes persist `EncryptedContent` and clear plaintext blocks in storage, then restore blocks through `NoteService.UnlockNoteAsync`.

## Coordinate Changes Across Layers

### Change note structure or block types

- Update `Domain/Models/Note.cs` and `Domain/Models/NoteContent.cs`.
- Update `Data/Entities/NoteEntity.cs` and `Data/Entities/NoteBlockEntity.cs`.
- Update `Services/Mapping/NoteMapper.cs`.
- Update `Data/Repositories/NoteRepository.cs` search, persistence, and retrieval logic.
- Update `ViewModels/NoteEditorViewModel.cs`, `Views/NoteEditorView.xaml`, and any list/detail rendering.
- Add or update EF migrations under `Migrations/` when persistence changes.

### Change encryption or password flow

- Update `Services/EncryptionService.cs`, `Services/NoteService.cs`, and `Data/Repositories/NoteRepository.cs` together.
- Update `ViewModels/NoteEditorViewModel.cs`, `ViewModels/MainViewModel.cs`, `Views/PasswordDialog.xaml`, and `Views/PasswordDialog.xaml.cs` together.
- Verify both create/edit and unlock flows.

### Change search, filtering, or tags

- Update `ViewModels/NoteListViewModel.cs` and `Data/Repositories/NoteRepository.cs`.
- Inspect `Services/SearchService.cs` only if the task actually routes through it; current search calls also flow through `NoteService.SearchAsync`.
- Keep tag persistence aligned with `Data/Repositories/TagRepository.cs` and `Data/Entities/NoteTagEntity.cs`.

### Change startup, DI, or configuration

- Update `App.xaml.cs` first.
- Keep the user secret key `ConnectionStrings:NoteDb` intact unless the task explicitly changes configuration shape.

## Validate Safely

- Run `dotnet build NoteApp.slnx` from the repo root when package restore is available.
- Set `DOTNET_CLI_HOME` to a writable directory before `dotnet` commands in sandboxed environments.
- Run `dotnet user-secrets set "ConnectionStrings:NoteDb" "..."` from `src/NoteApp` before database operations.
- Run `dotnet ef database update` from `src/NoteApp` after migration changes.
- If restore or build is blocked by sandbox or offline NuGet access, state that explicitly and limit validation to static inspection.

## Reference

Read `references/codebase-map.md` before making non-trivial changes. It contains the current file map, command examples, and repo-specific caveats that are not obvious from the README alone.
