# PDF Junior — PRD Addendum

Companion to `prd.md`. Captures technical depth and decision provenance that belongs downstream (architecture / solution design), kept out of the capability-focused PRD body. This is **input for `bmad-create-architecture`**, not committed architecture.

## 1. Stack re-target: React Native for Windows → C# / WinUI 3

The product is a faithful re-platform. Capabilities are unchanged; the implementation moves from a JavaScript/TypeScript-over-RNW bridge to native C# / WinUI 3. The table maps each legacy concern to its native equivalent.

| Concern | Legacy (RNW 0.81) | C# / WinUI 3 target | Notes |
|---|---|---|---|
| App framework | React Native for Windows 0.81 (TypeScript on Hermes) | **WinUI 3 (Windows App SDK), C#** | UI logic moves from JS/TS into C#; Fluent is now native, not bridged. |
| PDF merge engine | `pdf-lib` (pure JS) | **A .NET PDF library — TBD** | Candidates: PDFsharp/MigraDoc (MIT, pure managed, merge-capable), or QuestPDF/UglyToad PdfPig for read. Must support concatenation of whole files and preserve content. **Open Question 1.** |
| PDF validation (password/corrupt) | `pdf-lib` `PDFDocument.load()` | The chosen .NET PDF library, and/or `Windows.Data.Pdf.PdfDocument` (throws on encrypted/corrupt) | Validation must distinguish *error-password* from *error-corrupt* and extract page count for *valid* files. |
| PDF preview rendering | `react-native-pdf` (unconfirmed) → fallback WinRT `Windows.Data.Pdf.PdfDocument` → defer | **`Windows.Data.Pdf.PdfDocument`**, rendering pages to `Image`/`BitmapImage` in a scrollable WinUI control | First-class from C#; this is why preview is promoted to a confirmed v1 capability (PRD §4.2). |
| Off-thread merge | Web Worker (`mergeWorker.ts`) | **`async`/`await` + `Task.Run` / background thread**, marshaling progress to the UI thread | Satisfies NFR-1 (non-blocking, responsive). |
| File picker (input) | Custom C++/WinRT `FilePickerModule` TurboModule | **`Windows.Storage.Pickers.FileOpenPicker`** directly from C# (multi-select, `.pdf` filter) | No custom native module needed. |
| Output destination | Custom C++/WinRT `FolderPickerModule` + separate filename field + custom overwrite dialog | **`Windows.Storage.Pickers.FileSavePicker`** directly from C# (`SuggestedFileName = "merged.pdf"`, `FileTypeChoices = {.pdf}`) | **Consolidates** folder choice + filename + overwrite into one native Save dialog. The OS enforces filename validity and prompts for overwrite, so no in-app filename validation or custom overwrite dialog is needed (PRD FR-7). |
| State management | Zustand 5.0.14 (single store) | **C# view models (MVVM)** with `INotifyPropertyChanged` / `ObservableObject` | In-memory only; no persistence layer. |
| Fluent controls | Fluent UI surfaced through RNW | **Native WinUI 3 controls**: `InfoBar`, `ContentDialog`, `ProgressBar`/`ProgressRing`, `TextBox`, `ListView`, accent `Button` | Segoe UI Variable typography, 4-pt spacing, auto light/dark, Mica. |
| File item IDs | `crypto.randomUUID()` | `System.Guid` | — |
| Testing | Jest + `@rnx-kit/jest-preset` | **MSTest / xUnit / NUnit — TBD**; UI automation via WinAppDriver if needed | Resolve in architecture. |
| Packaging / distribution | MSIX via WAP packaging project | **MSIX via Windows Application Packaging Project** (unchanged) | Windows 11 minimum, Microsoft Store, minimal capabilities. |

## 2. Capability-relevant constraints that survive the re-platform

These are platform-independent and still bind the C#/WinUI build:

- Fully offline / local-only; no network in normal operation; no telemetry, cloud, or account (PRD NFR-2, NFR-4).
- Picker-mediated file access only — no `broadFileSystemAccess` (PRD §9.1).
- No file-count or file-size limit; merge must remain non-blocking; progress only after 2 s (PRD NFR-1).
- No persistence of any kind between sessions (PRD §9.1).
- Startup < 3 s; installed footprint < 100 MB (PRD NFR-5, NFR-6) — **track the Windows App SDK + chosen PDF library footprint against the 100 MB cap early.**
- Read-only, fit-to-width, vertical-scroll preview; must render partial content for damaged files with an inline exclusion notice (PRD §4.2).

## 3. Legacy decision provenance (carried forward)

Decisions from the legacy run that the fresh PRD preserves, for traceability:

- **Reorder = Move up/down buttons** (legacy sprint change 2026-06-10; reversed an earlier drag-and-drop design). **Decided** (user, 2026-06-14): buttons kept, drag-reorder out of scope. PRD FR-4.
- **Selectable/removable while *checking*** (legacy sprint change 2026-06-13, FR-2.1 / decision #27; the `isChecking` selection guard was to be removed). Carried as PRD FR-2 / FR-3 / FR-5.
- **Output filename set in the native Save dialog, default `merged.pdf`** (user decision, 2026-06-14 — replaced the legacy separate filename field). The OS handles naming, validity, and overwrite. PRD FR-7. This supersedes the legacy "blank/whitespace → merged.pdf" rule, which no longer applies (there is no in-app filename field).
- **App keeps no memory of last output location** (carries spirit of legacy decision #11). PRD FR-7 (Assumption).
- **Windows 11 minimum; no file/size limit; minimal capabilities; MSIX** (legacy NFR-3, NFR-1; decisions #10, #12). Carried unchanged.

## 4. Deferred technical decisions (for architecture)

1. **.NET PDF library** for merge + validation + page-count (PRD Open Question 1). Evaluate against the 100 MB install cap, the *error-password* vs *error-corrupt* distinction, and support for **cooperative cancellation** (PRD FR-12). `[2026-06-14: atomic write-then-move is no longer a requirement — PRD FR-11 dropped the no-partial-output guarantee; partial output is permitted on failure. The library may write directly to the destination.]`
2. **Validation strategy** — single library for both merge and validation, or `Windows.Data.Pdf` for validation/preview + a separate merge library. **Hard requirement (not optional):** the chosen approach must distinguish *error-password* from *error-corrupt* (PRD FR-2) and yield page counts for valid files. `Windows.Data.Pdf.PdfDocument` satisfies this — encrypted PDFs raise a distinct error from unparseable ones — so it is a safe validation floor even if the merge library differs.
3. **Test stack** — unit framework and whether UI automation (WinAppDriver) is warranted.

*(Accessibility implementation is no longer a deferred decision: per user decision 2026-06-14, accessibility is not a v1 requirement — see PRD §5.)*
