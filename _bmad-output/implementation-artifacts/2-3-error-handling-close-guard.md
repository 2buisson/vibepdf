---
baseline_commit: ee64fc1
depends_on: 2-2-execute-merge-report-success
---

# Story 2.3: Error Handling & Close Guard

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a user,
I want clear error messages when a merge fails and a safety prompt if I try to close the app mid-merge,
so that I understand what went wrong, can retry without losing my file list, and never accidentally cancel a merge.

> **Scope orientation (read first).** This is the **final story of Epic 2** and it finishes the two pieces Stories 2.1/2.2 deliberately stubbed: (a) the **specific error taxonomy** and (b) the **window-close guard (FR-12)**. Story 2.2 already runs the merge end-to-end, locks the UI, shows a generic error `InfoBar` (MC-18) on *any* failure, and creates a per-merge `CancellationTokenSource` (`_mergeCts`) that **nothing currently cancels**. This story: (1) changes `MergeOutcome.Failure` to carry the **`Exception`** (the long-deferred item), (2) implements `ErrorMapper : IErrorMapper` mapping exceptions → the canonical MC-15/16/17/18 strings, (3) replaces 2.2's two generic `ShowError(MergeErrorGeneric)` calls with **mapper-driven** specific messages, and (4) adds the `AppWindow.Closing` guard that, while a merge runs, shows a `ContentDialog` (Keep merging / Close anyway) and cooperatively cancels `_mergeCts` on Close-anyway. You do **NOT** change the success path, the progress bar, the UI-lock, the gating, or any `UiStrings` (MC-15…MC-23 already exist). See **Scope Boundaries**.

> **⚠️ Dependency: Stories 2.1 and 2.2 are implemented in the working tree but NOT yet committed** (`git status` shows them modified/untracked on top of `ee64fc1`). 2.3 builds directly on that working tree. Do **not** re-stub the merge, banners, or `_mergeCts` — they exist. Rebase your mental model onto the post-2.2 tree: `MainViewModel.MergeAsync` is fully implemented (merge → write → success/generic-error), `PdfSharpMergeService`/`OutputWriter`/`FolderLauncher` exist, and the success/error `InfoBar`s + `ProgressBar` are wired in `MainWindow.xaml`.

## Acceptance Criteria (BDD)

All strings are canonical `UiStrings` constants (already defined verbatim — **do not add or alter copy**). The `{...}` placeholders are runtime values (drive letter, filename), not microcopy.

**Error taxonomy plumbing (FR-11, resolves deferred-work):**

1. **Given** the error taxonomy is being built **When** the developer changes `MergeOutcome.Failure` to carry the original `Exception` and implements `ErrorMapper : IErrorMapper` **Then** the app project builds clean (0 warnings / 0 errors) for `-p:Platform=x64 -r win-x64`, and the full existing test suite stays green (no test constructs `MergeOutcome.Failure(string)`).

**Specific failure messages (FR-11):**

