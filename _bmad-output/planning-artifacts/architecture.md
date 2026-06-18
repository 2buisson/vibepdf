---
stepsCompleted: [1, 2, 3, 4, 5, 6, 7, 8]
lastStep: 8
status: 'complete'
completedAt: '2026-06-14'
inputDocuments:
  - _bmad-output/planning-artifacts/prds/prd-pdf-junior-2026-06-14/prd.md
  - _bmad-output/planning-artifacts/prds/prd-pdf-junior-2026-06-14/addendum.md
  - _bmad-output/planning-artifacts/ux-designs/ux-pdf-junior-2026-06-14/DESIGN.md
  - _bmad-output/planning-artifacts/ux-designs/ux-pdf-junior-2026-06-14/EXPERIENCE.md
workflowType: 'architecture'
project_name: 'pdf-junior'
user_name: 'Antoine'
date: '2026-06-14'
---

# Architecture Decision Document

_This document builds collaboratively through step-by-step discovery. Sections are appended as we work through each architectural decision together._

## Project Context Analysis

### Requirements Overview

**Functional Requirements:** 12 FRs in 4 capability groups.
- *File List Management (FR-1–FR-4)* — add via native multi-select picker with
  duplicate-skip; async per-file validation (checking/valid/error-password/
  error-corrupt + page count); remove; reorder via native ListView drag-and-drop.
  Architecturally: an observable, ordered in-memory collection bound to a ListView
  (drag-reorder mutates the collection directly), with an async validation pipeline
  feeding a per-item state machine.
- *PDF Preview (FR-5)* — read-only, fit-to-width, vertical-scroll render of the
  selected file; placeholder/exclusion states for checking/flagged. Architecturally:
  page-to-bitmap rendering via Windows.Data.Pdf into a scrollable WinUI surface.
- *Merge & Output (FR-6–FR-9)* — Merge gating, native Save dialog for destination,
  off-thread merge in display order, progress after 2s, success banner with Open
  folder. Architecturally: a cancellable background merge operation with progress
  marshaled to the UI thread.
- *Error Handling & Safety (FR-10–FR-12)* — invalid-state blocking, descriptive
  failure with no partial output, window-close guard. Architecturally: an error
  taxonomy mapping exceptions to canonical microcopy, atomic write-then-move, and
  cooperative cancellation with temp-file cleanup.

**Non-Functional Requirements:** 7 NFRs drive the core decisions.
- NFR-1 Non-blocking: off-UI-thread merge/validation, no count/size limit,
  progress only after 2s → threading & cancellation model.
- NFR-2 Privacy / local-only & NFR-4 No account & §9.1 No persistence:
  in-memory-only state, no network, no telemetry, picker-mediated access only.
- NFR-3 Store compliance: MSIX, minimal capabilities (no broadFileSystemAccess),
  Windows 11 minimum, must pass certification.
- NFR-5 Startup < 3s; NFR-6 Installed < 100 MB (HARD constraint — gates library
  choice); NFR-7 public privacy policy before submission.

**Scale & Complexity:**
- Primary domain: native Windows desktop — C# / WinUI 3 (Windows App SDK).
- Complexity level: Low scope / moderate concurrency-correctness rigor.
- Estimated architectural components: ~6 (App/Window shell, File-list view-model
  + collection, Validation service, Preview/render service, Merge service,
  Picker/IO adapters).

### Technical Constraints & Dependencies

- Stack fixed: C# / WinUI 3, MVVM (INotifyPropertyChanged/ObservableObject),
  native FileOpenPicker/FileSavePicker, native WinUI controls, System.Guid IDs.
- OPEN: .NET PDF merge library (PDFsharp/MigraDoc, QuestPDF, UglyToad PdfPig,
  others) — must support whole-file concatenation, fit the <100 MB budget,
  and allow cooperative cancellation + atomic write-then-move.
- Validation floor: Windows.Data.Pdf.PdfDocument distinguishes encrypted vs
  unparseable and yields page counts — safe even if the merge library differs.
- OPEN: test stack (MSTest/xUnit/NUnit) and whether WinAppDriver is warranted.
- MSIX via Windows Application Packaging Project; Windows 11 minimum.

### Cross-Cutting Concerns Identified

- Threading & cancellation: off-UI-thread validation (with per-file wall-clock
  guard) and merge execution; CancellationToken plumbing; UI-thread marshaling.
- Atomic file I/O: write-to-temp-then-move; guaranteed no partial output on
  failure or cancellation; temp cleanup.
