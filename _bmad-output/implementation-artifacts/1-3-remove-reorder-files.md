---
baseline_commit: dd65d1e
---

# Story 1.3: Remove & Reorder Files

Status: review

> **Updated 2026-06-18:** Reorder changed from Move up / Move down buttons to native `ListView` drag-and-drop (commit 859232b). ACs 4–6 below and the Change Log entry reflect this. The Tasks/Subtasks and Dev Notes further down document the original 2026-06-16 button implementation and are retained as history — see the Change Log for what superseded them.

## Story

As a user,
I want to remove unwanted files and reorder the remaining ones,
so that I control exactly which files are merged and in what order.

## Acceptance Criteria (BDD)

1. **Given** a file is selected in the File list **When** the user clicks Remove in the Preview toolbar **Then** the file is removed from the list, selection clears, and the Preview pane returns to "Select a file to preview it" (MC-2).

2. **Given** no file is selected **When** the user views the Preview toolbar **Then** Remove is disabled.

3. **Given** a flagged file (error-password, error-corrupt, or error-timeout) is selected **When** the user clicks Remove **Then** it is removed exactly like any other file.

4. **Given** a file is dragged to a new position in the File list **When** the user drops it **Then** the File list reorders to match the drop and the bound `Files` collection reflects the new display order (so Merge consumes the new order).

5. **Given** a file is dragged to a new position **When** the user drops it **Then** the dragged file remains the Selected file **And** the Preview pane continues to show it without reloading.

6. **Given** a file in any validation status (checking, valid, or flagged) **When** the user drags it **Then** it can be reordered like any other item.

## Tasks / Subtasks

