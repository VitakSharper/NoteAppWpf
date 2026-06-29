# NoteApp UI Redesign — Design Spec

**Date:** 2026-06-29
**Status:** Approved for implementation planning
**Scope:** Full UI redesign (shell layout + navigation + theme + note list). Styling and the `MainViewModel` navigation layer change; services, repositories, domain models, and block types are untouched.

---

## 1. Motivation

The current UI feels primitive: a `DrawerHost` hamburger drawer plus full-screen view swapping, MD2 styling, and a stock Indigo/Teal palette. We are doing a full redesign — not a theme refresh — to a modern three-pane workspace with a coherent "Modern Violet" identity on Material Design 3.

This document captures the decisions locked during brainstorming (2026-06-26 → 2026-06-29) and the architectural changes they require.

## 2. Locked Decisions

| Area | Decision |
|---|---|
| Layout | **Three-pane**: thin icon rail · note list · editor — all visible at once |
| Color | **"Modern Violet"** — accent `#6c79ff`, deep navy rail `#20283d`, crisp white surfaces |
| Design system | **Material Design 3** (rounded corners, soft surfaces); currently MD2 |
| Dark mode | Ship a matching dark theme, wired to the existing Settings toggle |
| List density | **Comfortable** — title + 1-line preview + tag chips + date + small block-type dots |
| Rail nav behavior | **Mixed** — Tags replaces only the middle pane (tag manager); Settings opens as a centered modal dialog |

## 3. Current Architecture (baseline)

- `MainWindow.xaml` — `DialogHost` (`Identifier="RootDialog"`) wrapping a `DrawerHost`. The drawer holds nav buttons (Notes / Tags / Settings); the body is a top `ColorZone` app bar + a single `ContentControl Content="{Binding CurrentView}"`.
- `MainViewModel` — a single `CurrentView` (`ObservableObject?`) that swaps the **whole** content area between `NoteListViewModel`, `TagManagerViewModel`, `SettingsViewModel`, and a transient `NoteEditorViewModel`. Nav commands (`NavigateToNotes/Tags/Settings`) and editor lifecycle (`OnEditNoteRequested`, `OnNoteSaved`, `OnEditorCancelled`) all reassign `CurrentView`.
- `NoteListView.xaml` — search card, type/tag filters, a collapsible tag-filter panel, and **two** mutually exclusive note presentations toggled by `IsListView`: a `WrapPanel` of 320px `Card`s and a multi-column `DataGrid`.
- `App.xaml` — MD2: `BundledTheme BaseTheme="Light" PrimaryColor="Indigo" SecondaryColor="Teal"` + `MaterialDesign2.Defaults.xaml`.
- Leaf views: `NoteEditorView.xaml`, `SettingsView.xaml`, `TagManagerView.xaml`, `PasswordDialog.xaml`.

**Why the navigation must change:** a single `CurrentView` can only show one thing at a time. The three-pane goal (list + editor visible together, Tags swapping only the middle pane, Settings as a modal) cannot be expressed by it.

## 4. Target Architecture

### 4.1 Shell (`MainWindow.xaml`)

Replace `DrawerHost` + hamburger with a three-column `Grid` inside the existing `DialogHost`:

```
┌────┬───────────────┊──────────────────────────┐
│rail│  middle pane  ┊       editor pane         │
│ 64 │   ~320–360    ┊           *               │
│ px │   (fixed)     ┊  ┊ = GridSplitter          │
└────┴───────────────┊──────────────────────────┘
```

- **Icon rail** (col 0, fixed ~64px, navy `#20283d`, no splitter): app glyph at top; vertical icon buttons — Notes, Tags — bound to nav commands with a selected-state indicator; a "+" New-note action; Settings pinned at the bottom. Tooltips on hover (labels optional/expandable later — not in scope now).
- **Middle pane** (col 1, ~320–360px, resizable via the single `GridSplitter` between it and the editor): hosts `MiddlePaneContent` via a `ContentControl` with the existing `DataTemplate` mapping (`NoteListViewModel` → `NoteListView`, `TagManagerViewModel` → `TagManagerView`).
- **Editor pane** (col 2, `*`): hosts `CurrentEditor` via a `ContentControl` — either a `NoteEditorViewModel` or an empty-state placeholder.
- `Snackbar` (`MessageQueue`) and `DialogHost` are retained.

### 4.2 Navigation (`MainViewModel`)

Replace the single `CurrentView` with two surfaces:

- **`MiddlePaneContent : ObservableObject?`** — toggles between `NoteListViewModel` and `TagManagerViewModel`.
  - `NavigateToNotes()` → `MiddlePaneContent = NoteListViewModel`
  - `NavigateToTags()` → `MiddlePaneContent = TagManagerViewModel` + `LoadTagsCommand`
- **`CurrentEditor : ObservableObject?`** — the right pane.
  - Default: an **empty-state** ("Select a note, or create one"). Modeled as either a dedicated lightweight `EditorPlaceholderViewModel` or a `null`-driven `DataTrigger` in XAML (implementation-plan choice; prefer the XAML empty-state to avoid a new VM).
  - `OnEditNoteRequested` / `CreateNote` / `OnNoteSaved` set `CurrentEditor` instead of `CurrentView`.
  - `OnEditorCancelled` resets `CurrentEditor` to the empty-state and **leaves the list pane intact**.