- Privacy & Store compliance: local-only, no network/telemetry, minimal
  capabilities, no persistence between sessions.
- Install-size budget (<100 MB): Windows App SDK + PDF library footprint tracked
  from day one.
- Error taxonomy: exception → canonical user-facing microcopy mapping.
- In-memory-only state: no persistence layer; reset to defaults each launch.

## Starter Template Evaluation

### Primary Technology Domain

Native Windows 11 desktop — C# / WinUI 3 (Windows App SDK). Stack is fixed by
the PRD addendum; this step selects the scaffold, not the stack.

### Verified Current Versions (web-checked, June 2026)

- Windows App SDK: 2.2.0 (Stable, 2026-06-09) — 2.x line, targets .NET 10.
- .NET: 10.0.x (LTS) — ships with Visual Studio 2026; supported to Nov 2028.
- CommunityToolkit.Mvvm: 8.4.2 (2026-03-25).
- Visual Studio 2026 (v18.x) with the Windows App SDK / .NET Desktop workloads.

### Starter Options Considered

- VS "WinUI Blank App (Packaged)" + CommunityToolkit.Mvvm — minimal, official,
  single-project MSIX. SELECTED.
- Template Studio for WinUI (C#) v5.4 — rejected: scaffolds NavigationView,
  Settings page, theme service, and LocalSettings persistence, all of which the
  PRD explicitly forbids (no nav chrome §11, no settings/theme toggle, no
  persistence §9.1).
- dotnet new WinUI templates — preview only; VS template preferred for support.

### Selected Starter: VS "WinUI Blank App (Packaged)" + CommunityToolkit.Mvvm

**Rationale for Selection:**
Smallest viable, fully-supported foundation. Single-project MSIX needs no
separate packaging project and is Store-ready. Importing nothing beyond the
MVVM toolkit keeps the install footprint near the WinUI/App-SDK floor (NFR-6,
<100 MB) and avoids scaffolding that contradicts the product's no-nav,
no-settings, no-persistence design.

### Current State: Project Already Scaffolded

The repository already contains `pdfjunior/pdfjunior.csproj`, generated from the
"WinUI Blank App (Packaged)" template — single-project MSIX (`EnableMsixTooling`,
`Msix` ProjectCapability, no separate WAP project), already referencing
`Microsoft.WindowsAppSDK 2.2.0`, with Mica wired in `MainWindow.xaml`, x86/x64/
ARM64 platforms + publish profiles, and `PublishTrimmed=True` on Release. So the
starter is in place; the first implementation story *aligns* it rather than
creating it.

**First implementation story — align the existing project:**

```text
pdfjunior.csproj is the starter (WinUI Blank App, Packaged; single-project MSIX;
Windows App SDK 2.2.0). Alignment fixes:
  - TargetFramework         net8.0-windows10.0.19041.0 → net10.0-windows10.0.22621.0  (.NET 10)
  - TargetPlatformMinVersion 10.0.17763.0 (Win10 1809) → 10.0.22000.0                 (Windows 11, NFR-3)
  - Add NuGet: CommunityToolkit.Mvvm 8.4.2
```

**Architectural Decisions Provided by Starter:**

- **Language & Runtime:** C# on .NET 10 (after alignment); WinUI 3 via Windows
  App SDK 2.2.0.
- **Packaging:** Single-project MSIX (no separate WAP project), Store-ready;
  minimal capability declarations (file access only — no broadFileSystemAccess).
- **UI framework:** Native WinUI 3 / Fluent; Mica; OS light/dark theme.
- **State/MVVM:** CommunityToolkit.Mvvm (ObservableObject, [ObservableProperty],
  RelayCommand) — in-memory only, no persistence layer.
- **Build tooling:** MSBuild/SDK-style project; Debug/Release; Release trimmed +
  ReadyToRun (supports the <100 MB / startup goals — verify trimming behaves
  with WinUI at packaging time).
- **Code organization:** single project; structure (Views/ViewModels/Services)
  decided in the architecture decisions step.
- **Testing:** framework chosen in a later step (MSTest/xUnit/NUnit — open).

**Note:** Single-project MSIX supersedes the addendum's "separate Windows
Application Packaging Project" note as a modern simplification (already reflected
in the scaffolded project).

## Core Architectural Decisions

### Decision Priority Analysis

**Critical Decisions (Block Implementation):**
- PDF library strategy (merge vs validation/preview split).
- Output / file-I/O write model under the MSIX sandbox.
- Concurrency & cancellation model (validation pipeline + merge execution).

