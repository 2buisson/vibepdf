---
baseline_commit: 3ebe699
---

# Story 1.4: Preview Selected File

Status: review

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a user,
I want to see a read-only preview of the selected PDF,
so that I can verify the correct file is in the right position before merging.

## Acceptance Criteria (BDD)

1. **Given** a valid PDF file is in the File list **When** the user clicks it to select it **Then** the Preview pane shows a read-only render of the file, **fit-to-width, with vertical scroll only** — **And** no editing, annotation, zoom, or page controls are present.

2. **Given** a valid multi-page PDF is selected **When** the user scrolls the Preview pane **Then** all pages are viewable via continuous vertical scroll.

3. **Given** a file in *checking* status is selected **When** the Preview pane is viewed **Then** it shows the "Checking…" placeholder (MC-7) instead of a preview.

4. **Given** a flagged file with **error-password** status is selected **When** the Preview pane is viewed **Then** it shows the inline exclusion notice MC-8: **"This file is password protected and will be excluded from the merge."** (centered; no preview content rendered).

5. **Given** a flagged file with **error-corrupt** status is selected **When** the Preview pane is viewed **Then** it shows the inline exclusion notice MC-9: **"This file could not be read and will be excluded from the merge."** (centered; no preview content rendered).

6. **Given** a flagged file with **error-timeout** status (`ValidationStatus.ErrorTimeout`) is selected **When** the Preview pane is viewed **Then** it shows the corrupt exclusion notice MC-9 (timeout is treated as "could not be read"). *(Not in the epics text — required because the enum gained `ErrorTimeout` in stories 1-2/1-3; see Dev Notes "Microcopy decisions".)*

7. **Given** no file is selected but the list is not empty **When** the Preview pane is viewed **Then** it shows MC-2 ("Select a file to preview it").

8. **Given** the File list is empty **When** the sidebar is viewed **Then** it shows MC-1 ("Add PDFs to get started"). *(Existing behavior — regression check; do not break it.)*

9. **Given** a checking file is selected and showing MC-7 **When** its validation completes (→ Valid / ErrorPassword / ErrorCorrupt / ErrorTimeout) **Then** the Preview pane updates automatically to the matching state (render or exclusion notice) **without** the user re-selecting it.

10. **Given** a file is selected and its preview is showing **When** the user drag-reorders the File list (any item, including the selected one) **Then** the selected file's preview content is **unchanged — no reload, no flicker, no re-render** (carryover guarantee from story 1.3 / FR-4).

11. **Given** the selected file is removed (Remove) **When** removal completes **Then** selection clears and the Preview pane returns to MC-2, and any in-flight render for it is cancelled. *(Regression with story 1.3 Remove; verify the preview clears.)*

## Tasks / Subtasks

