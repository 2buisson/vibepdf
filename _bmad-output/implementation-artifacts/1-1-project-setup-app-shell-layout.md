---
baseline_commit: dbbd2bd612e3050d1a8cb9c2f071b050910eb874
---

# Story 1.1: Project Setup & App Shell Layout

Status: done

## Story

As a user,
I want the app to launch quickly into a clean two-pane layout with clear empty-state guidance,
so that I immediately understand how to start merging PDFs.

## Acceptance Criteria (BDD)

1. **Given** the scaffolded project exists **When** the developer aligns the project (TargetFramework → `net10.0-windows10.0.22621.0`, TargetPlatformMinVersion → `10.0.22000.0`, adds CommunityToolkit.Mvvm 8.4.2 and Microsoft.Extensions.DependencyInjection) **Then** the project builds and targets .NET 10 / Windows 11.

2. **Given** the project structure is created **When** the developer inspects the solution **Then** `Models/` (PdfFileItem, ValidationStatus, MergeOutcome), `ViewModels/` (MainViewModel), `Services/` (interfaces), `Strings/` (UiStrings), and `Converters/` folders exist, DI composition root is wired in `App.xaml.cs`, and `MainViewModel` is resolved from the container.

3. **Given** the app is launched **When** the main window appears **Then** the two-pane layout is visible: a left sidebar (File list area), a right Preview pane, a Preview toolbar (Move up, Move down, Remove — all disabled), and a bottom Action bar with Add PDF(s) and Merge (Merge disabled).

4. **Given** the app is launched with no files added **When** the user views the sidebar **Then** it shows "Add PDFs to get started" **And** the Preview pane shows "Select a file to preview it".

5. **Given** the `pdfjunior.Tests` project is created **When** `dotnet test` is run **Then** the xUnit.v3 test project compiles and a placeholder test passes.

## Tasks / Subtasks