**Important Decisions (Shape Architecture):**
- App structure: MVVM layering + dependency injection.
- Preview rendering approach.
- Error taxonomy (exception → canonical microcopy).
- Test stack.

**Deferred Decisions (Post-MVP):**
- Page-range selection (PRD §6.2 — possible v1.1).
- WinAppDriver / UI automation (accessibility & UI-automation out of scope, PRD §5).
- Any accessibility work (explicit v1 non-goal).

### Application State & Data Model (no database)

- No database, no persistence layer (NFR-2, §9.1) — in-memory only, reset every launch.
- Domain model: `PdfFileItem { Guid Id; string Path; string DisplayName;
  ValidationStatus Status; int? PageCount }`, where
  `ValidationStatus = { Checking, Valid, ErrorPassword, ErrorCorrupt }`.
- File list = `ObservableCollection<PdfFileItem>`, bound to a `ListView` (single-select).
  Merge consumes this collection in display order.
- State machine: `Checking → Valid | ErrorPassword | ErrorCorrupt` (terminal).
- Merge-enabled is a derived property: ≥1 Valid, 0 Flagged, 0 Checking (FR-10).

### Privacy, Capabilities & Sandbox (no auth)

- No authentication/accounts (NFR-4).
- MSIX minimal capabilities: file access via the broker (pickers) **only**;
  **no `broadFileSystemAccess`** (§9.1, NFR-3).
- No network in normal operation; no telemetry/analytics (NFR-2).
- All file access flows through picker-granted `StorageFile`/`StorageItem` —
  never raw paths.

### PDF Engine & Library Strategy  *(resolves PRD Open Question 1)*

- **Merge engine: PDFsharp 6.2.4** (MIT, pure-managed). Open each Valid source
  with `PdfDocumentOpenMode.Import`, append its pages into a new output
  `PdfDocument` in File-list order, save to a stream. Pure-managed → negligible
  footprint against the <100 MB cap (NFR-6); MIT → clean for a free Store app.
- **Validation + page count + preview: `Windows.Data.Pdf.PdfDocument`** (in-box,
  zero footprint). `LoadFromFileAsync` raises a distinct error for encrypted
  files (→ `ErrorPassword`) vs unparseable (→ `ErrorCorrupt`), and exposes
  `PageCount` for Valid files. Preview renders pages via `PdfPage.RenderToStreamAsync`.
- **Rationale for the split:** Windows.Data.Pdf is the only zero-cost component
  that cleanly distinguishes password-vs-corrupt *and* renders preview; PDFsharp
  is the smallest license-clean whole-file merger. Together they stay far under
  the install-size cap. (QuestPDF rejected: can't merge existing PDFs + revenue
  license; iText rejected: AGPL.)
- **Residual risk:** a file Windows.Data.Pdf accepts could still fail in PDFsharp
  at merge time → handled by the FR-11 failure path (descriptive error), not by
  pre-validation.

### App Structure, MVVM & Dependency Injection

- **MVVM via CommunityToolkit.Mvvm 8.4.2** (`ObservableObject`,
  `[ObservableProperty]`, `[RelayCommand]`).
- **DI: Microsoft.Extensions.DependencyInjection (10.0.x)** — composition root in
  `App`, services registered (singletons), `MainViewModel` resolved from the
  container.
- **Layering:** `Views/` (XAML) · `ViewModels/` · `Services/` (interface-based) ·
  `Models/`. Single project, single window, no navigation/routing.
- **Service interfaces** (all async, off-UI-thread; testable):
  - `IPdfValidationService` — Windows.Data.Pdf classification + page count.
  - `IPdfPreviewService` — page-to-bitmap rendering (Windows.Data.Pdf).
  - `IPdfMergeService` — PDFsharp merge; takes `IProgress<double>` + `CancellationToken`.
  - `IFilePickerService` — `FileOpenPicker`/`FileSavePicker` wrappers (HWND-aware).
  - `IFolderLauncher` — open Explorer to the output folder (`Launcher`).
  - `IOutputWriter` — writes the merged document directly to the destination stream.
- **UI marshaling:** services run on background threads; UI state updates marshaled
  via `DispatcherQueue` / `IProgress<T>`.

### Concurrency & Cancellation Model