- [x] Task 1: Implement `Remove` command in MainViewModel (AC: #1, #2, #3)
  - [x] Replace the empty `Remove()` body: capture `SelectedFile` into a local, return early if null, then `Files.Remove(item)` and set `SelectedFile = null`
  - [x] `CanRemove()` stays `SelectedFile is not null` — already correct; do not change the gate
  - [x] Cancel the removed item's in-flight validation if present (see Task 4) — required so the semaphore slot frees and no status is written back to a removed item
  - [x] Flagged items have no special path — `Files.Remove` is status-agnostic (AC #3 needs no extra code, but cover it with a test)

- [x] Implement `MoveUp` / `MoveDown` commands in MainViewModel (AC: #4, #5, #6, #7, #8)
  - [x] `MoveUp()`: capture `SelectedFile`, get `index = Files.IndexOf(item)`, guard `index > 0`, call `Files.Move(index, index - 1)` — **never reassign `SelectedFile`**
  - [x] `MoveDown()`: capture `SelectedFile`, get `index`, guard `index < Files.Count - 1`, call `Files.Move(index, index + 1)` — **never reassign `SelectedFile`**
  - [x] Tighten `CanMoveUp()` → `SelectedFile is not null && Files.IndexOf(SelectedFile) > 0`
  - [x] Tighten `CanMoveDown()` → `SelectedFile is not null && Files.IndexOf(SelectedFile) < Files.Count - 1`
  - [x] After each `Files.Move`, call `MoveUpCommand.NotifyCanExecuteChanged()` and `MoveDownCommand.NotifyCanExecuteChanged()` (index changed but `SelectedFile` reference did not, so the `[NotifyCanExecuteChangedFor]` on the setter will NOT fire)

- [x] Re-evaluate move command enablement on collection changes (AC: #6, #7)
  - [x] In `OnFilesCollectionChanged`, after `NotifyMergeStateChanged()`, also notify `MoveUpCommand` / `MoveDownCommand` / `RemoveCommand` CanExecuteChanged — adding/removing files shifts the selected item's position and the list bounds (e.g. selected item becomes last → Move down must disable)

- [x] Cancel in-flight validation on removal + guard writes to removed items (addresses deferred items from 1-2)
  - [x] Add `private readonly Dictionary<PdfFileItem, CancellationTokenSource> _validationCts = [];`
  - [x] In `ValidateFileAsync`, register the per-item CTS in the dictionary at the start and remove+dispose it in an outer `finally`
  - [x] In `Remove()`, if `_validationCts.TryGetValue(item, out var cts)` → `cts.Cancel()` before `Files.Remove(item)`
  - [x] Inside every `RunOnUI(...)` status-write callback in `ValidateFileAsync`, guard with `if (!Files.Contains(item)) return;` so a late-completing validation never writes status to a removed item
  - [x] See "In-flight validation cancellation on removal" in Dev Notes for the exact pattern

- [x] Verify XAML toolbar bindings (AC: #1–#8) — likely no change needed
  - [x] Confirm the three toolbar `Button`s already bind `Command="{x:Bind ViewModel.MoveUpCommand/MoveDownCommand/RemoveCommand, Mode=OneTime}"` (they do as of story 1-2) — button enable/disable is driven by `ICommand.CanExecute`, so no XAML change is required
  - [x] Do NOT add `IsEnabled` bindings on the toolbar buttons — command `CanExecute` already controls enablement; a manual `IsEnabled` binding would conflict

- [x] Write unit tests (AC: #1–#8)
  - [x] Update `pdfjunior.Tests/ViewModels/MainViewModelTests.cs`
  - [x] Remove: selected file removed from `Files`, `SelectedFile` becomes null, `HasFiles` false when last file removed
  - [x] Remove of a flagged item (ErrorPassword / ErrorCorrupt / ErrorTimeout) succeeds (AC #3)
  - [x] Remove recomputes `CanMerge` (e.g. removing the only flagged file flips `CanMerge` true)
  - [x] MoveUp/MoveDown: item changes index, **`SelectedFile` is the same reference** after the move (AC #4/#5 "remains selected, no reload")
  - [x] `CanMoveUp` false when first item selected / true otherwise; `CanMoveDown` false when last item selected / true otherwise (AC #6/#7)
  - [x] `CanMoveUp` / `CanMoveDown` / `CanRemove` (via `*Command.CanExecute(null)`) all false when `SelectedFile` is null (AC #8)
  - [x] Removing a still-checking item: its later-completing (hanging) validation does not change `Files` and does not flip merge state (covers the cancellation/guard work)

- [x] Verify end-to-end (AC: #1–#8) — requires Visual Studio F5 (MSIX app cannot launch from CLI; see Testing Notes)
  - [x] Select a file → Remove → item disappears, nothing selected, preview shows "Select a file to preview it"; remove last file → sidebar shows "Add PDFs to get started"
  - [x] Select a middle file → Move up / Move down → item reorders, stays selected (highlighted), no flicker
  - [x] First item selected → Move up greyed, Move down active; last item selected → Move down greyed, Move up active; nothing selected → all three greyed

> **E2E verification note:** The build (0 warnings/0 errors) and all 39 unit tests pass and were verified in this session. The MSIX app cannot be launched from the CLI (`dotnet run` won't deploy it), so the **visual** F5 checks above were NOT performed in this CLI session — they are left for Antoine's manual Visual Studio F5 pass during review, matching the 1-1/1-2 convention. The VM-level behaviors underlying each visual check are covered by the unit tests.

## Dev Notes

### Current Project State (UPDATE files — read before modifying)

**`pdfjunior/ViewModels/MainViewModel.cs`** — The only file with real logic changes. Current relevant state:
- `Files` is `ObservableCollection<PdfFileItem>`. `SelectedFile` is an `[ObservableProperty]` already decorated with `[NotifyCanExecuteChangedFor]` for `MoveUpCommand`, `MoveDownCommand`, and `RemoveCommand` — so changing `SelectedFile` already re-evaluates all three commands' `CanExecute`.
- `MoveUp()`, `MoveDown()`, `Remove()` are **empty stubs**. `CanMoveUp()`/`CanMoveDown()`/`CanRemove()` all currently return `SelectedFile is not null` — only `CanRemove` is final; the two Move gates need index/bounds checks.
- `OnFilesCollectionChanged` subscribes/unsubscribes each item's `PropertyChanged` and calls `NotifyMergeStateChanged()`. It already unsubscribes items in `e.OldItems` on removal — so a removed item's later `Status` change will NOT recompute `CanMerge` (good; one less thing to worry about). Extend this method to also notify the Move/Remove commands.
- `ValidateFileAsync` uses `using var cts = new CancellationTokenSource()` plus `Task.Run` + `Task.Delay(5s)` + `Task.WhenAny` for the wall-clock guard. It writes status via `RunOnUI(...)`. **Two deferred-debt items from story 1-2 are assigned to this story** and live here (see "In-flight validation cancellation on removal").
- `RunOnUI(action)` runs `action()` inline when `_dispatcherQueue` is null (test context) else marshals via `DispatcherQueue.TryEnqueue`. All `Files` and `PdfFileItem` mutations must go through the UI thread / `RunOnUI`.
- `NotifyMergeStateChanged()` raises `CanMerge` + `MergeDisabledReason` + `MergeCommand.NotifyCanExecuteChanged()`.

**`pdfjunior/MainWindow.xaml`** — Preview toolbar already has the three buttons (Move up `&#xE74A;`, Move down `&#xE74B;`, Remove `&#xE74D;`) each bound `Command="{x:Bind ViewModel.MoveUpCommand/MoveDownCommand/RemoveCommand, Mode=OneTime}"` with hardcoded `ToolTipService.ToolTip` text ("Move up"/"Move down"/"Remove"). The `ListView` (`x:Name="FileListView"`) is `SelectionMode="Single"` with `SelectionChanged="FileListView_SelectionChanged"` (code-behind pushes selection to the VM). **No XAML change is expected for this story** — button enablement is driven entirely by command `CanExecute`.

**`pdfjunior/MainWindow.xaml.cs`** — `FileListView_SelectionChanged` sets `ViewModel.SelectedFile = FileListView.SelectedItem as PdfFileItem`. This is a **one-way View→ViewModel** push; there is no ViewModel→View binding on `SelectedItem`. See "Selection wiring" below for why this matters for Remove vs Move.

**`pdfjunior/Models/PdfFileItem.cs`** — `ObservableObject`; `Id`/`Path`/`DisplayName` are init-only, `Status`/`PageCount` are `[ObservableProperty]`. **No `Equals`/`GetHashCode` override** (deferred item), so `Files.Contains`, `Files.IndexOf`, and `Files.Remove` all use **reference equality** — which is exactly what we want here (operate on the selected instance). Do not add value equality.

**`pdfjunior/Strings/UiStrings.cs`** — MC-1 (`EmptyFileListPlaceholder`) and MC-2 (`EmptyPreviewPlaceholder`) exist. No new strings are required for this story. (The toolbar tooltips "Move up"/"Move down"/"Remove" are not in the EXPERIENCE.md microcopy inventory; leaving them as-is is acceptable — do not invent new canonical strings.)

### Architecture Compliance

- **MVVM:** All logic in `MainViewModel`. Use the existing `[RelayCommand]` generators (`MoveUpCommand`, `MoveDownCommand`, `RemoveCommand` are already generated from the stub methods). No reorder/remove logic in code-behind.
- **Derived/command state:** `CanMoveUp`/`CanMoveDown`/`CanRemove` are the `CanExecute` predicates — keep the gating logic in one place; do not duplicate it in XAML via `IsEnabled`.
- **Threading:** `ObservableCollection` mutations (`Move`, `Remove`) must happen on the UI thread. The command handlers run on the UI thread already (invoked from button click / `RunOnUI` in tests). The validation-cancellation bookkeeping (`_validationCts`) is only touched from the UI thread (command handlers and the resumed continuations of `ValidateFileAsync`, which has no `ConfigureAwait(false)`), so a plain `Dictionary` is safe — no locking needed.
- **Nullable:** Honor `<Nullable>enable</Nullable>`. Capture `SelectedFile` into a local and null-check it at the top of each handler rather than using `!`.
- **No new dependencies:** Reorder uses `ObservableCollection<T>.Move`, remove uses `.Remove` — both in-box. Do NOT add any NuGet package. PDFsharp is still out of scope until story 2-2.

### Reorder via `ObservableCollection<T>.Move` (critical pattern — AC #4, #5)

Use `Files.Move(oldIndex, newIndex)` — **not** remove-then-insert. `Move` raises a single `NotifyCollectionChangedAction.Move` and **preserves the item instance**, which gives us three required behaviors for free:
- The `ListView` keeps its selection on the moved item (selection is tracked by reference), so it stays highlighted at the new position.
- `SelectedFile` (the VM property) is the **same reference** before and after — so when preview rendering lands in story 1.4, the preview will **not reload** (AC #4/#5 "without reloading"). The whole point of `Move` is that nothing observes a selection *change*.
- No `SelectionChanged` fires (the selected item didn't change), so the code-behind won't null out `SelectedFile`.

```csharp
private bool CanMoveUp() => SelectedFile is not null && Files.IndexOf(SelectedFile) > 0;

[RelayCommand(CanExecute = nameof(CanMoveUp))]
private void MoveUp()
{
    var item = SelectedFile;
    if (item is null) return;
    var index = Files.IndexOf(item);
    if (index <= 0) return;
    Files.Move(index, index - 1);
    MoveUpCommand.NotifyCanExecuteChanged();
    MoveDownCommand.NotifyCanExecuteChanged();
}

private bool CanMoveDown() => SelectedFile is not null && Files.IndexOf(SelectedFile) < Files.Count - 1;

[RelayCommand(CanExecute = nameof(CanMoveDown))]
private void MoveDown()
{
    var item = SelectedFile;
    if (item is null) return;
    var index = Files.IndexOf(item);
    if (index < 0 || index >= Files.Count - 1) return;
    Files.Move(index, index + 1);
    MoveUpCommand.NotifyCanExecuteChanged();
    MoveDownCommand.NotifyCanExecuteChanged();
}
```

The explicit `NotifyCanExecuteChanged()` after a move is **required**: the index changed but `SelectedFile`'s reference did not, so the `[NotifyCanExecuteChangedFor]` attributes on the `SelectedFile` setter will not fire. Without these calls, Move up would stay enabled after moving an item to position 0.

### Remove + selection clearing (AC #1, #2, #3)

```csharp
[RelayCommand(CanExecute = nameof(CanRemove))]
private void Remove()
{
    var item = SelectedFile;
    if (item is null) return;

    if (_validationCts.TryGetValue(item, out var cts))
        cts.Cancel();           // free the semaphore slot; stop a hung validation

    Files.Remove(item);          // ListView auto-deselects the removed item
    SelectedFile = null;         // explicit clear — makes the VM testable without a ListView
}

private bool CanRemove() => SelectedFile is not null;   // unchanged
```

Setting `SelectedFile = null` explicitly is important: the VM must be correct in unit tests (no `ListView` present), and it guarantees AC #1's "selection clears" independent of view behavior. Setting it also re-fires the `[NotifyCanExecuteChangedFor]` on the three commands (disabling them — AC #8 after removal). Flagged-file removal (AC #3) needs no special handling — `Files.Remove` is status-agnostic.

### Selection wiring (why Remove and Move behave differently)

There is **no ViewModel→View binding** on `ListView.SelectedItem`; selection only flows View→VM via `FileListView_SelectionChanged`. Consequences:
- **Move:** `Files.Move` keeps the same selected instance, the `ListView` keeps it highlighted, no `SelectionChanged` fires → `SelectedFile` stays valid. Correct by construction.
- **Remove:** removing the selected item from the bound collection makes the `ListView` drop it from selection and fire `SelectionChanged` (→ code-behind sets `SelectedFile = null`). We also set `SelectedFile = null` in the VM so the VM is correct regardless. Both paths converge on null; the redundant assignment is harmless.

Do not attempt to add a two-way `SelectedItem="{x:Bind ViewModel.SelectedFile, Mode=TwoWay}"` binding in this story — it's a known deferred refactor (story 1-1 review), out of scope here, and would risk re-entrancy with the existing `SelectionChanged` handler.

### In-flight validation cancellation on removal (addresses deferred items from 1-2)

`deferred-work.md` assigns two items to this story (both at the moment Remove becomes real):
- *"Semaphore slots held by removed files — in-flight validations not cancelled on item removal, blocks other validations."*
- *"Validation may write status to PdfFileItem after it has been removed from Files collection."*

Fix both by (a) tracking a per-item `CancellationTokenSource` so Remove can cancel it, and (b) guarding status writes so a late completion never mutates a removed item. Recommended shape (adapt the existing `ValidateFileAsync`, keeping its current timeout/`WhenAny` structure):

```csharp
private readonly Dictionary<PdfFileItem, CancellationTokenSource> _validationCts = [];

private async Task ValidateFileAsync(PdfFileItem item)
{
    var cts = new CancellationTokenSource();
    _validationCts[item] = cts;                 // on UI thread (sync prefix of the call)
    try
    {
        await _validationSemaphore.WaitAsync();
        try
        {
            var validationTask = Task.Run(() => _validationService.ValidateAsync(item.Path, cts.Token));
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5), CancellationToken.None);

            var completed = await Task.WhenAny(validationTask, timeoutTask);
            if (completed == timeoutTask)
            {
                await cts.CancelAsync();
                RunOnUI(() => { if (Files.Contains(item)) item.Status = ValidationStatus.ErrorTimeout; });
                return;
            }

            var (status, pageCount) = await validationTask;
            RunOnUI(() =>
            {
                if (!Files.Contains(item)) return;   // removed mid-flight → drop the write
                item.Status = status;
                item.PageCount = pageCount;
            });
        }
        catch (OperationCanceledException)
        {
            // timeout OR removal-triggered cancel; only mark timeout if still present
            RunOnUI(() => { if (Files.Contains(item)) item.Status = ValidationStatus.ErrorTimeout; });
        }
        catch
        {
            RunOnUI(() => { if (Files.Contains(item)) item.Status = ValidationStatus.ErrorCorrupt; });
        }
        finally
        {
            _validationSemaphore.Release();
        }
    }
    catch (ObjectDisposedException)
    {
        // Semaphore disposed during shutdown — nothing to do
    }
    finally
    {
        _validationCts.Remove(item);
        cts.Dispose();
    }
}
```

Notes:
- `_validationCts` is only read/written on the UI thread (the command handlers, and `ValidateFileAsync`'s sync prefix + post-`await` continuations, since there's no `ConfigureAwait(false)`), so a plain `Dictionary` is fine.
- Cancelling on Remove makes `validationTask` fault/cancel promptly, releasing the semaphore so other files don't stall behind a removed one.
- The `Files.Contains(item)` guards run on the UI thread inside `RunOnUI`, where `Files` is only mutated — race-free. They rely on reference equality (no `Equals` override), which is correct.
- Do not change the 5-second wall-clock threshold or the timeout→`ErrorTimeout` mapping — that's story 1-2 behavior and still required.

### Project Structure Notes

This story modifies one production file and one test file. No new files, no new folders, no new packages.

```
pdfjunior/
└── pdfjunior/
    └── ViewModels/
        └── MainViewModel.cs          [UPDATE] — Remove/MoveUp/MoveDown bodies, tightened CanMove* gates,
                                                  collection-changed command notifications, per-item
                                                  validation CTS + removed-item write guards
pdfjunior.Tests/
└── ViewModels/
    └── MainViewModelTests.cs         [UPDATE] — remove/reorder/CanExecute tests
```

`MainWindow.xaml` / `MainWindow.xaml.cs` are expected to need **no** changes — verify the existing command bindings drive button enablement, then leave them. If you find the toolbar buttons don't disable correctly, the fix is in the VM's `CanExecute`/notifications, not the XAML.

### Testing Notes

- Tests use **xUnit.v3 + NSubstitute**; `MainViewModelTests` mocks `IFilePickerService` and `IPdfValidationService` via `CreateViewModel()`. Follow the existing arrange/act/await pattern.
- `DispatcherQueue.GetForCurrentThread()` returns null in the test host, so `RunOnUI` executes inline — status updates apply synchronously after `await`. Reuse the existing `WaitForValidation()` (`Task.Delay(200)`) helper where validation completion timing matters; for pure remove/reorder tests on already-populated lists you can add items directly (`vm.Files.Add(new PdfFileItem(path))`) and set `vm.SelectedFile` without invoking the picker.
- Exercise commands through the generated command objects: `vm.RemoveCommand.Execute(null)`, `vm.MoveUpCommand.Execute(null)`, and assert gates with `vm.MoveUpCommand.CanExecute(null)` etc. (matches how `CanMerge` is exercised today).
- AC #4/#5 "remains selected, no reload" is verified by asserting `ReferenceEquals(before, vm.SelectedFile)` (or `Assert.Same`) after the move and that the index changed — there is no preview to assert against yet (preview is story 1.4), so the reference-stability assertion is the proxy for "no reload."
- The removed-item-write guard: add a hanging validation (`TaskCompletionSource` that never completes), add+select+Remove the item, then complete/cancel the TCS and assert `Files` is unchanged and merge state didn't flip. (Pattern mirrors `Validation_Timeout_HangingValidation_StatusErrorTimeout`.)
- **E2E / visual:** the app is MSIX-packaged and **cannot be launched from the CLI** (`dotnet run` won't deploy it); the previous two stories verified visuals via Visual Studio F5. Run `dotnet test` for unit coverage; mark the manual F5 checks done after visual confirmation. Build clean (0 warnings, 0 errors) is the bar — story 1-2 left the solution warning-free.

### Anti-Patterns to Avoid

- Do NOT implement reorder as remove-then-insert — use `ObservableCollection.Move`. Remove+insert changes the selected reference, drops `ListView` selection, and (in 1.4) forces a preview reload, breaking AC #4/#5.
- Do NOT reassign `SelectedFile` inside `MoveUp`/`MoveDown`. The selection must stay the same instance.
- Do NOT add `IsEnabled` bindings to the toolbar buttons — command `CanExecute` owns enablement; a second source of truth causes drift.
- Do NOT forget `NotifyCanExecuteChanged()` after a `Move` — the index changed without a `SelectedFile` change, so command states won't update on their own (Move up would stay enabled at position 0).
- Do NOT auto-select a neighbor after Remove — EXPERIENCE.md is explicit: selection clears, no neighbor is selected.
- Do NOT mutate `Files` or `PdfFileItem` from a background thread — keep mutations on the UI thread / inside `RunOnUI`.
- Do NOT add a value-equality `Equals`/`GetHashCode` to `PdfFileItem` — reference equality is required for correct `IndexOf`/`Remove`/`Contains` on the selected instance.
- Do NOT add new NuGet packages or introduce PDFsharp — out of scope until story 2-2.
- Do NOT add a `SelectedItem` TwoWay binding refactor — out of scope; reuse the existing `SelectionChanged` push.
- Do NOT use `async void`, `.Result`, `.Wait()`, or `ConfigureAwait(false)` in VM code (WinUI needs the sync context).

### Previous Story Intelligence

**From story 1-2 (done):**
- The validation pipeline (`SemaphoreSlim(3)`, 5 s wall-clock guard via `Task.WhenAny`/`Task.Delay`, `RunOnUI` marshaling) is in place — this story extends it with cancellation/cleanup, it does not rewrite it.
- `[ObservableProperty]` requires **partial property** syntax (`public partial PdfFileItem? SelectedFile { get; set; }`) for WinUI/WinRT source generators — already used; match it.
- `x:Bind` function bindings to static methods (`FormatStatus`, `StatusForeground`, `BoolToVisibility`) are the established pattern; not needed for this story but informative.
- Tests run green via `dotnet test`; the MSIX app can't be launched headless — visual verification is a manual VS F5 step (note it in completion notes rather than claiming automated E2E).
- 24 tests currently pass (19 `MainViewModelTests`, 5 `PdfValidationServiceTests`). Keep them green; add to them.

**Open deferred items NOT in scope for this story** (be aware, do not fix unless they block you): inline string literals for MC-1/MC-2 placeholders in XAML, `GetRequiredService` in `MainWindow` ctor, locale-dependent password-error heuristic, `Task.Delay(200)` timing in `WaitForValidation`, `SubclassProc` GC-rooting. Two items ARE in scope and addressed here: semaphore-held-by-removed-files and status-write-after-removal.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story 1.3] — Acceptance criteria (BDD)
- [Source: _bmad-output/planning-artifacts/architecture.md#Application State & Data Model (no database)] — `ObservableCollection<PdfFileItem>` bound to single-select `ListView`; merge consumes display order
- [Source: _bmad-output/planning-artifacts/architecture.md#App Structure, MVVM & Dependency Injection] — VM owns logic, service interfaces, `[RelayCommand]`
- [Source: _bmad-output/planning-artifacts/architecture.md#MVVM Patterns (the highest-divergence area)] — derived/command state re-raised on input change; no logic in code-behind
- [Source: _bmad-output/planning-artifacts/architecture.md#Async, Threading & Cancellation Patterns] — UI-thread-only collection mutation; cooperative cancellation
- [Source: _bmad-output/planning-artifacts/architecture.md#Requirements → Structure Mapping] — FR-3 → `RemoveCommand`; FR-4 → `MoveUp/MoveDownCommand`
- [Source: _bmad-output/planning-artifacts/ux-designs/ux-pdf-junior-2026-06-14/EXPERIENCE.md#Component Patterns] — Preview toolbar grouping/gap; Move "selection follows, preview unchanged"; Remove "selection clears, no neighbor auto-selected"
- [Source: _bmad-output/planning-artifacts/ux-designs/ux-pdf-junior-2026-06-14/EXPERIENCE.md#State Patterns] — File removed / File reordered state treatments; MC-1/MC-2 placeholders
- [Source: _bmad-output/implementation-artifacts/1-2-add-validate-pdf-files.md] — validation pipeline, `RunOnUI`, `CanMerge`, test patterns
- [Source: _bmad-output/implementation-artifacts/deferred-work.md] — two items explicitly assigned to story 1-3
- [Source: pdfjunior/ViewModels/MainViewModel.cs] — current command stubs, `OnFilesCollectionChanged`, `ValidateFileAsync`
- [Source: pdfjunior/MainWindow.xaml] — preview toolbar bindings, `ListView` selection mode
- [Source: pdfjunior/MainWindow.xaml.cs] — `FileListView_SelectionChanged` View→VM push
- [Source: pdfjunior/Models/PdfFileItem.cs] — reference-equality model (no `Equals` override)

## Dev Agent Record

### Agent Model Used

claude-opus-4-8 (Claude Code, bmad-dev-story workflow)

### Debug Log References

- Build/test run: `dotnet build pdfjunior.Tests/pdfjunior.Tests.csproj -p:Platform=x64 -r win-x64` → Build succeeded, 0 Warning(s), 0 Error(s).
- Test execution (xUnit v3 / Microsoft.Testing.Platform — run the built test exe directly, `dotnet test` VSTest path discovers no tests): `pdfjunior.Tests.exe` → Total: 39, Failed: 0, Skipped: 0.
- RED baseline before implementation: 13 new tests failed against the empty command stubs, confirming test validity.

### Completion Notes List

- **Remove (AC #1, #2, #3):** `Remove()` captures `SelectedFile`, cancels any in-flight validation via `_validationCts`, calls `Files.Remove(item)`, then sets `SelectedFile = null` (re-disables all three commands per AC #8). `CanRemove` gate unchanged. Flagged-file removal needs no special path (covered by a `[Theory]` over the three error statuses).
- **Move (AC #4–#8):** `MoveUp`/`MoveDown` use `ObservableCollection.Move` (single Move notification, instance preserved → ListView keeps selection, `SelectedFile` reference stable → no preview reload in 1.4). Tightened `CanMoveUp`/`CanMoveDown` with index/bounds checks. Explicit `MoveUpCommand/MoveDownCommand.NotifyCanExecuteChanged()` after each move because the index changes without a `SelectedFile` reference change.
- **Collection-change enablement (AC #6, #7):** `OnFilesCollectionChanged` now also notifies Move/Remove `CanExecuteChanged`, so adding/removing a file re-evaluates the selected item's position/bounds (e.g. selected item becomes last → Move down disables).
- **Deferred-debt from 1-2 (both resolved, marked in `deferred-work.md`):** added per-item `Dictionary<PdfFileItem, CancellationTokenSource> _validationCts`; `ValidateFileAsync` registers the CTS in its sync prefix and disposes/removes it in an outer `finally`; Remove cancels it (frees the semaphore slot); every `RunOnUI` status-write is guarded with `if (!Files.Contains(item)) return;` so a late completion never mutates a removed item.
- **XAML:** verified the three preview-toolbar buttons already bind the commands with no `IsEnabled` — no XAML change required (enablement is driven entirely by `CanExecute`).
- **Tests:** added 15 test cases (13 methods, one a 3-case `[Theory]`); also fixed two pre-existing `xUnit1051` warnings in the existing hanging-validation test to keep the build at 0 warnings. Total suite 24 → 39, all green.
- **Not done in this session:** manual Visual Studio F5 visual verification (MSIX can't launch from CLI) — see the E2E verification note under Tasks/Subtasks.

### File List

- `pdfjunior/ViewModels/MainViewModel.cs` (UPDATE) — `_validationCts` field; `Remove`/`MoveUp`/`MoveDown` bodies; tightened `CanMoveUp`/`CanMoveDown`; Move/Remove `CanExecuteChanged` notifications in `OnFilesCollectionChanged`; per-item CTS + removed-item write guards in `ValidateFileAsync`.
- `pdfjunior.Tests/ViewModels/MainViewModelTests.cs` (UPDATE) — 15 new remove/reorder/CanExecute/cancellation test cases; xUnit1051 cleanup in existing timeout test.
- `_bmad-output/implementation-artifacts/deferred-work.md` (UPDATE) — marked the two 1-3-assigned items resolved.

## Change Log

- 2026-06-16: Implemented Remove/MoveUp/MoveDown commands with tightened CanExecute gating and collection-change re-evaluation; resolved two deferred 1-2 items (in-flight validation cancellation on removal + removed-item write guards). Added 15 unit tests (suite 24 → 39, all green; 0 warnings/0 errors). Status → review. Manual VS F5 visual pass pending.
- 2026-06-18: **Reorder reworked from Move up / Move down toolbar buttons to native `ListView` drag-and-drop** (`CanReorderItems`/`AllowDrop`/`CanDragItems`), which mutates the bound `Files` collection directly so Merge still consumes display order (commit 859232b). Removed the `MoveUp`/`MoveDown` `[RelayCommand]`s, their `CanMoveUp`/`CanMoveDown` gates, the two `[NotifyCanExecuteChangedFor]` attributes on `SelectedFile`, and the two toolbar `Button`s in `MainWindow.xaml`; the `Remove` button and its gate stay unchanged. Tests: the `MoveUp`/`MoveDown`/`CanMove*` cases were replaced by `Reorder_PreservesSelectionAndOrder` (mirrors the `Files.Move` the ListView performs and asserts selection + order survive) and `Remove_NoSelection_Disabled`. ACs 4–6 above rewritten for drag-and-drop; the original button-based AC #4–#8 and the Tasks/Dev Notes are retained below as history. This **reverses the 2026-06-14 PRD FR-4 / UX decision** (Move up/down buttons; drag-reorder out of scope), at the user's request. Planning artifacts (PRD, epics, architecture, EXPERIENCE.md, DESIGN.md) reconciled via bmad-correct-course on 2026-06-18.