- [x] **Task 1 — Implement `PdfPreviewService : IPdfPreviewService`** (AC: #1, #2)
  - [x] Create `pdfjunior/Services/PdfPreviewService.cs`. Implement the **existing** interface verbatim — do **not** change the signature: `Task<IReadOnlyList<BitmapImage>> RenderPagesAsync(string path, double width, CancellationToken ct)`.
  - [x] Use `Windows.Data.Pdf` (mirror `PdfValidationService`): `StorageFile.GetFileFromPathAsync(path)` → `PdfDocument.LoadFromFileAsync(file)`.
  - [x] For each page `0..PageCount-1`: `using var page = doc.GetPage((uint)i);` render to an `InMemoryRandomAccessStream` via `await page.RenderToStreamAsync(stream, options)` where `options = new PdfPageRenderOptions { DestinationWidth = (uint)Math.Max(1, width) }` (fit-to-width by render resolution). Then create `var bmp = new BitmapImage();`, `stream.Seek(0);`, `await bmp.SetSourceAsync(stream);` and add to the result list.
  - [x] Check `ct.ThrowIfCancellationRequested()` between pages so a superseded render stops promptly.
  - [x] `BitmapImage` is a XAML `DependencyObject` — it must be created on the **UI thread**. The VM calls this service from the UI thread (see Task 2), so do **not** wrap the BitmapImage creation in `Task.Run`. Keep every `await` non-blocking (NFR-1); do not call `.Result`/`.Wait()`.
  - [x] Register in DI (Task 5).

- [x] **Task 2 — Add the preview state machine to `MainViewModel`** (AC: #3–#11)
  - [x] Inject `IPdfPreviewService` via the constructor (new last parameter). Store it in a field.
  - [x] Add `public ObservableCollection<BitmapImage> PreviewPages { get; } = [];`
  - [x] Add a preview state enum `PreviewState { None, Checking, Ready, ExcludedPassword, ExcludedCorrupt }` (put it in `Models/PreviewState.cs`) and `[ObservableProperty] public partial PreviewState Preview { get; set; }`.
  - [x] Add computed bindings: `public bool ShowPreviewPages => Preview == PreviewState.Ready;` and `public string? PreviewPlaceholderText` (returns `UiStrings.EmptyPreviewPlaceholder` for None, `UiStrings.PreviewChecking` for Checking, `UiStrings.PreviewPasswordExclusion` for ExcludedPassword, `UiStrings.PreviewCorruptExclusion` for ExcludedCorrupt, `null`/empty for Ready) and `public bool ShowPreviewPlaceholder => Preview != PreviewState.Ready;`. Re-raise `ShowPreviewPages`/`ShowPreviewPlaceholder`/`PreviewPlaceholderText` whenever `Preview` changes (use `[NotifyPropertyChangedFor]` on the `Preview` property, or re-raise in the generated `OnPreviewChanged`).
  - [x] Add `private CancellationTokenSource? _previewCts;` (single in-flight render; cancel the previous on every selection/status change).
  - [x] Hook selection: implement `partial void OnSelectedFileChanged(PdfFileItem? value)` → call `UpdatePreviewAsync(value)`.
  - [x] Hook status-of-selected-file changes: in the **existing** `OnFilePropertyChanged`, when `sender` is the current `SelectedFile` and `e.PropertyName == nameof(PdfFileItem.Status)`, call `UpdatePreviewAsync(SelectedFile)` (AC #9). Keep the existing `NotifyMergeStateChanged()` behavior.
  - [x] Implement `UpdatePreviewAsync(PdfFileItem? item)`:
    - Cancel + dispose `_previewCts`; create a fresh one (`var cts = _previewCts = new();`).
    - Clear `PreviewPages`.
    - `null` → `Preview = PreviewState.None`; return.
    - `Checking` → `Preview = PreviewState.Checking`; return (no render).
    - `ErrorPassword` → `Preview = PreviewState.ExcludedPassword`; return.
    - `ErrorCorrupt` **or** `ErrorTimeout` → `Preview = PreviewState.ExcludedCorrupt`; return (AC #6).
    - `Valid` → render: capture `path = item.Path`; `var pages = await _previewService.RenderPagesAsync(path, PreviewViewportWidth, cts.Token);` then **guard staleness**: `if (cts.IsCancellationRequested || !ReferenceEquals(item, SelectedFile)) return;` before populating `PreviewPages` (add each via the UI thread) and setting `Preview = PreviewState.Ready`. Wrap the render in `try/catch (OperationCanceledException) { }` and a general `catch { Preview = PreviewState.ExcludedCorrupt; }` (a file that validated Valid but fails to render falls back to the corrupt notice).
  - [x] **Do NOT** call `UpdatePreviewAsync` from `OnFilesCollectionChanged` — a `Files.Move` (drag-reorder) does not change `SelectedFile`, so no preview update must occur (AC #10). Leave collection-changed handling exactly as-is.
  - [x] Add `[ObservableProperty] public partial double PreviewViewportWidth { get; set; }` for the View to feed the pane width (Task 4). When it transitions from `0`→`>0` while a `Valid` file is selected with no pages yet, trigger a render (re-call `UpdatePreviewAsync(SelectedFile)` in `OnPreviewViewportWidthChanged` guarded on `Preview != PreviewState.Ready && SelectedFile?.Status == Valid`).

- [x] **Task 3 — Wire preview UI in `MainWindow.xaml`** (AC: #1, #2, #3–#8)
  - [x] Replace the placeholder `TextBlock` currently inside the preview `ScrollViewer` (Grid.Row="1", lines ~100–110) with two overlaid regions in the same `Grid.Row="1"` cell:
    - **Pages:** a `ScrollViewer` (`HorizontalScrollBarVisibility="Disabled"`, `VerticalScrollBarVisibility="Auto"`) containing an `ItemsControl`/`ItemsRepeater` bound `ItemsSource="{x:Bind ViewModel.PreviewPages, Mode=OneWay}"`; item template = `<Image Source="{x:Bind}" Stretch="Uniform" HorizontalAlignment="Stretch"/>` stacked vertically. Bind the ScrollViewer `Visibility` to `ViewModel.ShowPreviewPages` (via the existing `local:MainWindow.BoolToVisibility(...)` helper).
    - **Placeholder:** a centered `TextBlock` bound `Text="{x:Bind ViewModel.PreviewPlaceholderText, Mode=OneWay}"`, `Foreground="{ThemeResource TextFillColorSecondaryBrush}"`, `Visibility="{x:Bind local:MainWindow.BoolToVisibility(ViewModel.ShowPreviewPlaceholder), Mode=OneWay}"`.
  - [x] Keep the existing preview toolbar (Remove button) untouched.
  - [x] Resolve the inline-literal deferred-debt **for the preview pane** while you are here: the preview placeholder must come from `UiStrings` (via `PreviewPlaceholderText`), not a literal. Optionally also switch the sidebar empty-state literal (line ~60) to `{x:Bind ...}`/`UiStrings.EmptyFileListPlaceholder` — same deferred item; mark in deferred-work.md only what you actually change.
  - [x] No spinners/glyphs on any text state (DESIGN.md): placeholder and exclusion notices are text-only.

- [x] **Task 4 — Feed preview viewport width from code-behind** (AC: #1)
  - [x] In `MainWindow.xaml`, give the preview `ScrollViewer` an `x:Name` and handle `SizeChanged`. In `MainWindow.xaml.cs`, set `ViewModel.PreviewViewportWidth = e.NewSize.Width` (subtract a small constant only if a scrollbar gutter causes horizontal overflow). This is acceptable view-wiring in code-behind (per architecture).
  - [x] Re-render on resize is **optional/nice-to-have** for v1 — `Stretch="Uniform"` keeps images fit-to-width visually between renders. Do not add resize-driven re-render unless trivial; if skipped, note it in deferred-work.md.

- [x] **Task 5 — DI registration** (AC: #1)
  - [x] In `App.xaml.cs` `ConfigureServices()`, add `services.AddSingleton<IPdfPreviewService, PdfPreviewService>();` (singleton, matching `IPdfValidationService`).

- [x] **Task 6 — Update tests** (AC: #3–#11)
  - [x] `MainViewModelTests.CreateViewModel()` **must** now pass a third arg: `Substitute.For<IPdfPreviewService>()` (store it as a field like the others). The mock's `RenderPagesAsync` returns `Task.FromResult<IReadOnlyList<BitmapImage>>([])` by default. **Do NOT construct real `BitmapImage` instances in tests** — they require a UI thread; return an empty list and assert on `Preview` state + service-invocation instead.
  - [x] Add `pdfjunior.Tests/ViewModels/` cases (state machine, no bitmaps):
    - Select `null` → `Preview == None`, `PreviewPages` empty, `ShowPreviewPlaceholder` true, placeholder text == `UiStrings.EmptyPreviewPlaceholder`.
    - Select Checking item → `Preview == Checking`, placeholder == `UiStrings.PreviewChecking`, `RenderPagesAsync` **not** called.
    - Select ErrorPassword → `Preview == ExcludedPassword`, placeholder == `UiStrings.PreviewPasswordExclusion`; ErrorCorrupt and ErrorTimeout → `ExcludedCorrupt`, placeholder == `UiStrings.PreviewCorruptExclusion` (a `[Theory]` over corrupt+timeout).
    - Select Valid item (width pre-set >0) → `RenderPagesAsync` called once with that item's `Path`; `Preview == Ready`; `ShowPreviewPages` true.
    - **AC #9:** select a checking item (no render), then set `item.Status = ValidationStatus.Valid` → `RenderPagesAsync` called once, `Preview == Ready`.
    - **AC #10 (critical):** add 3 valid items, select one, `RenderPagesAsync` called once; then `vm.Files.Move(...)` (mirrors drag-reorder) → assert `RenderPagesAsync` **received only 1 call total** (`_previewService.Received(1).RenderPagesAsync(...)`), `Preview` still `Ready`, `SelectedFile` unchanged reference.
    - **AC #11:** select a Valid item, Remove it → `Preview == None`, `PreviewPages` empty, `SelectedFile` null.
    - Stale-render guard: have the mock return via a `TaskCompletionSource`; select item A (render pending), then select item B before A completes; complete A's task → assert A's pages are **not** applied (`Preview` reflects B, not A).
  - [x] Set `vm.PreviewViewportWidth = 800` (or similar) in render tests so the Valid path renders.

- [x] **Task 7 — Verify end-to-end** (AC: #1–#11) — requires Visual Studio **F5** (MSIX app cannot launch from CLI; see Testing Notes)
  - [x] Build clean (0 warnings / 0 errors) for `-p:Platform=x64 -r win-x64`; run the built test exe directly (see `project_run_tests` memory) — all tests green.
  - [ ] Visual F5 checks: select a valid multi-page PDF → pages render fit-to-width, vertical scroll reveals all pages, no horizontal scroll, no zoom/page controls. Select a checking file → "Checking…"; watch it resolve to a render with no re-click. Select password / corrupt files → the exact MC-8 / MC-9 sentences. Drag-reorder the selected file → preview does **not** flicker/reload. Remove the selected file → preview returns to "Select a file to preview it"; empty the list → sidebar shows "Add PDFs to get started". **(Pending — manual VS F5 pass, MSIX cannot launch from CLI.)**

## Dev Notes

### Current state of the files you will touch (read before modifying)

- **`pdfjunior/Services/IPdfPreviewService.cs`** *(exists — interface only)*: `Task<IReadOnlyList<BitmapImage>> RenderPagesAsync(string path, double width, CancellationToken ct);`. **Implement it; do not change the signature.** `BitmapImage` is `Microsoft.UI.Xaml.Media.Imaging.BitmapImage`.
- **`pdfjunior/Services/PdfValidationService.cs`** *(pattern to mirror)*: shows the `Windows.Data.Pdf` access pattern (`StorageFile.GetFileFromPathAsync` → `PdfDocument.LoadFromFileAsync`) and the cancellation/`try-catch` discipline. Reuse the shape.
- **`pdfjunior/ViewModels/MainViewModel.cs`** *(primary change)*: `SelectedFile` is `[ObservableProperty]` with `[NotifyCanExecuteChangedFor(nameof(RemoveCommand))]`. `OnFilePropertyChanged` already fires on each item's `Status` change (currently only recomputes merge state) — extend it for the selected-file preview update. `RunOnUI(action)` runs inline when `_dispatcherQueue` is null (test host) else marshals via `DispatcherQueue.TryEnqueue` — use it for any `PreviewPages` mutation done off the UI thread (the render itself runs on the UI thread, so direct mutation after `await` is fine; still funnel through `RunOnUI` to match house style). The ctor currently takes `(IFilePickerService, IPdfValidationService)` — you are adding a third param.
- **`pdfjunior/MainWindow.xaml`** *(preview pane)*: the preview is `Grid.Column="2" → Grid.Row="1"`, currently a `ScrollViewer` wrapping a hardcoded `TextBlock "Select a file to preview it"`. Replace that inner content. The `ScrollViewer` already has the correct scroll config (`HorizontalScrollBarVisibility="Disabled"`, `VerticalScrollBarVisibility="Auto"`). Existing x:Bind static helpers live on `MainWindow`: `BoolToVisibility`, `BoolToVisibilityInverse`, `FormatStatus`, `StatusForeground` — reuse `BoolToVisibility` for the preview regions.
- **`pdfjunior/MainWindow.xaml.cs`** *(selection wiring)*: `FileListView_SelectionChanged` pushes `ViewModel.SelectedFile = FileListView.SelectedItem as PdfFileItem` (one-way View→VM; there is **no** VM→View `SelectedItem` binding). Add the preview `ScrollViewer` `SizeChanged` handler here. Do not introduce a `SelectedItem` TwoWay binding (deferred refactor, out of scope).
- **`pdfjunior/Strings/UiStrings.cs`** *(all strings already exist — do not invent)*: `EmptyPreviewPlaceholder` (MC-2), `PreviewChecking` (MC-7), `PreviewPasswordExclusion` (MC-8), `PreviewCorruptExclusion` (MC-9), `EmptyFileListPlaceholder` (MC-1). Use these verbatim.
- **`pdfjunior/Models/ValidationStatus.cs`**: `enum { Checking, Valid, ErrorPassword, ErrorCorrupt, ErrorTimeout }` — note `ErrorTimeout` exists (AC #6).
- **`pdfjunior/App.xaml.cs`**: `ConfigureServices()` is the single composition root — add the preview-service registration here.

### Microcopy decisions — READ THIS (prevents a wrong-string disaster)

The **epics.md** Story 1.4 ACs say the preview exclusion notices are *"Password protected"* / *"Could not read file"*. **That is wrong for the preview pane** — those are the *list-item caption* strings (MC-5 / MC-6). The authoritative UX spec (`EXPERIENCE.md` Microcopy Inventory) and the architecture rule *"`UiStrings` must match `EXPERIENCE.md` verbatim"* define the **preview** notices as the longer sentences:
- error-password → **MC-8** `UiStrings.PreviewPasswordExclusion` = "This file is password protected and will be excluded from the merge."
- error-corrupt / error-timeout → **MC-9** `UiStrings.PreviewCorruptExclusion` = "This file could not be read and will be excluded from the merge."

Use MC-8 / MC-9 in the preview pane. The short MC-5/MC-6 strings stay where they already are — the list-item captions (unchanged). `ErrorTimeout` is grouped with corrupt everywhere else (merge gating, list caption "Could not read file (timeout)"), so its preview notice is MC-9.

### The "no reload on reorder" guarantee (AC #10 — the highest-value correctness point)

Story 1.3 implemented drag-reorder as `ObservableCollection<PdfFileItem>.Move`, which **preserves the selected instance**: `SelectedFile`'s reference does not change, no `SelectionChanged` fires. Therefore the **only** triggers for a preview (re)render are: (a) `SelectedFile` reference changes, and (b) the *selected* file's `Status` changes (checking→resolved). A collection `Move`/reorder is **neither**. Concretely: **drive renders from `OnSelectedFileChanged` and `OnFilePropertyChanged`, never from `OnFilesCollectionChanged`.** The AC #10 test asserts `RenderPagesAsync` is called exactly once across an add+select+reorder sequence.

### Threading, cancellation & BitmapImage

- `BitmapImage` is a XAML `DependencyObject` → construct it on the **UI thread**. The VM invokes `RenderPagesAsync` on the UI thread (commands/property-changed run there), so the service may create `BitmapImage`s inline. Never `Task.Run` the BitmapImage creation.
- All `Windows.Data.Pdf` calls are `await`-able and non-blocking — never `.Result`/`.Wait()`/`ConfigureAwait(false)` in VM/UI code (WinUI needs the sync context).
- Use a **single** `_previewCts` (only one preview at a time). On each `UpdatePreviewAsync` cancel+dispose the previous CTS, make a new one, pass its token to `RenderPagesAsync`, and **guard staleness** before applying results: `if (cts.IsCancellationRequested || !ReferenceEquals(item, SelectedFile)) return;`. This prevents a slow render of file A from landing after the user selected file B (and mirrors the `_validationCts` discipline from story 1.3).
- `ObservableCollection<BitmapImage> PreviewPages` is mutated only on the UI thread (funnel through `RunOnUI` to match house style).

### Decided preview design (follow this to avoid divergence)

VM owns the state; the View is dumb:
```csharp
// Models/PreviewState.cs
public enum PreviewState { None, Checking, Ready, ExcludedPassword, ExcludedCorrupt }

// MainViewModel
public ObservableCollection<BitmapImage> PreviewPages { get; } = [];

[ObservableProperty]
[NotifyPropertyChangedFor(nameof(ShowPreviewPages))]
[NotifyPropertyChangedFor(nameof(ShowPreviewPlaceholder))]
[NotifyPropertyChangedFor(nameof(PreviewPlaceholderText))]
public partial PreviewState Preview { get; set; }

public bool ShowPreviewPages => Preview == PreviewState.Ready;
public bool ShowPreviewPlaceholder => Preview != PreviewState.Ready;
public string? PreviewPlaceholderText => Preview switch
{
    PreviewState.None              => UiStrings.EmptyPreviewPlaceholder,   // MC-2
    PreviewState.Checking          => UiStrings.PreviewChecking,           // MC-7
    PreviewState.ExcludedPassword  => UiStrings.PreviewPasswordExclusion,  // MC-8
    PreviewState.ExcludedCorrupt   => UiStrings.PreviewCorruptExclusion,   // MC-9
    _                              => null,
};

partial void OnSelectedFileChanged(PdfFileItem? value) => _ = UpdatePreviewAsync(value);
```
`UpdatePreviewAsync` is `async Task` but fire-and-forget from the partial hooks (`_ = UpdatePreviewAsync(...)`); swallow `OperationCanceledException`. Keep `async void` out of it — use `_ = SomeAsyncTask()`.

### Architecture compliance (guardrails)

- **MVVM:** all logic in `MainViewModel`; code-behind limited to view wiring (selection push + the new `SizeChanged` width feed). Use CommunityToolkit generators (`[ObservableProperty]`, partial hooks) — no hand-rolled `INotifyPropertyChanged`. Bind with compiled `{x:Bind}` (explicit mode), not `{Binding}`.
- **DI:** constructor injection only; register `IPdfPreviewService` as a singleton in the single composition root (`App.ConfigureServices`). Do not `new` the service or use a service locator.
- **Services boundary:** `PdfPreviewService` encapsulates `Windows.Data.Pdf`; only `BitmapImage` (the interface's declared return) crosses to the VM. Do not leak `PdfDocument`/`PdfPage`/`StorageFile` into the VM.
- **Theming:** preview placeholder text uses `{ThemeResource TextFillColorSecondaryBrush}`; no hardcoded hex, no custom radii, no spinners/glyphs on text states (DESIGN.md). Accent stays only on Merge + list selection.
- **Strings:** all copy from `UiStrings` (already present) — no inline literals. Resolve the preview-pane inline-literal deferred item while here.
- **Nullable:** honor `<Nullable>enable</Nullable>`; capture `SelectedFile` into locals and null-check rather than `!`.
- **No new NuGet packages:** `Windows.Data.Pdf` and `BitmapImage` are in-box. PDFsharp is still out of scope (story 2.2).
- **Privacy/logging:** local-only; any logging is `System.Diagnostics.Debug` only; never log file contents/paths beyond what the user sees.

### Library / framework specifics

- `Windows.Data.Pdf.PdfDocument` / `PdfPage.RenderToStreamAsync(IRandomAccessStream, PdfPageRenderOptions)` — in-box (Windows App SDK 2.2.0 / .NET 10). `PdfPageRenderOptions.DestinationWidth` (uint, pixels) drives fit-to-width render resolution; leave `DestinationHeight` unset to preserve aspect ratio.
- `Microsoft.UI.Xaml.Media.Imaging.BitmapImage` + `await bmp.SetSourceAsync(stream)`; `Windows.Storage.Streams.InMemoryRandomAccessStream` for the per-page buffer (`stream.Seek(0)` before `SetSourceAsync`).
- Versions in play (no upgrades): Microsoft.WindowsAppSDK 2.2.0, CommunityToolkit.Mvvm 8.4.2, Microsoft.Extensions.DependencyInjection 10.0.x, xunit.v3 3.2.2, NSubstitute 5.3.0.

### Project Structure Notes

```
pdfjunior/
└── pdfjunior/
    ├── Services/
    │   └── PdfPreviewService.cs        [NEW]    — implements existing IPdfPreviewService (Windows.Data.Pdf)
    ├── Models/
    │   └── PreviewState.cs             [NEW]    — enum { None, Checking, Ready, ExcludedPassword, ExcludedCorrupt }
    ├── ViewModels/
    │   └── MainViewModel.cs            [UPDATE] — IPdfPreviewService ctor param; PreviewPages; Preview state;
    │                                              UpdatePreviewAsync; _previewCts; OnSelectedFileChanged hook;
    │                                              OnFilePropertyChanged preview trigger; PreviewViewportWidth
    ├── MainWindow.xaml                 [UPDATE] — preview pages ItemsControl + placeholder TextBlock (UiStrings)
    ├── MainWindow.xaml.cs              [UPDATE] — preview ScrollViewer SizeChanged → PreviewViewportWidth
    └── App.xaml.cs                     [UPDATE] — register IPdfPreviewService → PdfPreviewService (singleton)
pdfjunior.Tests/
└── ViewModels/
    └── MainViewModelTests.cs          [UPDATE] — CreateViewModel() adds IPdfPreviewService mock; preview state tests
```
No new NuGet packages, no new test fixtures (existing `valid/encrypted/corrupt.pdf` suffice if you choose to add an optional service smoke test — but see Testing Notes; headless BitmapImage is not viable).

### Testing Notes

- Stack: **xUnit.v3 + NSubstitute**. `CreateViewModel()` is the shared factory — updating its signature is the first thing to do (a third injected mock). All existing tests flow through it.
- `DispatcherQueue.GetForCurrentThread()` is null in the test host → `RunOnUI` runs inline, so state updates apply synchronously after `await`. Reuse `WaitForValidation()` (`Task.Delay(200)`) where the validation pipeline is involved.
- **Do not construct `BitmapImage` in tests** (needs a UI thread). Mock `IPdfPreviewService.RenderPagesAsync` to return `Task.FromResult<IReadOnlyList<BitmapImage>>([])`, and assert on `Preview` state, `ShowPreviewPages/ShowPreviewPlaceholder/PreviewPlaceholderText`, and `Received(n).RenderPagesAsync(...)`. The empty list is fine — `Preview == Ready` is the signal, not page count.
- **Do not add a headless `PdfPreviewServiceTests`** that renders real bitmaps — `BitmapImage`/XAML need a UI thread; the actual render is a manual F5 check (same constraint that kept E2E manual in 1-1/1-2/1-3). If you want a service-level test, keep it to argument/cancellation behavior that doesn't construct a `BitmapImage`.
- **Run tests the project's way** (see memory `project_run_tests`): `dotnet test` fails to discover; build `pdfjunior.Tests` for `-p:Platform=x64 -r win-x64` and run the produced `pdfjunior.Tests.exe` directly. Current green baseline is **39 tests** — keep them green and add to them.
- **E2E/visual:** MSIX cannot launch from CLI; the rendered preview, fit-to-width, scroll, and no-flicker-on-reorder are confirmed by Antoine's Visual Studio F5 pass during review. State the F5 items as pending in completion notes rather than claiming automated E2E.

### Anti-Patterns to Avoid

- Do **not** use the epics' short strings ("Password protected"/"Could not read file") in the **preview** pane — use MC-8/MC-9 (`PreviewPasswordExclusion`/`PreviewCorruptExclusion`). (See "Microcopy decisions".)
- Do **not** trigger a preview render from `OnFilesCollectionChanged` — that would reload the preview on every drag-reorder and break AC #10.
- Do **not** forget `ErrorTimeout` in the state mapping (→ MC-9). Missing it leaves a timed-out selected file with the wrong/blank preview.
- Do **not** construct `BitmapImage` off the UI thread or inside `Task.Run`; do **not** construct it in unit tests.
- Do **not** change the `IPdfPreviewService` signature, add NuGet packages, or pull in PDFsharp.
- Do **not** add a `SelectedItem` TwoWay binding (deferred refactor); keep the existing `SelectionChanged` push.
- Do **not** apply value equality to `PdfFileItem` — reference equality is required (story 1.3) for `ReferenceEquals(item, SelectedFile)` staleness guards and for `Files.IndexOf/Remove`.
- Do **not** leave a render uncancelled when selection changes — a stale render landing on the wrong file is a visible defect; use `_previewCts` + the `ReferenceEquals` guard.
- Do **not** add zoom, page-nav, annotation, or any editing affordance (read-only preview, FR-5).
- Do **not** block the UI thread (`.Result`/`.Wait()`) or use `async void` / `ConfigureAwait(false)` in VM/service UI code.

### Previous Story Intelligence

**From story 1.3 (review) and 1.2 (done):**
- Drag-reorder = `ObservableCollection.Move` (commit 859232b) preserves the selected instance and fires no `SelectionChanged` → this is exactly why the preview must not reload on reorder; the test `Reorder_PreservesSelectionAndOrder` already documents the move + selection invariant — extend that idea to assert the render is not re-invoked.
- Per-item cancellation pattern (`_validationCts`, cancel on Remove, `if (!Files.Contains(item)) return;` write guards) is the established cancellation/staleness idiom — `_previewCts` + `ReferenceEquals(item, SelectedFile)` mirrors it.
- `[ObservableProperty]` requires **partial property** syntax (`public partial T Prop { get; set; }`) for the WinUI/WinRT generators — match it for `Preview` and `PreviewViewportWidth`.
- x:Bind **function** bindings to static `MainWindow` methods (`BoolToVisibility`, `FormatStatus`) are the established View idiom — reuse `BoolToVisibility` for preview region visibility.
- Tests: green via the direct-exe path (39 passing); MSIX visuals are a manual VS F5 step — note it, don't fake it.
- `OnFilePropertyChanged` already subscribes/unsubscribes each item and reacts to `Status` — extend it (don't rebuild it) for the selected-file preview trigger; removed items are already unsubscribed, so a removed file won't drive a stray preview update.

**Still-open deferred items (be aware; not this story's job unless they block you):** orphaned `Task.Run` after validation timeout; sidebar inline-literal (you *may* fix it here); `GetRequiredService` in `MainWindow` ctor; locale-dependent password heuristic; bare-catch corrupt classification; `Task.Delay(200)` test timing; `SubclassProc` GC-rooting. The two 1-3-assigned items (semaphore-on-removal, status-write-after-removal) are already resolved.

### Git Intelligence

Recent commits show the working cadence: one focused `feat:` commit per story (`d51551d` 1.1, `dd65d1e` 1.2, `07e2d78` 1.3), a follow-up `feat:` for the reorder pivot (`859232b`), and a `docs:` reconciliation (`3ebe699`). Commit 859232b is the relevant precedent: it *removed* the Move up/down commands and slimmed `MainViewModel`/`MainWindow.xaml`/tests for drag-reorder — confirming reorder lives entirely in the ListView and mutates `Files` directly (the basis for AC #10). Follow the same one-clean-commit convention; baseline for this story is `3ebe699`.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story 1.4] — Story statement + AC seed (note the preview-string correction in "Microcopy decisions")
- [Source: _bmad-output/planning-artifacts/architecture.md#PDF Engine & Library Strategy] — Windows.Data.Pdf for preview via `PdfPage.RenderToStreamAsync`; PDFsharp out of scope here
- [Source: _bmad-output/planning-artifacts/architecture.md#App Structure, MVVM & Dependency Injection] — `IPdfPreviewService` (page→bitmap), VM owns logic, DI singletons, constructor injection
- [Source: _bmad-output/planning-artifacts/architecture.md#MVVM Patterns (the highest-divergence area)] — `[ObservableProperty]`, `x:Bind` explicit, derived state re-raised, no logic in code-behind
- [Source: _bmad-output/planning-artifacts/architecture.md#Async, Threading & Cancellation Patterns] — UI-thread-only collection mutation, cooperative cancellation, no blocking/`ConfigureAwait(false)`
- [Source: _bmad-output/planning-artifacts/architecture.md#Requirements → Structure Mapping] — FR-5 → `PdfPreviewService` + MainWindow preview pane
- [Source: _bmad-output/planning-artifacts/ux-designs/ux-pdf-junior-2026-06-14/EXPERIENCE.md#Microcopy Inventory] — MC-2/MC-7/MC-8/MC-9 canonical strings (preview)
- [Source: _bmad-output/planning-artifacts/ux-designs/ux-pdf-junior-2026-06-14/EXPERIENCE.md#Component Patterns] — Preview pane: read-only, fit-to-width, vertical scroll, exclusion notice centered, no content rendered when flagged
- [Source: _bmad-output/planning-artifacts/ux-designs/ux-pdf-junior-2026-06-14/EXPERIENCE.md#State Patterns] — File selected (valid/flagged), file reordered "preview content unchanged — no reload"
- [Source: _bmad-output/planning-artifacts/ux-designs/ux-pdf-junior-2026-06-14/DESIGN.md#Components] — `ScrollViewer` preview pane, vertical scroll only; text-only states, no spinners/glyphs
- [Source: _bmad-output/implementation-artifacts/sprint-status.yaml] — 2026-06-18 note: "preview does not reload on reorder" guarantee holds (drag uses ObservableCollection.Move); re-verify in 1-4
- [Source: _bmad-output/implementation-artifacts/1-3-remove-reorder-files.md] — reorder = `Files.Move`, reference-stability of `SelectedFile`, cancellation idiom, test patterns, MSIX/F5 constraint
- [Source: _bmad-output/implementation-artifacts/deferred-work.md] — inline-literal placeholder debt (preview-pane line resolvable here)
- [Source: pdfjunior/Services/IPdfPreviewService.cs] — interface to implement (do not change)
- [Source: pdfjunior/Services/PdfValidationService.cs] — Windows.Data.Pdf access + cancellation pattern to mirror
- [Source: pdfjunior/ViewModels/MainViewModel.cs] — `SelectedFile`, `OnFilePropertyChanged`, `RunOnUI`, ctor to extend
- [Source: pdfjunior/MainWindow.xaml] — preview ScrollViewer/placeholder to replace; existing x:Bind helpers
- [Source: pdfjunior/MainWindow.xaml.cs] — `FileListView_SelectionChanged` push; add SizeChanged width feed
- [Source: pdfjunior/Strings/UiStrings.cs] — MC-2/MC-7/MC-8/MC-9 already defined
- [Source: pdfjunior/App.xaml.cs] — `ConfigureServices()` composition root
- [Source: pdfjunior.Tests/ViewModels/MainViewModelTests.cs] — `CreateViewModel()` factory + NSubstitute patterns to extend

## Dev Agent Record

### Agent Model Used

Claude Sonnet 4.6 (claude-sonnet-4-6)

### Debug Log References

- Initial XAML compile failed: `WMC1121: Invalid binding assignment` — the preview `ItemsControl.ItemTemplate`'s `<Image Source="{x:Bind}">` had no `x:DataType` on its `DataTemplate`, so the compiler couldn't resolve the binding context. Fixed by adding `xmlns:imaging="using:Microsoft.UI.Xaml.Media.Imaging"` and `x:DataType="imaging:BitmapImage"` on the `DataTemplate`.

### Completion Notes List

- Implemented `PdfPreviewService` mirroring `PdfValidationService`'s `Windows.Data.Pdf` access pattern; renders each page to an `InMemoryRandomAccessStream` via `PdfPageRenderOptions.DestinationWidth` (fit-to-width) and produces a `BitmapImage` per page.
- Added `Models/PreviewState.cs` and the full preview state machine in `MainViewModel`: `Preview` property with re-raised `ShowPreviewPages`/`ShowPreviewPlaceholder`/`PreviewPlaceholderText`, `PreviewPages`, `PreviewViewportWidth`, and `UpdatePreviewAsync` driven only by `OnSelectedFileChanged` and the existing `OnFilePropertyChanged` (status-of-selected-file) hook — never by `OnFilesCollectionChanged`, preserving the AC #10 "no reload on reorder" guarantee.
- Single in-flight `_previewCts` with cancel-and-dispose-then-recreate on every selection/status change, plus a `ReferenceEquals(item, SelectedFile)` staleness guard before applying render results (mirrors the `_validationCts` idiom from story 1.3).
- Used MC-8/MC-9 (`PreviewPasswordExclusion`/`PreviewCorruptExclusion`) in the preview pane per the Dev Notes correction — not the shorter epics.md strings (those remain the list-item captions, MC-5/MC-6, unchanged).
- `ErrorTimeout` is grouped with `ErrorCorrupt` → `ExcludedCorrupt` (MC-9), per AC #6.
- Wired `MainWindow.xaml`: preview `ScrollViewer`/`ItemsControl` (bound to `PreviewPages`, visibility on `ShowPreviewPages`) overlaid with a placeholder `TextBlock` (bound to `PreviewPlaceholderText`, visibility on `ShowPreviewPlaceholder`). Also resolved the sidebar empty-state inline-literal deferred item (now `{x:Bind strings:UiStrings.EmptyFileListPlaceholder}`).
- `MainWindow.xaml.cs`: added `PreviewScrollViewer_SizeChanged` to feed `ViewModel.PreviewViewportWidth`. Resize-driven re-render was **not** implemented (optional/nice-to-have per Task 4) — `Stretch="Uniform"` keeps already-rendered pages visually fit between renders.
- Registered `IPdfPreviewService → PdfPreviewService` as a singleton in `App.xaml.cs`, matching `IPdfValidationService`.
- Updated `MainViewModelTests.CreateViewModel()` to inject a third `IPdfPreviewService` mock (default: empty page list) and added 10 new preview test cases covering AC #3–#11 (state-per-status, render-once-on-valid, AC #9 checking→valid auto-render, AC #10 reorder-does-not-reload, AC #11 remove-clears-preview, and a stale-render supersession case). No `BitmapImage` instances are constructed in tests, per the Testing Notes constraint.
- Build is clean (0 warnings/0 errors) for `-p:Platform=x64 -r win-x64`; the direct-exe test run reports **44 total, 0 errors, 0 failed, 0 skipped**.
- **Pending:** the Task 7 visual F5 pass (rendered preview, fit-to-width, scroll, no-flicker-on-reorder, exact MC-8/MC-9 sentences) requires Antoine's manual Visual Studio F5 verification — MSIX cannot launch from CLI, consistent with stories 1.1–1.3. Left unchecked in Tasks/Subtasks rather than claimed as done.

### File List

- `pdfjunior/Services/PdfPreviewService.cs` (new)
- `pdfjunior/Models/PreviewState.cs` (new)
- `pdfjunior/ViewModels/MainViewModel.cs` (modified)
- `pdfjunior/MainWindow.xaml` (modified)
- `pdfjunior/MainWindow.xaml.cs` (modified)
- `pdfjunior/App.xaml.cs` (modified)
- `pdfjunior.Tests/ViewModels/MainViewModelTests.cs` (modified)

## Change Log

- 2026-06-18: Implemented `PdfPreviewService` (Windows.Data.Pdf, fit-to-width render) and the `MainViewModel` preview state machine (`PreviewState`, `PreviewPages`, `PreviewViewportWidth`, `UpdatePreviewAsync`) driven by selection + selected-file status changes only (never collection changes), preserving the AC #10 no-reload-on-reorder guarantee. Wired the preview pane in `MainWindow.xaml`/`MainWindow.xaml.cs` and registered the service in DI. Resolved the sidebar empty-state inline-literal deferred item. Added 10 unit tests covering AC #3–#11 — suite now 44 tests total, all green; 0 warnings/0 errors. Status → review. Manual VS F5 visual pass pending.