- **Validation pipeline:** each added file validated on a background thread
  (`Task.Run`) with **bounded concurrency** (`SemaphoreSlim`, small cap) so adding
  many files doesn't thrash. A **per-file wall-clock guard**
  (`CancellationTokenSource(timeout)`) resolves a hung parse to `ErrorCorrupt`.
  Threshold = a named constant, **default 5 s**, tuned so large
  valid PDFs aren't false-flagged (FR-2, NFR-1).
- **Merge execution:** single `Task.Run` + `CancellationToken`. Progress reported
  via `IProgress<double>`, **determinate by file count** (we own the per-file
  import loop), marshaled to the UI. The progress bar appears **only after 2 s**
  (timer); shorter merges show nothing (FR-8, NFR-1).
- **Cooperative cancellation:** token checked between file imports; on cancel the
  merge stops writing (no rollback/cleanup — see Output safety). Drives FR-12.

### Output / File-I/O Safety (FR-11 — partial output permitted, no temp file)

Per the 2026-06-14 PRD change, FR-11 no longer guarantees atomicity — a partial,
incomplete, or zero-byte file may remain on failure/cancellation. Temp-file
staging is therefore not used:

- The merge writes **directly to the destination StorageFile** from the
  FileSavePicker. No temp-file staging, no rollback, no post-failure cleanup.
- PDFsharp builds the merged document and `Save()`s straight to the destination
  stream, wrapped in `CachedFileManager.DeferUpdates`/`CompleteUpdatesAsync` (the
  standard picker-write pattern).
- On failure/cancellation (unreadable source, disk full, Close-anyway): surface a
  descriptive error (see Error Taxonomy); a partial/incomplete file may remain at
  the destination — the app does not remove it. The File list is preserved; a
  successful retry to the same name overwrites it.
- **Cooperative cancellation (`CancellationToken`) is still implemented** for the
  window-close guard (FR-12) so a long merge can be stopped — it simply stops
  writing, with no cleanup obligation.
- Overwrite confirmation remains OS-owned (native Save dialog).

### Error Taxonomy (exception → canonical microcopy)

- Services return a `MergeOutcome` result (`Success(path)` | `Failure(reason)`)
  rather than throwing across layers.
- Mapping to the **exact EXPERIENCE.md strings**:
  - disk full (`IOException`, HRESULT 0x70/0x27) → `Not enough space on {drive}.`
  - `UnauthorizedAccessException` → `Access denied`
  - source vanished (`FileNotFoundException`) → `File not found: {name}`
  - otherwise → `Merge failed. Try again or check the files.`
  - open-folder when missing → `Folder not found`

### Test Stack

- **xunit.v3 3.2.2** (Microsoft Testing Platform integration on .NET 10).
- Unit tests target **ViewModels + Services behind interfaces**: validation
  classification, merge-enabled gating (FR-10), file ordering, and error mapping.
  PDF fixtures: valid multi-page, encrypted, and corrupt (image-renamed-to-`.pdf`).
- **No WinAppDriver / UI automation in v1** (UI-automation & accessibility are
  explicit non-goals, PRD §5).

### Decision Impact Analysis

**Implementation Sequence:**
1. Align project + add packages (CommunityToolkit.Mvvm, PDFsharp,
   Microsoft.Extensions.DependencyInjection; test project on xunit.v3).
2. Models + validation state machine + `MainViewModel` + DI composition root.
3. Services: validation & preview (Windows.Data.Pdf), merge (PDFsharp), pickers,
   output writer.
4. Views + binding; merge-enabled gating; progress/cancellation; banners; close-guard.
5. Direct-write output + error mapping; unit tests.

**Cross-Component Dependencies:**
- Merge-enabled logic depends on the validation status of every list item.
- Output writer, cancellation, and error taxonomy are coupled (FR-8/11/12).
- Preview renderer and validation both use Windows.Data.Pdf → share one thin wrapper.

## Implementation Patterns & Consistency Rules

### Critical Conflict Points Identified

For a single-project C#/WinUI/MVVM app, agent divergence risk concentrates in:
naming, file/namespace layout, MVVM mechanics, async/threading discipline, DI
wiring, error/result conventions, UI-string sourcing, and XAML theming. (No DB,
API, events, or wire formats exist, so those template categories are N/A.)

### Naming Patterns (C#)

- Types, methods, properties, public members: **PascalCase**. Local vars &
  parameters: **camelCase**. Private fields: **`_camelCase`**. Constants:
  **PascalCase**. Interfaces: **`I`-prefixed** (`IPdfMergeService`).