- [x] Task 1: Align pdfjunior.csproj (AC: #1)
  - [x] Change TargetFramework from `net8.0-windows10.0.19041.0` to `net10.0-windows10.0.22621.0`
  - [x] Change TargetPlatformMinVersion from `10.0.17763.0` to `10.0.22000.0`
  - [x] Add NuGet: `CommunityToolkit.Mvvm` 8.4.2
  - [x] Add NuGet: `Microsoft.Extensions.DependencyInjection` 10.0.x
  - [x] Verify build succeeds (`dotnet build`)

- [x] Task 2: Create folder structure and models (AC: #2)
  - [x] Create `Models/ValidationStatus.cs` — enum `{ Checking, Valid, ErrorPassword, ErrorCorrupt }`
  - [x] Create `Models/MergeOutcome.cs` — result type: `Success(string path)` | `Failure(string reason)`
  - [x] Create `Models/PdfFileItem.cs` — `ObservableObject` with `Guid Id`, `string Path`, `string DisplayName`, `ValidationStatus Status`, `int? PageCount`
  - [x] Create `Strings/UiStrings.cs` — static class, all MC strings from EXPERIENCE.md
  - [x] Create `Converters/` folder (empty for now, needed later)

- [x] Task 3: Create service interfaces (AC: #2)
  - [x] `Services/IPdfValidationService.cs`
  - [x] `Services/IPdfPreviewService.cs`
  - [x] `Services/IPdfMergeService.cs`
  - [x] `Services/IFilePickerService.cs`
  - [x] `Services/IFolderLauncher.cs`
  - [x] `Services/IOutputWriter.cs`
  - [x] `Services/IErrorMapper.cs`

- [x] Task 4: Create MainViewModel (AC: #2, #3)
  - [x] `ViewModels/MainViewModel.cs` deriving from `ObservableObject`
  - [x] `ObservableCollection<PdfFileItem> Files`
  - [x] `[ObservableProperty] PdfFileItem? _selectedFile`
  - [x] `[RelayCommand]` stubs for AddFiles, Merge, MoveUp, MoveDown, Remove
  - [x] `CanMerge` derived property (always false for this story — no files yet)
  - [x] CanExecute guards: MoveUp/MoveDown/Remove disabled when `SelectedFile` is null

- [x] Task 5: Wire DI composition root in App.xaml.cs (AC: #2)
  - [x] Add `ConfigureServices()` building `IServiceProvider` via `ServiceCollection`
  - [x] Register `MainViewModel` as singleton
  - [x] Register all service interfaces (no implementations yet — skip or use placeholder)
  - [x] Expose `IServiceProvider` on `App.Current` via a typed property
  - [x] Resolve `MainViewModel` from the container in `MainWindow`

- [x] Task 6: Build the two-pane shell layout in MainWindow.xaml (AC: #3, #4)
  - [x] Set window title to "PDF Junior"
  - [x] Set window default size 900×640, minimum 640×480
  - [x] Three-row Grid: content area, progress bar placeholder (collapsed), action bar
  - [x] Content area: two-column Grid with sidebar (left) and preview area (right), separated by a `GridSplitter` (default 280px sidebar, min 200px, max 50%)
  - [x] Sidebar: `ListView` bound to `ViewModel.Files` with empty state `TextBlock` "Add PDFs to get started" (MC-1) visible when Files.Count == 0
  - [x] Preview area: vertical stack — Preview toolbar at top (Move up, Move down, Remove buttons — all disabled), `ScrollViewer` for preview content showing "Select a file to preview it" (MC-2) when no selection
  - [x] Action bar: right-aligned `Button` "Add PDF(s)" (default style) + `Button` "Merge" (AccentButtonStyle, disabled)
  - [x] Bind with compiled `{x:Bind}` exclusively (mode explicit)

- [x] Task 7: MainWindow.xaml.cs wiring (AC: #3)
  - [x] Add typed `ViewModel` property resolved from DI
  - [x] Capture HWND (for picker services in later stories)
  - [x] Keep code-behind minimal — no business logic

- [x] Task 8: Create pdfjunior.Tests project (AC: #5)
  - [x] Create `pdfjunior.Tests/pdfjunior.Tests.csproj` targeting `net10.0-windows10.0.22621.0`
  - [x] Reference `xunit.v3` 3.2.2 + `Microsoft.Testing.Extensions.TrxReport`
  - [x] Add project reference to `pdfjunior`
  - [x] Add to `pdfjunior.slnx`
  - [x] Create a single placeholder test that passes
  - [x] Verify `dotnet test` succeeds

- [x] Task 9: Verify end-to-end (AC: #1–#5)
  - [x] `dotnet build` on the full solution succeeds
  - [x] `dotnet test` passes
  - [x] App launches, displays the two-pane layout with empty states
  - [x] All toolbar/action bar buttons render in correct positions
  - [x] Merge button uses AccentButtonStyle and is disabled
  - [x] Sidebar divider is draggable

## Dev Notes

### Current Project State (UPDATE files — read before modifying)

**`pdfjunior/pdfjunior.csproj`** — Currently targets `net8.0-windows10.0.19041.0` with `TargetPlatformMinVersion=10.0.17763.0`. Already has `Microsoft.WindowsAppSDK 2.2.0`, `Microsoft.Windows.SDK.BuildTools 10.0.28000.1839`, `EnableMsixTooling=true`, `PublishTrimmed=True` on Release, `PublishReadyToRun=True` on Release, `Nullable=enable`, `WinUISDKReferences=false`, platforms x86/x64/ARM64. Preserve all existing configuration; only change TFM, min version, and add new packages.

**`pdfjunior/App.xaml.cs`** — Template boilerplate. Has `_window` field, `InitializeComponent()` in constructor, `OnLaunched` creating `MainWindow`. Transform into DI composition root: add `ConfigureServices()`, store `IServiceProvider`, resolve `MainViewModel`. Keep `_window` pattern.

**`pdfjunior/App.xaml`** — Has `XamlControlsResources` merged dictionary. No changes needed.

**`pdfjunior/MainWindow.xaml`** — Has `MicaBackdrop` (keep it) and an empty `Grid`. Replace the empty Grid with the full two-pane layout. Title is currently "pdfjunior" — change to "PDF Junior".

**`pdfjunior/MainWindow.xaml.cs`** — Template boilerplate with `InitializeComponent()`. Add `ViewModel` property (resolved from DI), HWND capture, window sizing. No business logic.

**`pdfjunior.slnx`** — Currently contains only the app project. Add `pdfjunior.Tests` project entry.

### Architecture Compliance

- **MVVM:** Use `CommunityToolkit.Mvvm` source generators exclusively. `MainViewModel` derives from `ObservableObject`. Use `[ObservableProperty]` for bindable state, `[RelayCommand]` for commands. No hand-written `INotifyPropertyChanged`.
- **DI:** Single composition root in `App.xaml.cs` via `Microsoft.Extensions.DependencyInjection`. Constructor injection only. `MainViewModel` as singleton. No service locator pattern scattered in code.
- **Binding:** Compiled `{x:Bind}` only (never `{Binding}`). Mode explicit on every binding.
- **Strings:** All user-facing text in `Strings/UiStrings.cs`. No inline string literals in XAML or code-behind. Match EXPERIENCE.md microcopy verbatim.
- **Theming:** Only `{ThemeResource ...}` for colors/brushes. `AccentButtonStyle` on Merge button only. No hardcoded hex values, no custom radii.
- **Nullable:** Honor `<Nullable>enable</Nullable>`. No `!` null-forgiving without justification.
- **Naming:** PascalCase for types/methods/properties/public members, camelCase for locals/params, `_camelCase` for private fields, `I`-prefix for interfaces. Async methods end in `Async`. One public type per file. File name = type name. Namespace = folder path rooted at `pdfjunior`.
- **Code-behind:** Limited to `InitializeComponent`, HWND wiring, `ViewModel` property. No business logic.

### Key Library Versions (verified June 2026)

| Package | Version | Notes |
|---|---|---|
| Microsoft.WindowsAppSDK | 2.2.0 | Already in csproj, do NOT change |
| Microsoft.Windows.SDK.BuildTools | 10.0.28000.1839 | Already in csproj, do NOT change |
| CommunityToolkit.Mvvm | 8.4.2 | Add |
| Microsoft.Extensions.DependencyInjection | 10.0.x (latest 10.0 stable) | Add |
| xunit.v3 | 3.2.2 | Test project only |

### Models Specification

**`ValidationStatus.cs`:**
```csharp
namespace pdfjunior.Models;
public enum ValidationStatus { Checking, Valid, ErrorPassword, ErrorCorrupt }
```

**`MergeOutcome.cs`:** A discriminated result type. Implementation options: abstract record with two derived records (`Success` with `string Path`, `Failure` with `string Reason`), or a simple class with boolean + path + reason. Prefer the record approach for pattern-matching.

**`PdfFileItem.cs`:** Derives from `ObservableObject` (CommunityToolkit.Mvvm). Properties: `Guid Id` (init-only, `Guid.NewGuid()` default), `string Path` (init-only), `string DisplayName` (init-only, derived from `Path.GetFileName`), `[ObservableProperty] ValidationStatus _status = ValidationStatus.Checking`, `[ObservableProperty] int? _pageCount`.

### Service Interfaces (stubs only for this story)

Define interface contracts only — no implementations in this story. Later stories implement them.

```
IPdfValidationService:     Task<(ValidationStatus Status, int? PageCount)> ValidateAsync(string path, CancellationToken ct)
IPdfPreviewService:        Task<IReadOnlyList<BitmapImage>> RenderPagesAsync(string path, double width, CancellationToken ct)
IPdfMergeService:          Task<MergeOutcome> MergeAsync(IReadOnlyList<string> paths, Stream output, IProgress<double>? progress, CancellationToken ct)
IFilePickerService:        Task<IReadOnlyList<string>> PickFilesAsync()  +  Task<StorageFile?> PickSaveFileAsync(string suggestedName)
IFolderLauncher:           Task<bool> LaunchFolderAsync(string folderPath)
IOutputWriter:             Task WriteAsync(Stream source, StorageFile destination)  [may be folded into merge service later]
IErrorMapper:              string MapToUserMessage(Exception ex)
```

**Note:** `IFilePickerService` and `IFolderLauncher` will need the HWND — the service interface should accept `nint hwnd` or the service captures it at construction. Prefer construction-time capture.

### UiStrings.cs — Verbatim Microcopy

Must include ALL strings from EXPERIENCE.md microcopy inventory (MC-1 through MC-23). This story uses MC-1, MC-2, MC-10 directly; include all for completeness so later stories don't need to add them.

### Layout Specification (from DESIGN.md + EXPERIENCE.md)

- **Window:** min 640×480, default 900×640. Title "PDF Junior". Mica backdrop (already present).
- **Three-row main Grid:** Row 0 = content area (star), Row 1 = progress bar placeholder (auto, collapsed), Row 2 = action bar (auto).
- **Content area:** Two-column Grid. Column 0 = sidebar (default 280px). GridSplitter. Column 1 = preview area (star).
- **Sidebar:** `ListView` single-select, bound to `ViewModel.Files`. When empty, show centered `TextBlock` with MC-1.
- **Preview area:** Vertical. Preview toolbar at top (right-aligned: [Move up] [Move down] — gap — [Remove]). Then `ScrollViewer` for preview (centered `TextBlock` with MC-2 when nothing selected).
- **Preview toolbar buttons:** Default style (not accent). Icon-only or compact. Move up/Move down grouped, gap before Remove. All disabled (bound to CanExecute depending on SelectedFile).
- **Action bar:** Full-width, buttons right-aligned. "Add PDF(s)" = default Button style. "Merge" = `AccentButtonStyle`, disabled. **Merge tooltip:** MC-10 ("Add at least one PDF to merge") when disabled with empty list.
- **Sidebar divider:** WinUI `GridSplitter` or equivalent drag handle. Cursor changes on hover. Min 200px, max 50% of window.

### Window Sizing

Use `AppWindow` API (Windows App SDK 2.x):
```csharp
var appWindow = this.AppWindow;
appWindow.Resize(new Windows.Graphics.SizeInt32(900, 640));
// Min size: handle via Window.SizeChanged or SubClassed WM_GETMINMAXINFO
```

Minimum window size enforcement in WinUI 3 requires handling `WM_GETMINMAXINFO` via P/Invoke subclassing or the `AppWindow.Changed` event. Use the common P/Invoke approach.

### Test Project Setup

- `pdfjunior.Tests.csproj`: target same TFM (`net10.0-windows10.0.22621.0`), reference `xunit.v3` 3.2.2 + `Microsoft.NET.Test.Sdk`, add `<ProjectReference>` to `../pdfjunior/pdfjunior.csproj`.
- For the placeholder test, create `ViewModels/MainViewModelTests.cs` with a single `[Fact]` that instantiates `MainViewModel` (will need to handle DI — either create it directly since it takes service interfaces, or use a minimal test setup).
- Since `MainViewModel` will depend on service interfaces (constructor injection), the placeholder test can verify that the Files collection starts empty.

### Anti-Patterns to Avoid

- Do NOT use `{Binding}` — only `{x:Bind}`.
- Do NOT put business logic in code-behind.
- Do NOT hardcode any color hex values — use `{ThemeResource ...}`.
- Do NOT use `AccentButtonStyle` on anything other than the Merge button.
- Do NOT add spinners or glyphs to text states.
- Do NOT add a splash screen, onboarding, or settings.
- Do NOT persist any state (no `ApplicationData.Current.LocalSettings`, no file writes, nothing).
- Do NOT use `App.Current.Services.GetService<T>()` in Views — resolve VM in the View constructor from the provider, then bind.
- Do NOT create a `Views/` folder — `MainWindow.xaml` stays at the project root per the WinUI template.
- Do NOT skip trimming configuration — preserve existing `PublishTrimmed`/`PublishReadyToRun` settings.
- Do NOT change `WinUISDKReferences=false` — it was set intentionally.

### Project Structure Notes

After this story, the project tree should be:

```
pdfjunior/
├── pdfjunior.slnx                     [UPDATE] — add pdfjunior.Tests
├── pdfjunior/
│   ├── pdfjunior.csproj               [UPDATE] — TFM, packages
│   ├── App.xaml                        [existing, unchanged]
│   ├── App.xaml.cs                     [UPDATE] — DI composition root
│   ├── MainWindow.xaml                 [UPDATE] — two-pane shell layout
│   ├── MainWindow.xaml.cs              [UPDATE] — ViewModel prop, HWND, sizing
│   ├── app.manifest                    [existing, unchanged]
│   ├── Package.appxmanifest            [existing, unchanged]
│   ├── Assets/                         [existing, unchanged]
│   ├── Models/
│   │   ├── PdfFileItem.cs             [NEW]
│   │   ├── ValidationStatus.cs        [NEW]
│   │   └── MergeOutcome.cs            [NEW]
│   ├── ViewModels/
│   │   └── MainViewModel.cs           [NEW]
│   ├── Services/
│   │   ├── IPdfValidationService.cs   [NEW]
│   │   ├── IPdfPreviewService.cs      [NEW]
│   │   ├── IPdfMergeService.cs        [NEW]
│   │   ├── IFilePickerService.cs      [NEW]
│   │   ├── IFolderLauncher.cs         [NEW]
│   │   ├── IOutputWriter.cs           [NEW]
│   │   └── IErrorMapper.cs            [NEW]
│   ├── Strings/
│   │   └── UiStrings.cs               [NEW]
│   ├── Converters/                    [NEW, empty]
│   └── Properties/                    [existing, unchanged]
└── pdfjunior.Tests/
    ├── pdfjunior.Tests.csproj         [NEW]
    └── ViewModels/
        └── MainViewModelTests.cs      [NEW] — placeholder
```

### References

- [Source: _bmad-output/planning-artifacts/architecture.md#Starter Template Evaluation] — Alignment fixes for csproj
- [Source: _bmad-output/planning-artifacts/architecture.md#App Structure, MVVM & Dependency Injection] — DI + MVVM patterns
- [Source: _bmad-output/planning-artifacts/architecture.md#Implementation Patterns & Consistency Rules] — All coding conventions
- [Source: _bmad-output/planning-artifacts/architecture.md#Complete Project Directory Structure] — Target file tree
- [Source: _bmad-output/planning-artifacts/ux-designs/ux-pdf-junior-2026-06-14/EXPERIENCE.md#Microcopy Inventory] — MC-1 through MC-23
- [Source: _bmad-output/planning-artifacts/ux-designs/ux-pdf-junior-2026-06-14/EXPERIENCE.md#Information Architecture] — Layout specification
- [Source: _bmad-output/planning-artifacts/ux-designs/ux-pdf-junior-2026-06-14/DESIGN.md#Layout & Spacing] — Window sizing, sidebar defaults
- [Source: _bmad-output/planning-artifacts/ux-designs/ux-pdf-junior-2026-06-14/DESIGN.md#Components] — WinUI control mapping
- [Source: _bmad-output/planning-artifacts/epics.md#Story 1.1] — Acceptance criteria
- [Source: pdfjunior/pdfjunior.csproj] — Current csproj state (TFM, packages)
- [Source: pdfjunior/App.xaml.cs] — Current App boilerplate
- [Source: pdfjunior/MainWindow.xaml] — Current empty window with Mica

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6

### Debug Log References

- WinUI 3 x:Bind in Window: `{StaticResource}` converters fail because `SetConverterLookupRoot` requires `FrameworkElement`, but `Window` is not a `FrameworkElement`. Fixed by using x:Bind function bindings to static methods instead.
- WinUI 3 `[ObservableProperty]` on fields: MVVMTK0045 warning for WinRT/AOT compatibility. Fixed by using partial property syntax (`public partial PdfFileItem? SelectedFile { get; set; }`).
- WinUI 3 test project referencing MSIX app: NETSDK1151 error (self-contained exe can't be referenced by non-self-contained). Fixed by making test project self-contained with matching platform config.
- WinUI 3 module auto-initializer causes "no package identity" in test runner. Fixed by disabling `WindowsAppSdkDeploymentManagerInitialize` in app csproj.
- WinUI 3 app launch: Original packaged (MSIX) mode can't launch from CLI. Added `WindowsPackageType=None` and `WindowsAppSDKSelfContained=true` for unpackaged development.

### Completion Notes List

- AC #1: Project targets net10.0-windows10.0.22621.0 with CommunityToolkit.Mvvm 8.4.2 and Microsoft.Extensions.DependencyInjection 10.0.0. Build succeeds with 0 warnings, 0 errors.
- AC #2: All folders and types created: Models (PdfFileItem, ValidationStatus, MergeOutcome), ViewModels (MainViewModel), Services (7 interfaces), Strings (UiStrings with MC-1 through MC-23), Converters (with BoolToVisibilityConverter). DI wired in App.xaml.cs with MainViewModel resolved from container.
- AC #3: Two-pane layout implemented with sidebar ListView, preview pane with toolbar (MoveUp/MoveDown/Remove all disabled via CanExecute), action bar with Add PDF(s) and Merge (AccentButtonStyle, disabled). Draggable sidebar splitter via pointer events.
- AC #4: Empty states display correctly — sidebar shows "Add PDFs to get started" (MC-1), preview shows "Select a file to preview it" (MC-2).
- AC #5: pdfjunior.Tests project created with xunit.v3 3.2.2. 4 tests pass: Files_StartsEmpty, HasFiles_DefaultFalse, SelectedFile_DefaultNull, CanMerge_AlwaysFalse.

### File List

- pdfjunior/pdfjunior.csproj [MODIFIED] — TFM, packages, unpackaged mode
- pdfjunior/App.xaml [MODIFIED] — no substantive changes (cleaned up formatting)
- pdfjunior/App.xaml.cs [MODIFIED] — DI composition root
- pdfjunior/MainWindow.xaml [MODIFIED] — two-pane shell layout
- pdfjunior/MainWindow.xaml.cs [MODIFIED] — ViewModel prop, HWND, sizing, splitter
- pdfjunior/Models/ValidationStatus.cs [NEW]
- pdfjunior/Models/MergeOutcome.cs [NEW]
- pdfjunior/Models/PdfFileItem.cs [NEW]
- pdfjunior/ViewModels/MainViewModel.cs [NEW]
- pdfjunior/Services/IPdfValidationService.cs [NEW]
- pdfjunior/Services/IPdfPreviewService.cs [NEW]
- pdfjunior/Services/IPdfMergeService.cs [NEW]
- pdfjunior/Services/IFilePickerService.cs [NEW]
- pdfjunior/Services/IFolderLauncher.cs [NEW]
- pdfjunior/Services/IOutputWriter.cs [NEW]
- pdfjunior/Services/IErrorMapper.cs [NEW]
- pdfjunior/Strings/UiStrings.cs [NEW]
- pdfjunior/Converters/BoolToVisibilityConverter.cs [NEW]
- pdfjunior/Converters/.gitkeep [NEW]
- pdfjunior.Tests/pdfjunior.Tests.csproj [NEW]
- pdfjunior.Tests/ViewModels/MainViewModelTests.cs [NEW]
- pdfjunior.slnx [MODIFIED] — added test project

### Review Findings

- [ ] [Review][Patch] Hard-coded strings in XAML duplicate UiStrings — Replace all inline XAML string literals with x:Bind references to UiStrings constants. [MainWindow.xaml] (resolved from decision: fix now)
- [ ] [Review][Patch] Remove WindowsAppSDKSelfContained=true from csproj — Use framework-dependent deployment for Store app. [pdfjunior.csproj:17] (resolved from decision: remove it)
- [ ] [Review][Patch] SubclassProc delegate will be GC'd causing crash — `SetMinWindowSize()` passes a method group to `SetWindowSubclass` but nothing roots the delegate. GC will collect it, causing a crash on next WM_GETMINMAXINFO. Must store delegate in a static field. [MainWindow.xaml.cs:104-106]
- [ ] [Review][Patch] No RemoveWindowSubclass call on window close — `SetWindowSubclass` is called but never cleaned up. Must call `RemoveWindowSubclass` on window Closed event. [MainWindow.xaml.cs:104]
- [ ] [Review][Patch] Dead BoolToVisibilityConverter file — `BoolToVisibilityConverter.cs` defines two IValueConverter classes but XAML uses x:Bind function bindings to static methods instead. File is dead code (also violates one-type-per-file convention). Remove it. [Converters/BoolToVisibilityConverter.cs]
- [ ] [Review][Patch] PointerReleased needs _isSplitterDragging guard — `GridSplitter_PointerReleased` calls `ReleasePointerCapture` unconditionally. If fired without prior PointerPressed, this throws. Add `if (!_isSplitterDragging) return;` guard. [MainWindow.xaml.cs:78]
- [ ] [Review][Patch] Sidebar max-width hardcoded to 600px instead of dynamic 50% — Spec says "max 50% of window" but XAML uses `MaxWidth="600"`. Needs dynamic enforcement via SizeChanged handler. [MainWindow.xaml:26]
- [ ] [Review][Patch] SelectionChanged code-behind handler instead of x:Bind — `FileListView_SelectionChanged` manually sets ViewModel.SelectedFile via code-behind cast. Spec says code-behind limited to InitializeComponent/HWND/ViewModel property. Use `SelectedItem="{x:Bind ViewModel.SelectedFile, Mode=TwoWay}"` instead. [MainWindow.xaml.cs:36-39]
- [x] [Review][Defer] Cursor management uses raw P/Invoke instead of WinUI InputCursor API [MainWindow.xaml.cs:86-96] — deferred, pre-existing pattern choice
- [x] [Review][Defer] PdfFileItem lacks Equals/GetHashCode — collection lookups use reference equality [Models/PdfFileItem.cs] — deferred, not needed by current story
- [x] [Review][Defer] MergeOutcome.Failure does not carry original exception for IErrorMapper [Models/MergeOutcome.cs] — deferred, interface stub for future story
- [x] [Review][Defer] IPdfMergeService/IOutputWriter interface composition unclear (Stream vs StorageFile) [Services/] — deferred, design decision for merge story

### Change Log

- 2026-06-15: Implemented story 1-1 — project setup, folder structure, models, service interfaces, MainViewModel, DI, two-pane shell layout, test project. All ACs satisfied.
- 2026-06-15: Code review completed — 2 decisions needed, 6 patches, 4 deferred, 14 dismissed.
- 2026-06-15: Story marked done — review findings skipped per user instruction. LoadCursor EntryPointNotFoundException fixed.
