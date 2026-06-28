---
title: 'Fix two edge cases deferred from the spec-remove-mvvm code review'
type: 'bugfix'
created: '2026-06-28'
status: 'done'
baseline_commit: '3c31cf7fcd51a330b2c213d3b8797d82b3d6dccc'
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The remove-MVVM refactor was a 1:1 parity port, so two exception/staleness edge cases were deferred rather than fixed (deferred-work.md §"code review of spec-remove-mvvm (2026-06-28)"): (1) the preview-render `catch` in `UpdatePreviewAsync` guards only on `ReferenceEquals(item, _selectedFile)`, missing the `!cts.IsCancellationRequested` check the success path has — a superseded-then-re-selected file whose stale render later throws a non-OCE can overwrite a freshly-rendered Valid preview with the corrupt placeholder; (2) `AddButton_Click` / `MergeButton_Click` are `async void` with the file-picker `await` outside any try, so a picker exception becomes an unhandled app crash instead of the old `AsyncRelayCommand` unobserved-task fault.

**Approach:** Make the preview `catch` guard match the success path (add `!cts.IsCancellationRequested`). Wrap each file-picker `await` in a try/catch that no-ops on failure, restoring the pre-refactor non-crash semantics. Both are behavior-hardening fixes with no user-visible change on the happy path.

## Boundaries & Constraints

**Always:**
- Preserve every concurrency invariant from spec-remove-mvvm (single in-flight preview render + staleness guard, validation semaphore/timeout, per-merge CTS + progress, dedupe, selection-shift).
- The preview `catch` fix must read as the same guard as the success path so the two stay obviously symmetric.
- Build/test entry points unchanged (`-p:Platform=x64 -r win-x64`).

**Ask First:**
- Any user-visible change to picker-failure behavior beyond "silent no-op" (e.g. showing an error dialog) — none intended.

**Never:**
- No new MVVM/INPC/commands — the file stays code-behind with event handlers.
- Do not change the merge flow's own try/catch/finally, the validation pipeline, or any service logic.
- Do not swallow the picker `catch` so broadly that it also hides exceptions from the post-pick work (add/validate, merge) — wrap only the picker `await`, not the whole handler.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Behavior | Error Handling |
|----------|--------------|-------------------|----------------|
| Stale render throws after re-select | Select A, select B, re-select A; A's first (cancelled) render later throws a non-OCE | Fresh A render wins; corrupt placeholder is NOT shown | `catch` no-ops because `cts.IsCancellationRequested` is true for the stale render |
| Current render genuinely fails | Selected Valid file fails to render (not cancelled) | Corrupt placeholder shows (unchanged) | `catch` runs `SetPreview(ExcludedCorrupt)` because cts not cancelled and item still selected |
| Add picker throws | `PickFilesAsync` throws | No crash; no files added; UI unchanged | wrap `await` in try/catch; `return` |
| Save picker throws | `PickSaveFileAsync` throws | No crash; silent no-op; no merge lock engaged | wrap `await` in try/catch; `return` before `SetIsMerging(true)` |

</frozen-after-approval>

## Code Map

- `vibepdf/MainWindow.xaml.cs` — only file touched. `UpdatePreviewAsync` catch (~L292), `AddButton_Click` (~L100), `MergeButton_Click` (~L349).
- `vibepdf/Services/IFilePickerService.cs` — reference: pickers return empty/`null` on cancel (no throw), so the catch is for genuine failures only.

## Tasks & Acceptance

**Execution:**
- [x] `vibepdf/MainWindow.xaml.cs` `UpdatePreviewAsync` — change the non-OCE `catch` guard to `if (!cts.IsCancellationRequested && ReferenceEquals(item, _selectedFile))` so a stale (cancelled) render can no longer overwrite the current preview — matches the success-path guard.
- [x] `vibepdf/MainWindow.xaml.cs` `AddButton_Click` — wrap only the `await _filePickerService.PickFilesAsync()` call in try/catch; on exception `return` (no files added, no crash).
- [x] `vibepdf/MainWindow.xaml.cs` `MergeButton_Click` — wrap only the `await _filePickerService.PickSaveFileAsync(...)` call in try/catch; on exception `return` before any merge state is engaged (silent no-op, parity with cancelling the Save dialog).

**Acceptance Criteria:**
- Given a stale, cancelled preview render that later throws a non-OCE while its file is the current selection again, when the catch runs, then the corrupt placeholder is not shown and the fresh render stands.
- Given a picker that throws, when the handler runs, then the app does not crash and no partial state is left (Add: no items added; Merge: no lock, no title %, no dialog).
- Given `dotnet build vibepdf/vibepdf.csproj -p:Platform=x64 -r win-x64`, then it succeeds; given `dotnet test vibepdf.Tests/vibepdf.Tests.csproj -p:Platform=x64 -r win-x64`, then the existing suite stays green.

## Design Notes

- **Picker catch is silent by design.** The old `AsyncRelayCommand` surfaced a post-`await` picker fault as an unobserved task exception — no crash, no user feedback. A bare `catch { return; }` around just the picker `await` restores exactly that. Both pickers already treat cancellation as empty/`null` (not an exception), so the catch only fires on real failures, which are near-unreachable. Showing an error dialog would be new UX and is out of scope (Ask First).
- **Why no unit tests.** Both paths are WinUI-host- and UI-thread-bound (`BitmapImage` is a `DependencyObject`; handlers need a realized `Window`). Consistent with spec-remove-mvvm, which deleted `MainViewModelTests` and relies on the service-layer suite + the F5 parity walkthrough; these two fixes are verified the same way (build + inspection of guard symmetry + manual walkthrough), not by adding a brittle host-bound test.

## Verification

**Commands:**
- `dotnet build vibepdf/vibepdf.csproj -p:Platform=x64 -r win-x64` — expected: succeeds.
- `dotnet test vibepdf.Tests/vibepdf.Tests.csproj -p:Platform=x64 -r win-x64` — expected: existing tests green.

**Manual checks:**
- Inspect that the `UpdatePreviewAsync` catch guard is now textually symmetric with the success-path guard (both gate on `!cts.IsCancellationRequested` + `ReferenceEquals`).
- F5 happy-path walkthrough (add → validate → preview → merge) behaves identically; cancelling the Add and Save pickers remains a silent no-op.

## Suggested Review Order

**Preview staleness fix (start here)**

- The one-line fix: catch guard now mirrors the success path so a stale render can't clobber a fresh preview.
  [`MainWindow.xaml.cs:309`](../../vibepdf/MainWindow.xaml.cs#L309)

**Picker crash-safety (async void hardening)**

- Add handler: only the picker `await` is wrapped; failure no-ops, the add/validate loop stays outside the try.
  [`MainWindow.xaml.cs:101`](../../vibepdf/MainWindow.xaml.cs#L101)
- Merge handler: only the Save-picker `await` is wrapped; returns before any merge state engages.
  [`MainWindow.xaml.cs:363`](../../vibepdf/MainWindow.xaml.cs#L363)

**Peripheral**

- `using Windows.Storage;` added because `StorageFile?` is now named explicitly (was `var`).
  [`MainWindow.xaml.cs:17`](../../vibepdf/MainWindow.xaml.cs#L17)