- Async methods end in **`Async`**; `CancellationToken` is the **last parameter**.
- One public type per file; **file name = type name**; **namespace = folder path**
  rooted at `pdfjunior` (e.g. `pdfjunior.Services`, `pdfjunior.ViewModels`).
- XAML element `x:Name` in **PascalCase**; ViewModels named `{Feature}ViewModel`.

### Structure Patterns

- Folders: `Views/` · `ViewModels/` · `Services/` (interface + impl, e.g.
  `IPdfMergeService` + `PdfSharpMergeService`) · `Models/` · `Strings/` (UI text)
  · `Converters/` (if any). Single project; no feature-folder nesting (app is small).
- Tests in a sibling project **`pdfjunior.Tests`** (xUnit.v3), mirroring the
  source namespace layout. No co-located tests.
- Assets stay in the existing `Assets/`.

### MVVM Patterns (the highest-divergence area)

- **Use CommunityToolkit.Mvvm source generators exclusively**: `[ObservableProperty]`
  for bindable state, `[RelayCommand]` for actions. **No hand-written
  `INotifyPropertyChanged`, no `event` handlers in code-behind for logic.**
- ViewModels derive from `ObservableObject`; **all UI logic lives in ViewModels**,
  not code-behind. Code-behind is limited to view wiring (e.g. passing the HWND to
  pickers, `InitializeComponent`).
- **Bind with compiled `{x:Bind}`** (mode explicit; default `OneWay`), not classic
  `{Binding}` — type-safe, faster, fails at compile time.
- Each View exposes its VM via a typed `ViewModel` property resolved from DI in the
  constructor; `MainWindow` owns `MainViewModel`.
- **Derived state** (e.g. `IsMergeEnabled`) is a computed property re-raised when its
  inputs change — never duplicated logic in the View.

### Async, Threading & Cancellation Patterns

- **Never block the UI thread**: no `.Result`, `.Wait()`, `Task.Run(...).Result`.
  All I/O and PDF work is `await`-ed.
- Background work via `Task.Run`; **marshal UI/collection updates back via
  `DispatcherQueue.TryEnqueue`** (or `IProgress<T>` whose callback is captured on
  the UI thread). `ObservableCollection<T>` is mutated **only on the UI thread**.
- Long operations (validation, merge) **accept a `CancellationToken`** and check it
  cooperatively; the merge token is owned by the window-close guard.

### Dependency Injection Patterns

- **Single composition root** in `App` (`ConfigureServices()` building an
  `IServiceProvider`); store it on `App.Current`. **Constructor injection only —
  no service locator / `App.Current.Services.GetService` scattered in code.**
- Services registered as **singletons** (stateless/app-lifetime); ViewModels
  registered and **resolved**, not `new`-ed in Views. **`MainViewModel` is
  registered as a singleton (single window).**

### Error & Result Patterns

- Services **return result types** (`MergeOutcome { Success(path) | Failure(reason) }`),
  they do **not** throw across layer boundaries for expected failures. Only truly
  exceptional/programmer errors propagate as exceptions.
- The **exception → microcopy mapping lives in exactly one place** (a single
  mapper), producing the canonical `Failure` reason strings.

### UI String Patterns

- **All user-facing strings live in one `Strings/UiStrings.cs` static class** and
  must match `EXPERIENCE.md` **verbatim**. No inline string literals duplicated in
  Views/ViewModels. (v1 is English-only per PRD §5, so a static constants class is
  used over `.resw`; centralizing makes the strings unit-testable against the UX spec.)
- Format strings (`Not enough space on {drive}.`, `{N} pages`) use named/positional
  formatting from the same source.

### XAML / Theming Patterns

- **Only WinUI theme resources** for color/brush/spacing (`{ThemeResource ...}`) —
  **no hardcoded hex, no custom radii** (DESIGN.md). Accent (`AccentButtonStyle`,
  list selection) in **exactly two places**; errors use the semantic critical brush,
  never accent.
- No spinners/glyphs on text states; states are text-only (DESIGN.md / EXPERIENCE.md).

### Logging / Privacy Patterns

- **No telemetry, analytics, or network sinks** (NFR-2). Any logging is local,
  debug-only (`System.Diagnostics.Debug`), never shipped to a remote and never
  containing file contents or full paths beyond what the user already sees.

### Enforcement Guidelines

**All AI agents MUST:**
- Honor `<Nullable>enable</Nullable>` — no `!` null-forgiving to silence warnings
  without justification.