2. **Given** the merge fails because the destination disk is full **When** the error banner appears **Then** it shows **`UiStrings.MergeErrorDiskFull`** = "Merge failed — Not enough space on {drive}." with the **actual destination drive root** (e.g. "E:\") — MC-15.

3. **Given** the merge fails because writing the destination is denied **When** the error banner appears **Then** it shows **`UiStrings.MergeErrorAccessDenied`** = "Merge failed — Access denied" — MC-16.

4. **Given** the merge fails because a source file disappeared **When** the error banner appears **Then** it shows **`UiStrings.MergeErrorFileMissing`** = "Merge failed — File not found: {filename}" with the **actual source filename** (no directory) — MC-17.

5. **Given** the merge fails for any other/unexpected reason **When** the error banner appears **Then** it shows **`UiStrings.MergeErrorGeneric`** = "Merge failed. Try again or check the files." — MC-18 (the mapper's fallback).

**Error banner behavior + state preservation (FR-11):**

6. **Given** an error banner is shown **When** the user views it **Then** it is **manual-dismiss only** (no auto-dismiss — error `InfoBar` has no timer), and once the merge attempt ends the **UI is unlocked**: File list reorder re-enabled, and Add PDF(s) / Merge / Remove re-enabled (this already flows from 2.2's `finally { IsMerging = false; }` — confirm it is not regressed).

7. **Given** a merge has failed **When** the user views the File list **Then** it is **preserved for retry** — no files removed, order unchanged.

**Window-close guard (FR-12):**

8. **Given** a merge is in progress (`IsMerging == true`) **When** the user attempts to close the window **Then** the close is **cancelled** and a `ContentDialog` appears — title **`UiStrings.CloseGuardTitle`** (MC-20 "Merge in progress"), body **`UiStrings.CloseGuardBody`** (MC-21), primary/default button **`UiStrings.CloseGuardKeepMerging`** (MC-22 "Keep merging"), secondary button **`UiStrings.CloseGuardCloseAnyway`** (MC-23 "Close anyway").

9. **Given** the close-guard dialog is shown **When** the user selects **Keep merging** (default) **Then** the dialog closes and the merge continues uninterrupted (the window stays open).

10. **Given** the close-guard dialog is shown **When** the user selects **Close anyway** **Then** the in-progress merge is **cancelled cooperatively** (`_mergeCts` is cancelled) and the window closes **And** a partial or incomplete output file may remain at the destination.

11. **Given** no merge is in progress (`IsMerging == false`) **When** the user closes the window **Then** the window closes immediately with **no** guard dialog.

## Tasks / Subtasks

- [ ] **Task 1 — Carry the exception in `MergeOutcome.Failure`** (AC: #1, #2–#5)
  - [ ] In `pdfjunior/Models/MergeOutcome.cs`, change `Failure` to hold the original exception:
    ```csharp
    namespace pdfjunior.Models;

    public abstract record MergeOutcome
    {
        public sealed record Success(string Path) : MergeOutcome;
        public sealed record Failure(Exception Exception) : MergeOutcome;
    }
    ```
  - [ ] In `pdfjunior/Services/PdfSharpMergeService.cs`, change the catch from `new MergeOutcome.Failure(ex.Message)` to `new MergeOutcome.Failure(ex)` (one line — line ~28). Leave everything else (import loop, progress, `Save(output, closeStream: false)`, the `OperationCanceledException` rethrow) untouched.
  - [ ] `Success.Path` stays unused (the VM owns the destination); do not touch it. No existing test constructs `Failure(string)`, so the suite stays green (AC #1).

- [ ] **Task 2 — Implement the error taxonomy `ErrorMapper`** (AC: #2, #3, #4, #5)
  - [ ] Extend the **existing** `pdfjunior/Services/IErrorMapper.cs` to accept the destination path (needed to derive the disk-full drive root — `IOException` does not carry it):
    ```csharp
    namespace pdfjunior.Services;

    public interface IErrorMapper
    {
        // destinationPath supplies the drive root for the disk-full message (MC-15);
        // the exception itself carries the source filename (MC-17).
        string MapToUserMessage(Exception ex, string? destinationPath = null);
    }
    ```
  - [ ] Create `pdfjunior/Services/ErrorMapper.cs` implementing the canonical taxonomy (architecture "Error Taxonomy"). **Check `FileNotFoundException` before the disk-full `IOException` branch** — `FileNotFoundException` is a subclass of `IOException`:
    ```csharp
    using System.Runtime.InteropServices;
    using pdfjunior.Strings;

    namespace pdfjunior.Services;

    public class ErrorMapper : IErrorMapper
    {
        public string MapToUserMessage(Exception ex, string? destinationPath = null)
        {
            switch (ex)
            {
                case FileNotFoundException fnf:                                   // MC-17 (source vanished)
                    return string.Format(
                        UiStrings.MergeErrorFileMissing,
                        Path.GetFileName(fnf.FileName ?? string.Empty));
                case UnauthorizedAccessException:                                 // MC-16 (write denied)
                    return UiStrings.MergeErrorAccessDenied;
            }

            // Disk full surfaces as IOException/COMException with Win32 0x70 (ERROR_DISK_FULL)
            // or 0x27 (ERROR_HANDLE_DISK_FULL). The drive comes from the destination path.
            if (ex is IOException or COMException && (ex.HResult & 0xFFFF) is 0x70 or 0x27)
            {
                var drive = string.IsNullOrEmpty(destinationPath)
                    ? string.Empty
                    : Path.GetPathRoot(destinationPath) ?? string.Empty;
                return string.Format(UiStrings.MergeErrorDiskFull, drive);         // MC-15
            }

            return UiStrings.MergeErrorGeneric;                                   // MC-18 (fallback)
        }
    }
    ```
  - [ ] This is the **single place** the exception→microcopy mapping lives (architecture). Do not scatter `string.Format(MergeError…)` anywhere else.

- [ ] **Task 3 — Register `ErrorMapper` and inject it into `MainViewModel`** (AC: #2–#5)
  - [ ] In `pdfjunior/App.xaml.cs` `ConfigureServices()`, register the singleton alongside the others:
    ```csharp
    services.AddSingleton<IErrorMapper, ErrorMapper>();
    ```
  - [ ] In `pdfjunior/ViewModels/MainViewModel.cs`, add `IErrorMapper errorMapper` as the **6th** constructor parameter and store it in a `private readonly IErrorMapper _errorMapper;` field. *(This changes the ctor signature → update the test factory in Task 6. DI resolves it automatically once registered.)*

- [ ] **Task 4 — Wire specific messages into `MergeAsync`** (AC: #2, #3, #4, #5, #6, #7)
  - [ ] In `MainViewModel.MergeAsync`, replace 2.2's two **generic** `ShowError(UiStrings.MergeErrorGeneric)` calls with mapper-driven calls. The `MergeOutcome.Failure` branch (source-read failures from PDFsharp) and the trailing `catch` (destination-write failures from `OutputWriter`) both map through `_errorMapper`, passing `destination.Path`:
    ```csharp
    if (outcome is MergeOutcome.Failure failure)
    {
        ShowError(_errorMapper.MapToUserMessage(failure.Exception, destination.Path));
        return;
    }
    ...
    catch (OperationCanceledException)
    {
        // Cooperative cancel via the close guard (Task 5). Window is closing — no banner.
    }
    catch (Exception ex)
    {
        ShowError(_errorMapper.MapToUserMessage(ex, destination.Path));
    }
    ```
  - [ ] Keep the `catch (OperationCanceledException)` **before** `catch (Exception ex)` (OCE is the close-anyway path — silent). Change only the bare `catch` to `catch (Exception ex)`.
  - [ ] Do **not** touch the success path, `StartProgressDelay`, `ShowSuccess`/`ShowError` helpers, `IsMerging` gating, or the `finally` block — AC #6 (UI unlock) and AC #7 (list preserved) already hold from 2.2's `finally { … IsMerging = false; }` and the fact that `Files` is never mutated on failure. Verify you did not regress them.

- [ ] **Task 5 — Implement the window-close guard (FR-12)** (AC: #8, #9, #10, #11)
  - [ ] In `MainViewModel.cs`, expose cooperative cancellation (the `_mergeCts` field already exists from 2.2):
    ```csharp
    // Called by the window-close guard (FR-12). No-op when no merge is running.
    public void RequestCancelMerge() => _mergeCts?.Cancel();
    ```
  - [ ] In `pdfjunior/MainWindow.xaml.cs`, subscribe to `AppWindow.Closing` in the constructor (after `Hwnd`/`AppWindow` are available — the ctor already calls `AppWindow.Resize`). Add `using Microsoft.UI.Windowing;`:
    ```csharp
    AppWindow.Closing += OnAppWindowClosing;
    ```
  - [ ] Add the guard handler + dialog (this is **sanctioned code-behind** — architecture explicitly allows "the close-guard dialog" in code-behind). `args.Cancel` **must** be set synchronously before the handler returns; the dialog is shown fire-and-forget:
    ```csharp
    private bool _forceClose;
    private bool _closeGuardOpen;

    private void OnAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_forceClose || !ViewModel.IsMerging)
            return;                       // AC #11 — no merge → close immediately

        args.Cancel = true;               // AC #8 — block the close synchronously
        if (_closeGuardOpen) return;      // a guard dialog is already showing
        _ = ShowCloseGuardAsync();
    }

    private async Task ShowCloseGuardAsync()
    {
        _closeGuardOpen = true;
        try
        {
            var dialog = new ContentDialog
            {
                Title = UiStrings.CloseGuardTitle,             // MC-20
                Content = UiStrings.CloseGuardBody,            // MC-21
                PrimaryButtonText = UiStrings.CloseGuardKeepMerging,   // MC-22 (default)
                SecondaryButtonText = UiStrings.CloseGuardCloseAnyway, // MC-23
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = Content.XamlRoot,
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Secondary)       // AC #10 — Close anyway
            {
                ViewModel.RequestCancelMerge();
                _forceClose = true;
                Close();                                       // re-raises Closing → _forceClose lets it through
            }
            // AC #9 — Keep merging (Primary): do nothing; merge continues.
        }
        finally
        {
            _closeGuardOpen = false;
        }
    }
    ```
  - [ ] `ContentDialog` requires `XamlRoot` in WinUI 3 — set `XamlRoot = Content.XamlRoot`. The `_closeGuardOpen` flag prevents stacking dialogs if the user repeatedly hits the close button.
  - [ ] Do **not** add an `x:Bind`/XAML `ContentDialog` — construct it in code-behind (it's transient and overlay-owned). No `MainWindow.xaml` change is needed this story.

- [ ] **Task 6 — Tests** (AC: #1, #2, #3, #4, #5)
  - [ ] Create `pdfjunior.Tests/Services/ErrorMapperTests.cs` (mirrors the other service tests; `ErrorMapper` is a pure function and is the fully-unit-testable core of this story):
    - **Disk full → MC-15 with drive:** `new IOException("disk full") { HResult = unchecked((int)0x80070070) }`, `destinationPath = @"E:\out.pdf"` → assert `== string.Format(UiStrings.MergeErrorDiskFull, @"E:\")`. Add a second case with HResult `0x80070027`.
    - **Access denied → MC-16:** `new UnauthorizedAccessException()` → assert `== UiStrings.MergeErrorAccessDenied`.
    - **Source missing → MC-17 with filename:** `new FileNotFoundException("missing", @"C:\src\gone.pdf")` → assert `== string.Format(UiStrings.MergeErrorFileMissing, "gone.pdf")` (filename only, no directory).
    - **Generic → MC-18:** `new InvalidOperationException("boom")` → assert `== UiStrings.MergeErrorGeneric`.
    - **(Optional) non-disk-full IOException → MC-18:** `new IOException("locked") { HResult = unchecked((int)0x80070020) }` → generic.
  - [ ] In `pdfjunior.Tests/ViewModels/MainViewModelTests.cs`, add the new mock to the factory (the **only** place the new ctor param surfaces — all existing tests keep compiling):
    ```csharp
    private readonly IErrorMapper _errorMapper = Substitute.For<IErrorMapper>();

    private MainViewModel CreateViewModel() =>
        new(_pickerService, _validationService, _mergeService, _outputWriter, _folderLauncher, _errorMapper);
    ```
  - [ ] Add a close-guard cancellation no-op test (the only VM-side close-guard behavior reachable without a window): **`RequestCancelMerge_NoActiveMerge_DoesNotThrow`** — `var vm = CreateViewModel(); vm.RequestCancelMerge();` completes without throwing (`_mergeCts` is null → `?.Cancel()` no-ops).
  - [ ] **Why the failure→banner wiring is not unit-tested:** driving `MergeAsync` to its failure branches requires a real `StorageFile` destination (sealed WinRT type, returned by `PickSaveFileAsync` — cannot be mocked/constructed), exactly as in 2.1/2.2. So the banner *content* on failure is verified by (a) `ErrorMapperTests` (the string mapping) plus (b) the F5 pass (Task 7). Do **not** fake a `StorageFile`.
  - [ ] Run the suite the project's way (Testing Notes): build `pdfjunior.Tests` for `-p:Platform=x64 -r win-x64` and run the produced `pdfjunior.Tests.exe` directly — `dotnet test` does **not** discover these. Keep the current green suite (~56 after 2.2) green and add to it (~6 new: 4–5 `ErrorMapperTests` + `RequestCancelMerge_NoActiveMerge_DoesNotThrow`).

- [ ] **Task 7 — Verify end-to-end (manual VS F5; MSIX cannot launch from CLI)** (AC: #2–#11)
  - [ ] Build clean (0 warnings / 0 errors) for `-p:Platform=x64 -r win-x64` (AC #1).
  - [ ] F5 error checks: (a) **Disk full** — merge to a nearly-full small drive (or a small USB stick) so the write fails → error banner reads "Merge failed — Not enough space on {drive}." with the real drive; banner does **not** auto-dismiss; the file list is intact; UI is unlocked. (b) **Access denied** — merge to a write-protected/locked destination → "Merge failed — Access denied". (c) **Source missing** — start with valid files, delete one source file from disk just before/after pressing Merge so PDFsharp can't read it → "Merge failed — File not found: {name}". (d) **Generic** — any other failure → "Merge failed. Try again or check the files." (e) After any failure, pressing Merge again clears the stale banner (2.2 behavior — re-confirm).
  - [ ] F5 close-guard checks: (f) Start a **long** merge (large set, or throttle) and click the window **X** while it runs → the `ContentDialog` appears (title "Merge in progress", body MC-21, default "Keep merging"). (g) **Keep merging** → dialog closes, merge continues to a success banner; window stays open. (h) Re-run a long merge, click X, choose **Close anyway** → the window closes (a partial/incomplete file may remain at the destination). (i) With **no** merge running, click X → window closes immediately, no dialog. Mark pending until Antoine's F5 pass confirms.

## Dev Notes

### Scope Boundaries (what is and isn't this story)

| In scope (2.3) | Out of scope — already done / not this story |
|---|---|
| `MergeOutcome.Failure(Exception)` shape change (resolves deferred-work) | The merge itself, `OutputWriter`, `FolderLauncher` → **2.2 (done in tree)** |
| `ErrorMapper : IErrorMapper` + DI registration + VM injection | Success banner, Open folder, ~8 s auto-dismiss → **2.2 (done)** |
| Mapper-driven MC-15/16/17/18 in `MergeAsync` (both failure surfaces) | `>2 s` `ProgressBar`, UI-lock (`IsMerging`) → **2.2 (done)** |
| `AppWindow.Closing` close guard + `ContentDialog` (MC-20…23) | Merge gating / `MergeDisabledReason` / Save dialog → **2.1 (done)** |
| `MainViewModel.RequestCancelMerge()` (cancels existing `_mergeCts`) | Adding/altering any `UiStrings` (MC-15…23 already exist) |
| `ErrorMapperTests` + factory mock + cancel no-op test | Changing `MergeOutcome.Success`, `CanMerge`, or the success/progress flow |

Do not pull success/progress/gating work — it's already in the working tree. This story only adds the *failure messaging* and the *close guard*.

### The `{drive}` problem and why `IErrorMapper` gains a parameter (read carefully)

The architecture fixed `IErrorMapper.MapToUserMessage(Exception ex)`. But MC-15 ("Not enough space on **{drive}**.") needs the destination drive root, and a disk-full `IOException` **does not carry the path**. Two facts drive the design:

- **Disk-full drive** comes only from the **destination** (`destination.Path`, known to the VM). So the mapper needs that context → the signature gains `string? destinationPath = null`. This keeps *all* taxonomy + string formatting in the single mapper (architecture: "the exception → microcopy mapping lives in exactly one place"), rather than splitting the `string.Format` into the VM.
- **Source filename** (MC-17) comes from `FileNotFoundException.FileName`, which the exception *does* carry — no extra context needed.

This is the one interface change in the story; it is additive (optional param) and does not affect any other caller. *(Flagged for confirmation — see "Questions for Antoine".)*

### Two distinct failure surfaces → two map sites, one mapper

```
MainViewModel.MergeAsync
 ├─ _mergeService.MergeAsync(...)  → MergeOutcome.Failure(ex)   // SOURCE read errors (PDFsharp): FileNotFound / corrupt
 │       └─ ShowError(_errorMapper.MapToUserMessage(failure.Exception, destination.Path))   // MC-17 / MC-18
 └─ _outputWriter.WriteAsync(...)  → throws                     // DESTINATION write errors: disk-full / access-denied
         └─ catch (Exception ex) → ShowError(_errorMapper.MapToUserMessage(ex, destination.Path))   // MC-15 / MC-16
```

- The merge runs into an **in-memory `MemoryStream`** (2.2's bridge), so disk-full/access-denied can only occur in `OutputWriter.WriteAsync` (the `catch` path), and source-missing/corrupt only in `PdfSharpMergeService` (the `Failure` path). Passing `destination.Path` to both call sites is harmless and correct (the drive is only consulted for the disk-full case).
- **Residual risk (note, not a blocker):** whether PDFsharp surfaces a vanished source as a BCL `FileNotFoundException` (with `FileName` set) is engine-dependent. If it wraps it differently, the message falls through to the generic MC-18 — acceptable and consistent with the architecture's documented validation/merge-engine-mismatch residual risk. Re-verify in the F5 source-missing check; if it lands on MC-18, note it in `deferred-work.md` rather than forcing a brittle pre-check.

### Close-guard specifics (FR-12 — AC #8–#11)

- **Hook:** `AppWindow.Closing` (`Microsoft.UI.Windowing`) is the *cancelable* close event; `AppWindowClosingEventArgs.Cancel` must be set **synchronously** in the handler. `Window.Closed` is too late (not cancelable). The existing ctor already touches `AppWindow` (`AppWindow.Resize`), so subscribing there is safe.
- **Async dialog from a sync handler:** set `args.Cancel = true` first, then launch `ShowCloseGuardAsync()` fire-and-forget (`_ = …`). On **Close anyway**, cancel the merge, set `_forceClose = true`, and call `Close()`; the re-raised `Closing` sees `_forceClose` and lets the window go.
- **Guard only while merging:** gate on `ViewModel.IsMerging`. EXPERIENCE.md engages the guard during merge execution; gating on `IsMerging` covers both the <2 s and ≥2 s windows simply and correctly (AC #11: no dialog when not merging).
- **Button mapping:** Primary = "Keep merging" (default, safe) → `ContentDialogResult.Primary`; Secondary = "Close anyway" → `ContentDialogResult.Secondary`. `DefaultButton = ContentDialogButton.Primary` makes Enter/Esc keep merging.
- **Cooperative cancel only:** `RequestCancelMerge` cancels `_mergeCts`; `PdfSharpMergeService` checks the token between file imports and throws `OperationCanceledException`, which `MergeAsync` swallows silently (no banner — the window is closing). A partial/incomplete file may remain (FR-11/FR-12 permit this; the app does not clean up). For a tiny/fast merge the cancel may not be observed before completion — acceptable ("cooperatively").
- **Stale dialog edge case:** if the merge finishes while the dialog is open, "Close anyway" cancels an already-disposed/null `_mergeCts` (no-op via `?.`) and still closes; "Keep merging" just closes the now-stale dialog with the merge already done. Both are benign.

### Current state of the files you will touch (read before modifying)

- **`pdfjunior/Models/MergeOutcome.cs`** *(change):* `Failure(string Reason)` → `Failure(Exception Exception)`. `Success(string Path)` unchanged.
- **`pdfjunior/Services/IErrorMapper.cs`** *(change):* currently `string MapToUserMessage(Exception ex);` — add the optional `string? destinationPath = null`.
- **`pdfjunior/Services/ErrorMapper.cs`** *(NEW):* the only implementation of `IErrorMapper`; the file does not exist yet (only the interface).
- **`pdfjunior/Services/PdfSharpMergeService.cs`** *(one line):* `new MergeOutcome.Failure(ex.Message)` → `new MergeOutcome.Failure(ex)` (line ~28). The `catch (OperationCanceledException) { throw; }` stays.
- **`pdfjunior/ViewModels/MainViewModel.cs`** *(change):* add 6th ctor param `IErrorMapper errorMapper` + field; add `public void RequestCancelMerge() => _mergeCts?.Cancel();`; in `MergeAsync` replace the two `ShowError(UiStrings.MergeErrorGeneric)` calls with `_errorMapper.MapToUserMessage(...)` calls and change the bare `catch` to `catch (Exception ex)`. `_mergeCts`, `ShowError`, `ShowSuccess`, `IsMerging`, the `finally` block all already exist (2.2) — do not duplicate them.
- **`pdfjunior/MainWindow.xaml.cs`** *(change):* subscribe `AppWindow.Closing` in the ctor; add `OnAppWindowClosing` + `ShowCloseGuardAsync` + `_forceClose`/`_closeGuardOpen`; add `using Microsoft.UI.Windowing;`. `ViewModel`, `Content`, `AppWindow`, and `Close()` are all already available. No other code-behind changes.
- **`pdfjunior/App.xaml.cs`** *(one line):* add `services.AddSingleton<IErrorMapper, ErrorMapper>();` to `ConfigureServices()` (alongside the 2.2 merge/output/folder singletons).
- **`pdfjunior/MainWindow.xaml`** *(no change):* the success/error `InfoBar`s + `ProgressBar` are already wired (2.2). The close guard is code-behind + `ContentDialog`. **Do not edit the XAML.**
- **`pdfjunior/Strings/UiStrings.cs`** *(no change):* MC-15 `MergeErrorDiskFull`, MC-16 `MergeErrorAccessDenied`, MC-17 `MergeErrorFileMissing`, MC-18 `MergeErrorGeneric`, MC-20…23 `CloseGuard*` — **all already defined**. Do not add strings.
- **`pdfjunior.Tests/ViewModels/MainViewModelTests.cs`** *(factory + 1 test):* `CreateViewModel()` injects 5 mocks today; add `_errorMapper`. Add the cancel no-op test.
- **`pdfjunior.Tests/Services/PdfSharpMergeServiceTests.cs`** *(no change expected):* asserts only `MergeOutcome.Success`; the `Failure` shape change does not touch it. Confirm it still compiles/passes.

### Architecture compliance (guardrails)

- **MVVM:** failure messaging lives in `MainViewModel` (orchestration) + `ErrorMapper` (taxonomy). The close-guard `ContentDialog` is the **one sanctioned code-behind exception** (architecture boundaries: "no business logic in code-behind (only `InitializeComponent`, HWND wiring, **and the close-guard dialog**)"). Use CommunityToolkit generators elsewhere; no hand-rolled INPC.
- **DI:** constructor injection only; `IErrorMapper` is a singleton in the single composition root (`App.ConfigureServices`). No service-locator in the VM.
- **Services boundary:** `ErrorMapper` returns a `string` (canonical microcopy); exception types and HResult inspection stay inside it. PDFsharp/WinRT exception types do not leak into the View. `RequestCancelMerge` exposes only intent, not the CTS.
- **Result types:** services return `MergeOutcome` (now `Failure(Exception)`); the VM maps it. Expected failures do not throw across the merge-service boundary (PDFsharp catches and returns `Failure`); `OutputWriter` write failures throw and are caught/mapped in the VM.
- **Strings:** every banner/dialog string is an existing `UiStrings` constant via `string.Format` where needed; drive root and filename are file-system facts, not microcopy. No inline literals.
- **Theming:** `ContentDialog` and the error `InfoBar` use stock WinUI styling (DESIGN: ContentDialog overlay, `OverlayCornerRadius` 8px; error `InfoBar` Severity=Error). No custom colors/brushes/radii.
- **Async/threading:** the close handler sets `args.Cancel` synchronously, shows the dialog async; `RequestCancelMerge` runs on the UI thread (dialog callback). Never block the UI thread; no `.Result`/`.Wait()`.
- **Nullable:** honor `<Nullable>enable</Nullable>` — null-check `fnf.FileName`, `Path.GetPathRoot(...)`, `_mergeCts` (via `?.`) rather than `!`.
- **Privacy:** no new dependencies, no network/telemetry; any logging is `System.Diagnostics.Debug` only. Error messages never include full paths beyond the drive root / bare filename the UX spec prescribes.

### Anti-patterns to avoid

- Do **not** keep `MergeOutcome.Failure(string)` — the exception must reach the mapper (this is the whole point of the deferred item).
- Do **not** put the disk-full/file-missing `string.Format` in the VM — it belongs solely in `ErrorMapper` (single-place taxonomy).
- Do **not** check the disk-full `IOException` branch *before* `FileNotFoundException` — FNF is an `IOException` subclass and must match first.
- Do **not** localize/parse `ex.Message` to classify errors — use type + HResult (the deferred-work log already flags locale-dependent string matching as debt; don't add more).
- Do **not** set `args.Cancel` after an `await` — it must be synchronous; show the dialog fire-and-forget.
- Do **not** use `Window.Closed` (not cancelable) or try to cancel from there; use `AppWindow.Closing`.
- Do **not** forget `XamlRoot` on the `ContentDialog` (WinUI 3 throws without it).
- Do **not** show the guard when `IsMerging` is false (AC #11), and do **not** stack multiple dialogs (use `_closeGuardOpen`).
- Do **not** add a banner on the close-anyway `OperationCanceledException` (window is closing — keep it silent).
- Do **not** add an auto-dismiss timer to the error banner (manual dismiss only — already correct in 2.2's `ShowError`).
- Do **not** mutate `Files` on failure (AC #7) or change the success/progress/gating flow.
- Do **not** construct or mock `StorageFile` in tests; the failure→banner happy path is F5-only.
- Do **not** add/modify any `UiStrings` constant (MC-15…23 are final and present).

### Previous story intelligence

**From Story 2.2 (the surface you extend — in the working tree, uncommitted):**
- `MainViewModel.MergeAsync` is fully implemented: clears banners → Save dialog → `_mergeCts = new(...)` → `IsMerging = true` → `StartProgressDelay()` → merge into `MemoryStream` → `OutputWriter.WriteAsync` → `ShowSuccess` / (currently) generic `ShowError`, all in a `try/catch(OCE)/catch/finally`. The `finally` sets `IsProgressVisible = false; IsMerging = false;` and disposes `_mergeCts`. **Your AC #6/#7 (UI unlock + list preserved) already hold** — just don't regress them.
- `_mergeCts` is created per merge and **nothing cancels it yet** — 2.2 explicitly left that for the close guard. `RequestCancelMerge` is the trigger.
- `ShowError(text)` closes the success banner, sets `ErrorBannerText`, opens the error `InfoBar`, and starts **no** timer (manual dismiss). Reuse it verbatim — only change the *argument* from the generic string to the mapper output.
- 2.2's note: "*Do not change `MergeOutcome` shape; 2.3 will revisit it to carry the exception for `IErrorMapper`.*" — that is exactly Task 1.
- The error `InfoBar` (Severity=Error, manual dismiss) and `ProgressBar` are already in `MainWindow.xaml` Row 1. No XAML change this story.

**From the deferred-work log:**
- *"`MergeOutcome.Failure` carries only a `string Reason`, not the original exception — `IErrorMapper` cannot be used on merge failures without the exception."* → resolved by Task 1. Update `deferred-work.md` to strike this item when done (mirror the strike-through convention used for the resolved composability item).

**From Epic 1 + 2.1/2.2 testing posture:**
- Tests run via the **direct-exe** path (`pdfjunior.Tests.exe` for `-p:Platform=x64 -r win-x64`); `dotnet test` does not discover them (memory `project_run_tests`). `DispatcherQueue.GetForCurrentThread()` is null in the test host, so `RunOnUI` runs inline.
- MSIX visuals (`ContentDialog`, `InfoBar` rendering, real `StorageFile` write failures) are **manual VS F5** — state F5 items pending, never fake automated E2E. This matches 1.x / 2.1 / 2.2.
- `[ObservableProperty]`/`[RelayCommand]` use **partial** members. `_mergeCts`/`ShowError`/`IsMerging` already exist — reuse, don't re-add.

### Git intelligence

Baseline is `ee64fc1`; Stories 2.1 + 2.2 are **uncommitted** in the working tree (so this story sits on top of an as-yet-uncommitted Epic-2 base). Recent cadence is one focused `feat:` commit per story (`dd65d1e` 1.2, `07e2d78` 1.3, `859232b`/`03155fb`/`0bcd2ce` reorder+1.4+webview) plus `docs:` reconciliations. Follow the same one-clean-`feat:`-commit convention (e.g. `feat: implement story 2-3 error handling and close guard`). This story is **smaller** than 2.2 (one new file `ErrorMapper.cs`, one new test file, targeted edits to 5 existing files). Whether to commit 2.1/2.2/2.3 separately or together is Antoine's call — do not commit unless asked.

### Project Structure Notes

```
pdfjunior/
└── pdfjunior/
    ├── App.xaml.cs                      [UPDATE] — register IErrorMapper → ErrorMapper singleton
    ├── MainWindow.xaml.cs               [UPDATE] — AppWindow.Closing guard + ContentDialog (MC-20…23); RequestCancelMerge call
    ├── Models/
    │   └── MergeOutcome.cs              [UPDATE] — Failure(string) → Failure(Exception)
    ├── Services/
    │   ├── IErrorMapper.cs              [UPDATE] — add optional destinationPath param
    │   ├── ErrorMapper.cs               [NEW]    — exception → MC-15/16/17/18 (single taxonomy)
    │   └── PdfSharpMergeService.cs      [UPDATE] — Failure(ex) instead of Failure(ex.Message)
    └── ViewModels/
        └── MainViewModel.cs             [UPDATE] — +IErrorMapper ctor dep; RequestCancelMerge(); mapper-driven ShowError in MergeAsync
pdfjunior.Tests/
├── Services/ErrorMapperTests.cs        [NEW]    — disk-full(+drive)/access-denied/file-missing(+name)/generic
└── ViewModels/MainViewModelTests.cs    [UPDATE] — factory +1 mock (_errorMapper); RequestCancelMerge no-op test
```

No `UiStrings` change, no `MainWindow.xaml` change, no new NuGet packages. `Success` shape, gating, success/progress flow unchanged.

### Questions for Antoine (resolve at review, non-blocking)

1. **`IErrorMapper` signature extension.** MC-15 needs the destination **drive**, which a disk-full `IOException` doesn't carry, so the story extends the architecture's `MapToUserMessage(Exception ex)` to `MapToUserMessage(Exception ex, string? destinationPath = null)` to keep all formatting in the one mapper. Confirm that's acceptable vs. having the VM format the drive (which would split the taxonomy). Recommended: keep the extension.
2. **Source-missing classification.** AC #4 (MC-17 "File not found: {name}") depends on PDFsharp throwing a BCL `FileNotFoundException` with `FileName` set for a vanished source. If F5 shows it landing on the generic MC-18 instead, is the generic message acceptable for that edge (and the specific message deferred), or should we add a pre-write source-existence check? Recommended: accept generic fallback + deferred note (avoids a racy pre-check).

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story 2.3: Error Handling & Close Guard] — story statement + AC seed (FR-11, FR-12)
- [Source: _bmad-output/planning-artifacts/epics.md#Requirements Inventory] — FR-11 (descriptive error: disk full / write denied / source vanished / generic; manual dismiss; partial output permitted; list preserved), FR-12 (close-during-merge guard: Keep merging default / Close anyway cancels cooperatively)
- [Source: _bmad-output/planning-artifacts/architecture.md#Error Taxonomy (exception → canonical microcopy)] — `MergeOutcome` Success|Failure; IOException 0x70/0x27 → MC-15; `UnauthorizedAccessException` → MC-16; `FileNotFoundException` → MC-17; else → MC-18; open-folder missing → MC-19 (2.2)
- [Source: _bmad-output/planning-artifacts/architecture.md#Output / File-I/O Safety] — direct write, no temp file/rollback; cooperative `CancellationToken` for the close guard; partial output permitted on cancel/failure
- [Source: _bmad-output/planning-artifacts/architecture.md#Concurrency & Cancellation Model] — single `Task.Run` + `CancellationToken`; token checked between file imports; close-guard owns the merge token
- [Source: _bmad-output/planning-artifacts/architecture.md#Error & Result Patterns] — services return result types; the exception→microcopy mapping lives in **exactly one place** (the mapper)
- [Source: _bmad-output/planning-artifacts/architecture.md#Architectural Boundaries] — code-behind limited to `InitializeComponent`, HWND wiring, **and the close-guard dialog** (sanctions the FR-12 code-behind)
- [Source: _bmad-output/planning-artifacts/architecture.md#Requirements → Structure Mapping] — FR-11 → `ErrorMapper` + VM error `InfoBar`; FR-12 → MainWindow `AppWindow.Closing` + `ContentDialog` + merge `CancellationToken`
- [Source: _bmad-output/planning-artifacts/ux-designs/ux-pdf-junior-2026-06-14/EXPERIENCE.md#Microcopy Inventory] — MC-15/16/17/18 (error strings) + MC-20/21/22/23 (close-guard dialog), verbatim
- [Source: _bmad-output/planning-artifacts/ux-designs/ux-pdf-junior-2026-06-14/EXPERIENCE.md#State Patterns] — "Merge failure → InfoBar(Error) MC-15…18, manual dismiss, list preserved, UI unlocked, partial file may remain"; "Close during merge → ContentDialog MC-20/21/22/23, default Keep merging, Close anyway cancels cooperatively"
- [Source: _bmad-output/planning-artifacts/ux-designs/ux-pdf-junior-2026-06-14/EXPERIENCE.md#Voice and Tone] — "Honest, specific reason, no apology filler"; specific reasons are the expected default, generic only when no detail is available
- [Source: _bmad-output/planning-artifacts/ux-designs/ux-pdf-junior-2026-06-14/EXPERIENCE.md#Key Flows] — Flow 4 (David, disk full): "Merge failed — Not enough space on E:\.", no auto-dismiss, list intact, retry clears the stale banner
- [Source: _bmad-output/planning-artifacts/ux-designs/ux-pdf-junior-2026-06-14/DESIGN.md#Components] — `ContentDialog` close guard: Primary "Keep merging" (default), Secondary "Close anyway"; standard overlay, `OverlayCornerRadius` 8px; error `InfoBar` Severity=Error, manual dismiss; stock WinUI, no custom colors/radii
- [Source: _bmad-output/implementation-artifacts/2-2-execute-merge-report-success.md] — the `MergeAsync` body, `_mergeCts`, `ShowError`/`ShowSuccess`, `IsMerging` lock, `finally` unlock you extend; note "do not change `MergeOutcome` shape — 2.3 carries the exception"
- [Source: _bmad-output/implementation-artifacts/2-1-merge-gating-save-dialog.md] — `StorageFile` is sealed/unmockable → confirm/failure paths are F5-only; gating/Save-dialog context
- [Source: _bmad-output/implementation-artifacts/deferred-work.md] — "`MergeOutcome.Failure` carries only a string, not the exception — `IErrorMapper` cannot be used without it" (resolved by this story)
- [Source: pdfjunior/Models/MergeOutcome.cs] — `Success(string Path)` | `Failure(string Reason)` → change `Failure`
- [Source: pdfjunior/Services/IErrorMapper.cs] — `string MapToUserMessage(Exception ex)` to extend + implement
- [Source: pdfjunior/Services/PdfSharpMergeService.cs] — `catch (Exception ex) { return new MergeOutcome.Failure(ex.Message); }` → `Failure(ex)`
- [Source: pdfjunior/ViewModels/MainViewModel.cs] — `MergeAsync` (two `ShowError(MergeErrorGeneric)` sites + bare `catch`), `_mergeCts`, `ShowError`, `IsMerging`, `finally`; add `RequestCancelMerge`
- [Source: pdfjunior/MainWindow.xaml.cs] — ctor (`AppWindow.Resize`, `ViewModel`, `Hwnd`), code-behind patterns; add `AppWindow.Closing` guard + `ContentDialog`
- [Source: pdfjunior/App.xaml.cs] — `ConfigureServices()` singletons (add `IErrorMapper`)
- [Source: pdfjunior/Strings/UiStrings.cs] — MC-15/16/17/18 + MC-20/21/22/23 already defined (no change)
- [Source: pdfjunior.Tests/ViewModels/MainViewModelTests.cs] — `CreateViewModel()` factory (add `_errorMapper`); test templates
- [Source: pdfjunior.Tests/Services/PdfValidationServiceTests.cs] — service-test layout to mirror for `ErrorMapperTests`
- [Source: memory project_run_tests] — build x64/win-x64 and run `pdfjunior.Tests.exe` directly; `dotnet test` does not discover
- [Source: memory feedback_msix_packaging] — `WindowsPackageType=MSIX` (app project) — unchanged this story
- [Source: memory project_pdfsharp_winrt_engine_split] — validation uses WinRT, merge uses PDFsharp; a file that validates can still fail PDFsharp at merge → surfaces via this story's error path (residual-risk note)

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

### File List

## Change Log

| Date | Version | Description |
|---|---|---|
| 2026-06-20 | 0.1.0 | Story drafted (ready-for-dev): error taxonomy (`MergeOutcome.Failure(Exception)` + `ErrorMapper` → MC-15/16/17/18) wired into `MergeAsync`; window-close guard (`AppWindow.Closing` + `ContentDialog` MC-20…23 + `RequestCancelMerge`). |