- **Settings** → opens as a modal over `RootDialog` via `DialogHost.Show(...)` (or an `IsSettingsOpen` bound `DialogHost`), rendering `SettingsView`. `NavigateToSettings()` opens it; the existing `SettingsViewModel.CloseRequested` closes the dialog (rather than reassigning `CurrentView`).
- **Selection ↔ editor:** selecting a note in the list opens it in `CurrentEditor`. Encrypted notes keep the existing `PasswordDialog` unlock flow before populating the editor.
- **Startup (`LaunchPage`):** `StartupPage.Notes` and `.Tags` set `MiddlePaneContent`; `StartupPage.Settings` opens the Settings dialog on launch over the Notes pane.

These are orchestration changes only. `NoteListViewModel`, `NoteEditorViewModel`, `SettingsViewModel`, and `TagManagerViewModel` keep their public surfaces; their events (`EditNoteRequested`, `CreateNoteRequested`, `SaveCompleted`, `CancelRequested`, `ShowMessage`, `CloseRequested`) are re-wired to the new properties.

### 4.3 Theme (`App.xaml` + new `Theme/` dictionaries)

- Swap `MaterialDesign2.Defaults.xaml` → `MaterialDesign3.Defaults.xaml`.
- Set the primary color to the Modern Violet accent (`#6c79ff`); choose a complementary secondary.
- Add `Theme/ModernViolet.xaml` (palette brushes, rail/surface brushes, corner-radius + elevation tokens) merged in `App.xaml`.
- **Dark mode:** wire the existing Settings dark toggle to switch the MD3 base theme (Light/Dark) and swap rail/surface brush sets. Persist via the existing `AppSettingsService`.

### 4.4 Note list — Comfortable density (`NoteListView`)

- Replace the card `WrapPanel` **and** the `DataGrid` with a **single-column list** (`ListBox`/`ItemsControl`) sized for the narrow middle pane. **Remove the `IsListView` card/list toggle.**
- Each row (Comfortable): note-type/lock icon · **title** (medium) · **1-line preview snippet** (trimmed) · **tag chips** · **short date** (e.g., "Jun 27") · small block-type dots (text/file/link) when present. Encrypted notes show a lock + "Encrypted" instead of a preview.
- Retain the search card, type filters, and the collapsible tag-filter panel above the list.
- Selecting a row sets `CurrentEditor`; the row shows a selected highlight using the violet accent.
- **Preview snippet (new):** a small, list-only addition — a computed `Preview` string derived from the first text block (or a thin list-item wrapper). It does not alter the domain `Note` model's persisted shape.
- Recolor tag chips from the current green (`#C8E6C9`/`#2E7D32`) to violet-accent tokens.

### 4.5 Editor & dialogs

- `NoteEditorView` — restyle for the right pane: MD3 rounded surfaces, accent-colored primary actions, comfortable padding. No change to block editing logic.
- `PasswordDialog` — restyle to match MD3 + Modern Violet.
- `TagManagerView` — restyle to sit naturally in the middle pane.

## 5. Components & Boundaries

| Unit | Responsibility | Changes |
|---|---|---|
| `MainWindow.xaml` | Three-pane shell + rail + dialog host | Rewritten layout |
| `MainViewModel` | Orchestrate middle pane, editor pane, Settings dialog | Navigation rework |
| `App.xaml` + `Theme/*` | MD3 + Modern Violet + dark mode | New theme dictionaries |
| `NoteListView(.xaml)` | Comfortable single-column list | List template rewrite; toggle removed; preview added |
| `NoteEditorView` | Right-pane editor | Restyle only |
| `SettingsView` | Settings content rendered in a modal | Host change + restyle |
| `TagManagerView` | Tag manager in middle pane | Restyle only |
| `PasswordDialog` | Encryption unlock/set | Restyle only |

## 6. Non-Goals

- No changes to `EncryptionService`, `BackupService`, repositories, `NoteService`, domain models, value objects, or `NoteBlock` types.
- No EF migration (no schema change).
- No new note features (no new block types, no PDF export work).
- Rail label expansion / collapsible rail text is deferred (icons + tooltips only for now).

## 7. Risks & Considerations

- **Editor empty-state vs. always-open:** prefer a XAML-driven empty-state on `null CurrentEditor` to avoid an extra VM; confirm during planning.
- **Unsaved-edit handling on selection change:** selecting a different note while an editor has unsaved changes — current code recreates the editor per open; preserve/confirm existing save semantics, don't silently discard.
- **Window min-width:** three panes need a larger `MinWidth` than today's 800 to avoid cramped panes; revisit during layout.
- **Dark mode brush coverage:** ensure rail, surfaces, tag chips, and DataGrid-replacement list all have dark variants.

## 8. Implementation Order (for the plan)

1. Theme foundation: MD3 swap + `Theme/ModernViolet.xaml` (light), build green.
2. Shell: `MainWindow.xaml` three-pane grid + rail (static content).
3. `MainViewModel` navigation rework (middle pane + editor pane + Settings dialog) — re-wire events.
4. `NoteListView` Comfortable single-column list + preview snippet; remove toggle.
5. Restyle `NoteEditorView`, `TagManagerView`, `PasswordDialog`.
6. Dark mode wiring + Settings toggle.
7. Manual verification pass (run app, exercise nav, encryption, dark mode).