- Use the CommunityToolkit.Mvvm generators (no manual INPC), `x:Bind`, constructor
  DI, result-type error handling, and `UiStrings` for all copy.
- Keep all UI/collection mutations on the UI thread and never block it.

**Pattern enforcement:** `.editorconfig` encodes naming/style rules so the
compiler/analyzer flags violations; code review checks MVVM-generator usage,
`x:Bind`, and string centralization. Pattern changes are recorded in this document.

### Pattern Examples

**Good:**
```csharp
[ObservableProperty] private PdfFileItem? _selectedFile;     // generator
[RelayCommand(CanExecute = nameof(CanMerge))]
private async Task MergeAsync(CancellationToken ct) { ... }  // Async + CT last
```
```xml
<Button Content="{x:Bind ViewModel.MergeLabel}" Style="{StaticResource AccentButtonStyle}"/>
```

**Anti-patterns (avoid):**
```csharp
public event PropertyChangedEventHandler? PropertyChanged;   // hand-rolled INPC
var result = MergeAsync().Result;                            // blocks UI thread
infoBar.Message = "Not enough space...";                     // inline string literal
Background="#0078D4";                                        // hardcoded color
```

## Project Structure & Boundaries

### Complete Project Directory Structure

(`[existing]` = already scaffolded; `[new]` = added during implementation)

```
pdfjunior/                              (repo root)
├── pdfjunior.slnx                      [existing] — add pdfjunior.Tests to it
├── .editorconfig                       [new] — encodes naming/style rules (enforcement)
├── pdfjunior/                          [existing] — the app project
│   ├── pdfjunior.csproj                [existing] — align TFM→net10, MinVer→Win11, add packages
│   ├── app.manifest                    [existing]
│   ├── Package.appxmanifest            [existing] — minimal capabilities (no broadFileSystemAccess)
│   ├── App.xaml / App.xaml.cs          [existing] — composition root: ConfigureServices(), IServiceProvider
│   ├── MainWindow.xaml / .cs           [existing] — the single window (two-pane shell, all UI); close-guard
│   ├── Assets/                         [existing] — Store logos / icons
│   ├── Models/                         [new]
│   │   ├── PdfFileItem.cs              — Guid Id, Path, DisplayName, Status, PageCount? (ObservableObject)
│   │   ├── ValidationStatus.cs         — enum { Checking, Valid, ErrorPassword, ErrorCorrupt }
│   │   └── MergeOutcome.cs             — result type: Success(path) | Failure(reason)
│   ├── ViewModels/                     [new]
│   │   └── MainViewModel.cs            — file list, selection, commands, CanMerge, validation pipeline
│   ├── Services/                       [new]
│   │   ├── IPdfValidationService.cs / PdfValidationService.cs    — Windows.Data.Pdf classify + page count
│   │   ├── IPdfPreviewService.cs    / PdfPreviewService.cs       — Windows.Data.Pdf page→bitmap render
│   │   ├── IPdfMergeService.cs      / PdfSharpMergeService.cs    — PDFsharp merge (IProgress + CT)
│   │   ├── IFilePickerService.cs    / FilePickerService.cs       — FileOpenPicker / FileSavePicker (HWND)
│   │   ├── IFolderLauncher.cs       / FolderLauncher.cs          — open Explorer to output folder
│   │   ├── IOutputWriter.cs         / OutputWriter.cs            — write merged doc directly to destination
│   │   └── IErrorMapper.cs          / ErrorMapper.cs             — exception → canonical Failure reason
│   ├── Strings/                        [new]
│   │   └── UiStrings.cs                — single source of all user-facing copy (matches EXPERIENCE.md)
│   ├── Converters/                     [new, if needed]
│   │   └── ValidationStatusToBrushConverter.cs   — status caption color (or x:Bind funcs)
│   └── Properties/                     [existing] — launchSettings, PublishProfiles (x86/x64/arm64)
└── pdfjunior.Tests/                    [new] — xUnit.v3 unit tests
    ├── pdfjunior.Tests.csproj
    ├── ViewModels/MainViewModelTests.cs        — CanMerge gating (FR-10), ordering, selection
    ├── Services/PdfValidationServiceTests.cs   — password vs corrupt vs valid classification (FR-2)
    ├── Services/PdfSharpMergeServiceTests.cs    — page order, single-file, cancellation (FR-8)
    ├── Services/ErrorMapperTests.cs            — exception → exact microcopy (FR-11)
    └── Fixtures/                               — valid.pdf, encrypted.pdf, corrupt.pdf (image-as-pdf)
```

**Note on `Views/`:** the entire UI is the single `MainWindow.xaml`, kept at the
project root per the WinUI template — **do not move it**. A `Views/` folder is
introduced only if a UserControl is later extracted; none is required for v1.

### Architectural Boundaries (layer boundaries — no API/DB/network exist)

- **View ↔ ViewModel:** one-way dependency. The View binds with `{x:Bind}` to
  `MainViewModel`; **no business logic in code-behind** (only `InitializeComponent`,
  HWND wiring, and the close-guard dialog). The View never touches Services.
- **ViewModel ↔ Services:** the VM depends on **service *interfaces* only** (DI-injected),
  orchestrates them, and holds in-memory state. It does not `new` services or call
  platform/library APIs directly.
- **Services ↔ Platform/Libraries:** each service **encapsulates** its dependency —
  `Windows.Data.Pdf`, `PDFsharp`, the pickers — and exposes **domain types/results**
  (`PdfFileItem`, `ValidationStatus`, `MergeOutcome`). Library types like PDFsharp's
  `PdfDocument` never leak into the VM; `StorageFile` crosses only where the picker
  result legitimately must.
- **No external/data boundaries:** local-only, in-memory, no network, no persistence.

### Requirements → Structure Mapping

| FR | Lives in |
|---|---|
| FR-1 Add PDFs | `FilePickerService` (open) + `MainViewModel.AddFilesCommand` |
| FR-2 Validate | `PdfValidationService` + VM validation pipeline + `PdfFileItem.Status` |
| FR-3 Remove | `MainViewModel.RemoveCommand` |
| FR-4 Reorder | `ListView` drag-and-drop (`CanReorderItems`/`AllowDrop`/`CanDragItems`) mutating the bound `Files` collection — no view-model command [Decision 2026-06-18] |
| FR-5 Preview | `PdfPreviewService` + MainWindow preview pane |
| FR-6 Trigger merge | `MainViewModel.MergeCommand` (+ `CanMerge`) |
| FR-7 Output destination | `FilePickerService` (FileSavePicker) |
| FR-8 Execute merge | `PdfSharpMergeService` + `OutputWriter` |
| FR-9 Report success | VM + MainWindow `InfoBar` + `FolderLauncher` |
| FR-10 Block merge | `MainViewModel.CanMerge` + cause-specific tooltip |
| FR-11 Handle failure | `ErrorMapper` + VM error `InfoBar` |
| FR-12 Close guard | MainWindow `AppWindow.Closing` handler + `ContentDialog` + merge `CancellationToken` |

**Cross-cutting:** `UiStrings` (all copy, FR-2/9/10/11) · single startup-captured
threading/HWND access (App) · `MergeOutcome`/`ErrorMapper` (error taxonomy).

### Data Flow

`Add` → picker returns paths → VM appends `PdfFileItem(Checking)` → validation
service per item (bounded concurrency) → status/page-count marshaled to UI →
selection drives preview service → `Merge` → save picker → `PdfSharpMergeService`
writes via `OutputWriter` straight to the destination (progress > 2s) → `MergeOutcome`
→ success/error `InfoBar`. No data leaves the device.

### Build & Distribution

- **Build output:** `bin/`, `obj/` per project (git-ignored).
- **Distribution:** single-project **MSIX** from `pdfjunior` (Package & Publish);
  per-arch publish profiles already present (x86/x64/arm64); Release is trimmed +
  ReadyToRun. Windows 11 minimum.
- **Tests:** `pdfjunior.Tests` runs via `dotnet test` (Microsoft Testing Platform);
  not packaged.

## Architecture Validation Results

### Coherence Validation ✅

**Decision Compatibility:** All choices target .NET 10 and interoperate cleanly —
Windows App SDK 2.2.0, PDFsharp 6.2.x (net10), CommunityToolkit.Mvvm 8.4.2,
Microsoft.Extensions.DependencyInjection 10.0.x, xunit.v3 3.2.2. No version or
runtime conflicts. The FR-11 reconciliation (partial output permitted) removed the
only internal contradiction (temp-file staging vs direct write).

**Pattern Consistency:** MVVM-generator + `x:Bind` + constructor-DI + result-type
error handling align with the CommunityToolkit.Mvvm / Microsoft.Extensions.DI
decisions; UI-string centralization aligns with the English-only scope; theming
rules align with DESIGN.md.

**Structure Alignment:** Views/ViewModels/Services/Models/Strings layout supports
the layer boundaries; single-project MSIX matches the scaffolded repo; the test
project supports the xUnit decision.

### Requirements Coverage Validation ✅

**Functional Requirements:** All 12 FRs map to concrete components (see
Requirements → Structure Mapping). No FR is unsupported.

**Non-Functional Requirements:**
- NFR-1 non-blocking → off-thread Task.Run + IProgress + cancellation + >2s progress. ✅
- NFR-2 local-only / NFR-4 no account → no network/telemetry, in-memory, picker-only. ✅
- NFR-3 MSIX/Store/Win11 → single-project MSIX, minimal caps, Win11 min (after align). ✅
- NFR-5 startup <3s → lightweight single-window init, R2R; **measure at packaging**. ⚠️
- NFR-6 <100 MB → managed PDFsharp + in-box Windows.Data.Pdf + trimming; **track App SDK footprint**. ⚠️
- NFR-7 privacy policy → **release deliverable, not an architecture artifact** (PM/release task). ⚠️

### Implementation Readiness Validation ✅

**Decision Completeness:** All critical decisions documented with verified versions.
**Structure Completeness:** Concrete file/folder tree mapped to FRs; boundaries defined.
**Pattern Completeness:** Naming, structure, MVVM, async/threading, DI, error, string,
theming, and logging patterns specified with good/anti-pattern examples.

### Gap Analysis Results

**Critical Gaps:** None.

**Important Gaps (non-blocking, verify during implementation):**
- **Trimming × WinUI/PDFsharp:** `PublishTrimmed=True` (Release) can strip
  reflection-used members; verify the packaged app runs and PDFsharp/WinUI aren't
  broken (add trimming roots/`TrimmerRootAssembly` if needed). Balances NFR-6 vs SM-2.
- **Budget measurement:** NFR-5 (<3s) and NFR-6 (<100 MB) are runtime/packaging
  budgets — measure early, don't assume.
- **Validation wall-clock threshold:** default 30 s to be tuned (FR-2 assumption).

**Nice-to-Have Gaps:**
- `.editorconfig` rule content not yet enumerated (referenced as the enforcement point).
- Preview rendering for very large PDFs: render-on-demand/virtualization not deeply
  specified (acceptable for v1; revisit if memory pressure appears).

### Validation Issues Addressed

The FR-11 atomicity reversal was reconciled across PRD, addendum, UX EXPERIENCE.md,
and this architecture (direct-write output model). NFR-7 reclassified as a release
deliverable. No blocking issues remain.

### Architecture Completeness Checklist

**Requirements Analysis**
- [x] Project context thoroughly analyzed
- [x] Scale and complexity assessed
- [x] Technical constraints identified
- [x] Cross-cutting concerns mapped

**Architectural Decisions**
- [x] Critical decisions documented with versions
- [x] Technology stack fully specified
- [x] Integration patterns defined
- [x] Performance considerations addressed

**Implementation Patterns**
- [x] Naming conventions established
- [x] Structure patterns defined
- [x] Communication patterns specified
- [x] Process patterns documented

**Project Structure**
- [x] Complete directory structure defined
- [x] Component boundaries established
- [x] Integration points mapped
- [x] Requirements to structure mapping complete

### Architecture Readiness Assessment

**Overall Status:** READY FOR IMPLEMENTATION (all 16 checklist items confirmed; no
critical gaps — the three ⚠️ items are non-blocking verification/release tasks).

**Confidence Level:** High.

**Key Strengths:** minimal license-clean dependency set well under the size cap;
strong agent-divergence guardrails; clean layer boundaries; faithful PRD/UX trace.

**Areas for Future Enhancement:** trimming validation, preview virtualization for
huge PDFs, page-range selection (v1.1), `.editorconfig` rule enumeration.

### Implementation Handoff

**AI Agent Guidelines:** follow the recorded decisions and patterns exactly; depend
on service interfaces; keep all copy in `UiStrings`; never block the UI thread; no
network/telemetry/persistence.

**First Implementation Priority:** align `pdfjunior.csproj` — TargetFramework →
`net10.0-windows10.0.22621.0`, TargetPlatformMinVersion → `10.0.22000.0`, add
`CommunityToolkit.Mvvm 8.4.2`, `PDFsharp 6.2.x`, `Microsoft.Extensions.DependencyInjection`;
create `pdfjunior.Tests` (xunit.v3). Then build models → services → MainViewModel → UI.
