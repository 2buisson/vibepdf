---
baseline_commit: ee64fc1
---

# Story 2.1: Merge Gating & Save Dialog

Status: review

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a user,
I want the Merge button to be enabled only when my file list is ready, and to choose where to save the output,
so that I never accidentally merge with invalid files and I control the output location and filename.

> **Scope orientation (read first).** This is the first story of Epic 2 and it is *narrow*. Most of the gating logic (`CanMerge`, `MergeDisabledReason`) **already exists** from Epic 1 scaffolding — your job is to (a) **correct** the tooltip-priority so it matches the ACs, (b) make the disabled-Merge tooltip **actually render** (a WinUI gotcha), and (c) implement the **Save dialog** (`PickSaveFileAsync` is currently a `null` stub and `MergeAsync` is a no-op stub). You do **NOT** build the merge itself, the progress bar, the UI-lock, banners, Open folder, or the close guard — those are stories 2.2 and 2.3. See **Scope Boundaries** below.

## Acceptance Criteria (BDD)

Merge-gating (FR-10) — the tooltip strings are canonical `UiStrings` constants (already defined); the parenthetical IDs map to the Microcopy Inventory:

1. **Given** the File list contains ≥1 valid file, 0 flagged files, and 0 files still checking **When** the user views the Action bar **Then** the Merge button is **enabled** (and shows no tooltip — `MergeDisabledReason` is `null`).

2. **Given** the File list contains ≥1 valid file **and** ≥1 flagged file (error-password / error-corrupt / error-timeout), with 0 still checking **When** the user hovers the disabled Merge button **Then** a tooltip shows **`UiStrings.MergeDisabledFlaggedFiles`** = "Remove files with errors before merging" (MC-11).

3. **Given** the File list contains **zero valid files** — either empty, or every item flagged — with 0 still checking **When** the user hovers the disabled Merge button **Then** a tooltip shows **`UiStrings.MergeDisabledNoFiles`** = "Add at least one PDF to merge" (MC-10). *(This includes the **all-flagged** case — the current code returns MC-11 here; correcting it is the crux of this AC. See Dev Notes "Tooltip priority correction".)*

4. **Given** any file in the list is still in **checking** status (regardless of what else is present) **When** the user hovers the disabled Merge button **Then** a tooltip shows **`UiStrings.MergeDisabledStillChecking`** = "Waiting for files to finish checking" (MC-12). *(Checking outranks flagged: a list with both a flagged and a checking file shows MC-12 "until all items resolve".)*

5. **Given** the Merge button is disabled for any reason **When** the user hovers (or keyboard-focuses) it **Then** the tooltip is **actually displayed**. *(WinUI does not show `ToolTipService.ToolTip` on a disabled control by default — this AC requires the wrapper workaround in Dev Notes; verify in the F5 pass.)*

Save dialog (FR-7):

6. **Given** Merge is enabled and the user clicks it **When** the native Save dialog opens **Then** the filename is pre-filled as **"merged.pdf"** (`UiStrings.DefaultMergeFileName`), the file type is **.pdf**, and the user can choose the destination folder and edit the filename.

7. **Given** the Save dialog is open **When** the user confirms a destination **Then** control passes to the merge-execution seam (the actual write is Story 2.2). *(For this story, "begins execution" means: the chosen `StorageFile` is obtained and handed to the seam without error. No file is written yet.)*

8. **Given** the Save dialog is open **When** the user cancels the dialog **Then** **no merge occurs, no error is shown, nothing is written**, and the app is unchanged from its pre-click state (silent no-op). `MergeAsync` returns without throwing.

## Tasks / Subtasks

- [x] **Task 1 — Correct the `MergeDisabledReason` tooltip priority** (AC: #2, #3, #4)
  - [x] In `pdfjunior/ViewModels/MainViewModel.cs`, reorder the `MergeDisabledReason` checks to this exact ladder (the current order yields the wrong string for the all-flagged and flagged+checking cases):
    ```csharp
    public string? MergeDisabledReason
    {
        get
        {
            if (Files.Count == 0)
                return UiStrings.MergeDisabledNoFiles;                 // MC-10 (empty)
            if (Files.Any(f => f.Status == ValidationStatus.Checking))
                return UiStrings.MergeDisabledStillChecking;           // MC-12 (checking outranks flagged)
            if (!Files.Any(f => f.Status == ValidationStatus.Valid))
                return UiStrings.MergeDisabledNoFiles;                 // MC-10 (all-flagged → "add a PDF")
            if (Files.Any(f => f.Status is ValidationStatus.ErrorPassword or ValidationStatus.ErrorCorrupt or ValidationStatus.ErrorTimeout))
                return UiStrings.MergeDisabledFlaggedFiles;            // MC-11 (has valid + flagged)
            return null;                                               // enabled
        }
    }
    ```
  - [x] **Do not touch `CanMerge`** — it is already correct (≥1 Valid, 0 Flagged, 0 Checking) and its tests pass. Only the *reason string ordering* changes.
  - [x] Leave `NotifyMergeStateChanged()` (which re-raises both `CanMerge` and `MergeDisabledReason`) exactly as-is — it already fires on every relevant collection/status change.

- [ ] **Task 2 — Make the disabled-Merge tooltip render** (AC: #5)
  - [x] In `pdfjunior/MainWindow.xaml`, the Merge button currently carries `ToolTipService.ToolTip` directly. A **disabled** WinUI button does not display its own tooltip. Move the tooltip to a **wrapper** that stays hit-test-visible while the button is disabled:
    ```xml
    <Border ToolTipService.ToolTip="{x:Bind ViewModel.MergeDisabledReason, Mode=OneWay}"
            Background="Transparent">
        <Button
            Content="Merge"
            Style="{StaticResource AccentButtonStyle}"
            Command="{x:Bind ViewModel.MergeCommand, Mode=OneTime}" />
    </Border>
    ```
  - [x] Keep the `Border` inside the existing right-aligned Action-bar `StackPanel` (it replaces the bare `<Button Content="Merge" …>`). The `Add PDF(s)` button is unchanged.
  - [x] When Merge is enabled, `MergeDisabledReason` is `null` → the wrapper shows no tooltip; the enabled button works normally. No tooltip should appear in the enabled state.
  - [ ] This is **view-only** wiring — no code-behind. Verify rendering in the Task 6 F5 pass (this behavior cannot be unit-tested).

- [x] **Task 3 — Add the default-filename string** (AC: #6)
  - [x] In `pdfjunior/Strings/UiStrings.cs`, add `public const string DefaultMergeFileName = "merged.pdf";` (the default Save-dialog filename — keep all user-facing copy here per the no-inline-literals rule). Place it near the merge strings with a short comment.

- [x] **Task 4 — Implement `PickSaveFileAsync` in `FilePickerService`** (AC: #6, #7, #8)
  - [x] In `pdfjunior/Services/FilePickerService.cs`, replace the `null`-returning stub with a real `FileSavePicker`, mirroring the existing `PickFilesAsync` HWND pattern:
    ```csharp
    public async Task<StorageFile?> PickSaveFileAsync(string suggestedName)
    {
        var picker = new FileSavePicker();
        InitializeWithWindow.Initialize(picker, Hwnd);
        picker.SuggestedFileName = suggestedName;                 // "merged.pdf"
        picker.DefaultFileExtension = ".pdf";
        picker.FileTypeChoices.Add("PDF document", new List<string> { ".pdf" });
        return await picker.PickSaveFileAsync();                  // null when the user cancels
    }
    ```
  - [x] `FileSavePicker.FileTypeChoices` **must** be non-empty or the picker throws — always add the `.pdf` choice. The `.pdf` extension literal is acceptable here (consistent with `PickFilesAsync`'s `FileTypeFilter.Add(".pdf")` — file extensions are not microcopy).
  - [x] `Hwnd` is already set on the singleton `FilePickerService` in `App.OnLaunched` — reuse it; do not re-acquire the window handle.

- [x] **Task 5 — Wire `MergeAsync` to open the Save dialog** (AC: #6, #7, #8)
  - [x] In `MainViewModel.cs`, replace the no-op `MergeAsync` body. Keep `[RelayCommand(CanExecute = nameof(CanMerge))]`:
    ```csharp
    [RelayCommand(CanExecute = nameof(CanMerge))]
    private async Task MergeAsync()
    {
        var destination = await _filePickerService.PickSaveFileAsync(UiStrings.DefaultMergeFileName);
        if (destination is null)
            return; // FR-7: cancelling the Save dialog is a silent no-op

        // Story 2.2 plugs the off-thread merge here: write the Valid files,
        // in display order, to `destination`. No file is written in this story.
    }
    ```
  - [x] Do **not** add UI-lock, progress, banners, or close-guard logic — those are stories 2.2/2.3 (see Scope Boundaries). The method must simply obtain the destination and stop.
  - [x] `MergeAsync` becomes `async Task` — no `async void`, no `.Result`/`.Wait()`, no `ConfigureAwait(false)` (WinUI sync context).

- [x] **Task 6 — Tests** (AC: #2, #3, #4, #6, #8)
  - [x] In `pdfjunior.Tests/ViewModels/MainViewModelTests.cs`, add gating-reason cases (the existing `MergeDisabledReason_*` tests stay green; add the missing buckets):
    - **AC #3 all-flagged → MC-10:** add two items both flagged (e.g. one `ErrorPassword`, one `ErrorCorrupt`), 0 valid, 0 checking → assert `vm.MergeDisabledReason == UiStrings.MergeDisabledNoFiles`. *(This is the case the current code gets wrong; the test must fail before Task 1 and pass after.)*
    - **AC #4 checking outranks flagged → MC-12:** one flagged item + one item left in `Checking` → assert `vm.MergeDisabledReason == UiStrings.MergeDisabledStillChecking`.
    - You can build these states directly without the picker: `vm.Files.Add(new PdfFileItem(path) { Status = ... })` (mirrors the existing `Remove_*`/`Reorder_*` tests). No `await` needed for pure status-set cases.
  - [x] Add Save-dialog command cases (mock `IFilePickerService`):
    - **AC #6 invocation:** set up a mergeable list (≥1 Valid, 0 flagged, 0 checking), `await vm.MergeCommand.ExecuteAsync(null)`, then assert `_pickerService.Received(1).PickSaveFileAsync(UiStrings.DefaultMergeFileName)`.
    - **AC #8 cancel = no-op:** `_pickerService.PickSaveFileAsync(Arg.Any<string>()).Returns((StorageFile?)null);` → `await vm.MergeCommand.ExecuteAsync(null)` completes without throwing and mutates nothing (`vm.Files` unchanged, `vm.CanMerge` still true). *(`StorageFile` is sealed and cannot be substituted, so the **confirm** path that returns a real `StorageFile` is **not** unit-testable — cover it in the F5 pass. Only the cancel/`null` path and the invocation are unit-tested.)*
    - Add `using Windows.Storage;` to the test file for the `StorageFile?` cast (the interface already exposes `StorageFile`, so the type resolves in the test project).
  - [x] Run the suite the project's way (see Testing Notes): build `pdfjunior.Tests` for `-p:Platform=x64 -r win-x64` and run the produced `pdfjunior.Tests.exe` directly — `dotnet test` does **not** discover these tests. Baseline is **43 tests, all green**; keep them green and add to them.

- [ ] **Task 7 — Verify end-to-end (manual VS F5; MSIX cannot launch from CLI)** (AC: #1–#8)
  - [x] Build clean (0 warnings / 0 errors) for `-p:Platform=x64 -r win-x64`. *(Verified: both `pdfjunior` and `pdfjunior.Tests` build with 0 warnings / 0 errors.)*
  - [ ] F5 visual checks: (a) **enabled** Merge with all-valid list, **no** tooltip; (b) hover disabled Merge with a flagged-but-has-valid list → "Remove files with errors before merging"; (c) all-flagged list → "Add at least one PDF to merge"; (d) a checking file present → "Waiting for files to finish checking"; (e) empty list → "Add at least one PDF to merge". (f) Click Merge on a valid list → Save dialog opens with "merged.pdf" pre-filled and `.pdf` type; **Cancel** → nothing happens, app idle; **Save** → dialog closes, no crash, no file written yet (Story 2.2 will write it). Mark these pending until Antoine's F5 pass confirms.

## Dev Notes

### Scope Boundaries (what is and isn't this story)

| In scope (2.1) | Out of scope — later story |
|---|---|
| Merge-gating **enabled/disabled** state (already correct via `CanMerge`) | Actual merge / PDFsharp / `PdfSharpMergeService` → **2.2** |
| Merge-disabled **tooltip** (correct string + actually rendering) | UI-lock during merge, progress bar (>2 s) → **2.2** |
| Save dialog: `PickSaveFileAsync` impl + `MergeAsync` opening it | Success banner, Open folder → **2.2** |
| Cancel = silent no-op | Error banners, "Not enough space", etc. → **2.3** |
| | Window-close guard (FR-12) → **2.3** |
| | Dismiss visible banner on Merge press (no banners exist yet) → **2.2** |

Do not pull any 2.2/2.3 work forward. `MergeAsync` ends at "destination obtained."

### Current state of the files you will touch (read before modifying)

- **`pdfjunior/ViewModels/MainViewModel.cs`** *(primary change — small):*
  - `CanMerge` (lines ~53–57) is **already correct** — do not change it. It is `[RelayCommand(CanExecute = nameof(CanMerge))]` on `MergeAsync` and re-raised by `NotifyMergeStateChanged()`.
  - `MergeDisabledReason` (lines ~59–73) has a **priority bug** (see next section) — reorder its checks per Task 1.
  - `MergeAsync` (lines ~261–265) is a no-op stub (`return Task.CompletedTask;`) — implement per Task 5.
  - The ctor is `(IFilePickerService filePickerService, IPdfValidationService validationService)` — **no new constructor parameter** (the save picker lives on the existing `IFilePickerService`). `_filePickerService` is already a field.
- **`pdfjunior/Services/IFilePickerService.cs`** *(no change needed):* already declares `Task<StorageFile?> PickSaveFileAsync(string suggestedName);` (and `using Windows.Storage;`). Implement the impl only.
- **`pdfjunior/Services/FilePickerService.cs`** *(implement the stub):* `Hwnd` property exists and is set by `App.OnLaunched`. `PickFilesAsync` shows the exact HWND-init pattern (`new FileOpenPicker(); InitializeWithWindow.Initialize(picker, Hwnd);`) — mirror it for `FileSavePicker`. The stub `PickSaveFileAsync` currently returns `Task.FromResult<StorageFile?>(null)`.
- **`pdfjunior/MainWindow.xaml`** *(Action bar, lines ~122–137):* the Merge `<Button>` already binds `Command` and `ToolTipService.ToolTip="{x:Bind ViewModel.MergeDisabledReason, Mode=OneWay}"`. Wrap it in a `Border` per Task 2. The `strings:` xmlns is already declared (`xmlns:strings="using:pdfjunior.Strings"`).
- **`pdfjunior/Strings/UiStrings.cs`** *(add one constant):* MC-10/11/12 already exist (`MergeDisabledNoFiles`, `MergeDisabledFlaggedFiles`, `MergeDisabledStillChecking`). Add `DefaultMergeFileName = "merged.pdf"`.
- **`pdfjunior/App.xaml.cs`** *(no change):* `FilePickerService` is registered as a singleton and exposed via `IFilePickerService`; `Hwnd` is set in `OnLaunched`. Nothing to add — no new service this story.
- **`pdfjunior.Tests/ViewModels/MainViewModelTests.cs`** *(extend):* `CreateViewModel()` already injects `IFilePickerService`/`IPdfValidationService` mocks. The factory signature is **unchanged**. Existing `MergeDisabledReason_*` and `CanMerge_*` tests are your template.

### Tooltip priority correction (the crux of AC #3/#4 — read carefully)

The current `MergeDisabledReason` checks **flagged before checking and before "no valid"**, which produces the wrong string in two cases:

| List state | Current (wrong) | Required (AC) |
|---|---|---|
| All items flagged, 0 valid | MC-11 "Remove files with errors" | **MC-10 "Add at least one PDF to merge"** |
| ≥1 flagged **and** ≥1 still checking | MC-11 | **MC-12 "Waiting for files to finish checking"** |

Why MC-10 (not MC-11) for all-flagged: the two strings imply *different remedies* — MC-11 means "remove the bad ones and you can merge" (only true if a valid file remains); MC-10 means "you have nothing valid yet, add one." When everything is flagged, removing them leaves nothing, so the actionable message is MC-10. This is exactly how the UX spec scopes them: MC-10 fires "when list is empty **or all items are flagged**"; MC-11 fires "when ≥1 flagged file is present (**distinct from MC-10**)" — i.e. flagged *alongside* a valid file. Epics Story 2.1 AC likewise buckets "all-flagged" under "zero valid files → no valid files" (MC-10).

Why MC-12 wins over MC-11 while checking: the UX State Pattern says a checking list keeps Merge "disabled (tooltip: MC-12) **until all items resolve**", and the epics AC says "any file still in checking status → MC-12" unconditionally. So checking is evaluated before flagged. The Task 1 ladder encodes this: empty → checking → no-valid → flagged → enabled.

The existing test `MergeDisabledReason_FlaggedFiles_ReturnsFlaggedMessage` uses **valid + flagged** (not all-flagged), so it stays MC-11 and must remain green after your reorder. Add the two missing buckets (all-flagged, flagged+checking) as new tests.

### The disabled-button-tooltip gotcha (AC #5 — easy to miss, breaks 3 ACs)

WinUI 3 does **not** display `ToolTipService.ToolTip` on a control whose `IsEnabled == false`. Because Merge is disabled exactly when `MergeDisabledReason` is non-null, the tooltip-on-the-button approach (current XAML) shows **nothing** in precisely the states ACs #2–#4 are about. The fix is to attach the tooltip to a **wrapper element that stays enabled** (`Border` with `Background="Transparent"`); a disabled child lets the pointer reach the wrapper, so the wrapper's tooltip shows on hover. When the button is enabled, `MergeDisabledReason` is `null`, so the wrapper shows no tooltip. This is a view-only change (Task 2) and is verified manually in F5 — it has no unit test. If the wrapper tooltip still fails to appear in F5 (framework quirk), note it in `deferred-work.md` and discuss; do **not** silently leave the ACs unmet.

### Save dialog specifics (FileSavePicker, Windows App SDK 2.2.0 / .NET 10)

- `FileSavePicker` is HWND-aware exactly like `FileOpenPicker`: construct, then `WinRT.Interop.InitializeWithWindow.Initialize(picker, Hwnd)`. Skipping the HWND init throws `COMException` (no window handle) in a desktop app.
- `FileTypeChoices` is **required** (must contain ≥1 entry) — `picker.FileTypeChoices.Add("PDF document", new List<string> { ".pdf" });`. An empty `FileTypeChoices` throws at `PickSaveFileAsync()`.
- `SuggestedFileName = "merged.pdf"` pre-fills the filename box. `DefaultFileExtension = ".pdf"` ensures the extension if the user omits it.
- `await picker.PickSaveFileAsync()` returns a `StorageFile` on confirm, **`null` on cancel**. The OS owns overwrite confirmation and filename validity (FR-7) — the app adds nothing.
- The returned `StorageFile` is what Story 2.2 will write to (the architecture's direct-write model wraps it in `CachedFileManager.DeferUpdates`/`CompleteUpdatesAsync`). That's why the interface returns `StorageFile?`, not a `string` path — **keep the `StorageFile` return type**; do not downgrade it to a path.

### Testing notes

- Stack: **xUnit.v3 3.2.2 + NSubstitute 5.3.0**; `CreateViewModel()` is the shared factory (signature unchanged this story). `DispatcherQueue.GetForCurrentThread()` is null in the test host, so `RunOnUI` runs inline and status writes apply synchronously. Reuse `WaitForValidation()` (`Task.Delay(200)`) only where the validation pipeline is exercised; the new gating-reason tests set `Status` directly and need no delay.
- **`StorageFile` cannot be mocked** (sealed WinRT type — NSubstitute can't proxy it) and cannot be constructed without a real file. Therefore: unit-test the **cancel** path (picker returns `null`) and the **invocation** (`Received(1).PickSaveFileAsync(UiStrings.DefaultMergeFileName)`); the **confirm** path is a manual F5 item and is fully exercised in Story 2.2. This mirrors how the open picker (`PickFilesAsync`) is unit-tested via its `IReadOnlyList<string>` return, while the real picker UI is F5-only.
- `FilePickerService.PickSaveFileAsync` itself (the real `FileSavePicker`) is **not** unit-tested — it needs a live HWND/picker UI, same as `PickFilesAsync`. Verify via F5.
- **Run tests the project's way** (memory `project_run_tests`): `dotnet test` fails to discover; build the test project for `-p:Platform=x64 -r win-x64` and run the produced `pdfjunior.Tests.exe` directly. Keep the **43-test** baseline green; you should be adding ~4 tests.

### Architecture compliance (guardrails)

- **MVVM:** all logic stays in `MainViewModel`; the tooltip-wrapper is pure XAML (no code-behind). Use CommunityToolkit generators (`[RelayCommand]`, `[ObservableProperty]`) — no hand-rolled INPC. Bind with compiled `{x:Bind}` (explicit mode), not `{Binding}`.
- **DI:** constructor injection only; no new service, no service-locator. The save picker rides on the already-injected `IFilePickerService`.
- **Services boundary:** `FilePickerService` encapsulates `FileSavePicker`; only the `StorageFile?` result crosses to the VM. Do not leak picker types into the VM beyond `StorageFile`.
- **Strings:** the default filename goes in `UiStrings` (`DefaultMergeFileName`); no inline literals in the VM/View. The `.pdf` extension literal in the service is acceptable (matches the open picker; an extension is not microcopy). Tooltip strings are the existing MC-10/11/12 constants.
- **Theming:** no color/brush/radius changes. The wrapper `Border` is `Background="Transparent"` (not a themed surface) purely for hit-testing — it adds no visual. Merge keeps `AccentButtonStyle`; accent stays only on Merge + list selection.
- **Async/threading:** `MergeAsync` is `async Task`; never block the UI thread, no `async void`, no `.Result`/`.Wait()`/`ConfigureAwait(false)`.
- **Nullable:** honor `<Nullable>enable</Nullable>` — null-check the `StorageFile?` result rather than `!`.
- **Privacy/no-new-deps:** no NuGet packages this story (PDFsharp is still Story 2.2). `FileSavePicker` is in-box. No network/telemetry; any logging is `System.Diagnostics.Debug` only.

### Anti-patterns to avoid

- Do **not** change `CanMerge` — it's correct; only reorder `MergeDisabledReason`.
- Do **not** leave the tooltip on the disabled `Button` (it won't render) — use the enabled wrapper (AC #5).
- Do **not** return MC-11 for the all-flagged case, and do **not** let flagged outrank checking — follow the Task 1 ladder exactly.
- Do **not** change `PickSaveFileAsync`'s return type to a path string — Story 2.2 needs the `StorageFile` for the `CachedFileManager` write pattern.
- Do **not** implement the merge, progress, UI-lock, banners, Open folder, or close guard here (2.2/2.3).
- Do **not** add a UI-lock or disable controls on Merge press — the app stays interactive while the Save dialog is open (UX "Merge pressed: the app remains interactive… no lock").
- Do **not** construct or mock `StorageFile` in tests; do **not** assert on the confirm path in a unit test.
- Do **not** forget `FileTypeChoices.Add(...)` — an empty choices collection throws.
- Do **not** add an inline "merged.pdf" literal in the VM — reference `UiStrings.DefaultMergeFileName`.

### Previous story intelligence

**From stories 1.2–1.4 (done) and the reorder pivot:**
- `MainViewModel` already wires `Files.CollectionChanged` + per-item `PropertyChanged` (`OnFilePropertyChanged`) → `NotifyMergeStateChanged()`, which re-raises `CanMerge` **and** `MergeDisabledReason` and calls `MergeCommand.NotifyCanExecuteChanged()`. So your reordered `MergeDisabledReason` is automatically re-evaluated whenever a file's `Status` changes or the collection mutates — no extra plumbing needed.
- The HWND-aware picker pattern is established in `PickFilesAsync` (`InitializeWithWindow.Initialize(picker, Hwnd)`); `Hwnd` is assigned to the singleton `FilePickerService` in `App.OnLaunched`. Reuse verbatim for `FileSavePicker`.
- `[ObservableProperty]`/`[RelayCommand]` use **partial** members for the WinUI/WinRT generators. `MergeCommand`'s `CanExecute = nameof(CanMerge)` is already generated.
- `PdfFileItem` uses **reference equality** (no `Equals`/`GetHashCode` override) — fine here; gating only reads `Status`.
- Tests run via the **direct-exe** path (43 green); MSIX visuals (the Save dialog UI, tooltip rendering) are **manual VS F5** — state them pending, don't fake automated E2E. This matches 1.1–1.4.

**Still-open deferred items (be aware; not this story's job unless they block you):** orphaned `Task.Run` after validation timeout; `GetRequiredService` in `MainWindow` ctor; locale-dependent password heuristic; bare-catch corrupt classification; `Task.Delay(200)` test timing; `SubclassProc` GC-rooting; `MergeOutcome.Failure` carries only a string (no exception — matters for 2.3's `IErrorMapper`); `IPdfMergeService` (raw `Stream`) vs `IOutputWriter` (`StorageFile`) composability (resolve when building 2.2). See `deferred-work.md`.

### Git intelligence

Recent cadence is one focused `feat:` commit per story (`dd65d1e` 1.2, `07e2d78` 1.3, `859232b` reorder pivot, `03155fb` 1.4, `0bcd2ce` webview pivot) plus `docs:` reconciliations (`3ebe699`, `ee64fc1`). Baseline for this story is **`ee64fc1`**. Follow the same one-clean-`feat:`-commit convention (e.g. `feat: implement story 2-1 merge gating and save dialog`). Note that `IFilePickerService.PickSaveFileAsync` and the `CanMerge`/`MergeDisabledReason`/Merge-button-tooltip scaffolding already landed in earlier commits — this story completes the stubs and corrects the priority, it does not introduce the surface from scratch.

### Project Structure Notes

```
pdfjunior/
└── pdfjunior/
    ├── Services/
    │   └── FilePickerService.cs       [UPDATE] — implement PickSaveFileAsync (FileSavePicker, HWND, .pdf, suggested name)
    ├── ViewModels/
    │   └── MainViewModel.cs           [UPDATE] — reorder MergeDisabledReason ladder; implement MergeAsync (open save dialog, cancel = no-op)
    ├── Strings/
    │   └── UiStrings.cs               [UPDATE] — add DefaultMergeFileName = "merged.pdf"
    └── MainWindow.xaml                [UPDATE] — wrap Merge button in a Border carrying the disabled tooltip
pdfjunior.Tests/
└── ViewModels/
    └── MainViewModelTests.cs          [UPDATE] — all-flagged→MC-10, flagged+checking→MC-12, MergeAsync invokes save picker, cancel = no-op
```

No new files, no new NuGet packages, no DI changes, no constructor-signature changes. `IFilePickerService.cs` and `App.xaml.cs` are unchanged.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story 2.1: Merge Gating & Save Dialog] — story statement + AC seed (FR-7, FR-10)
- [Source: _bmad-output/planning-artifacts/epics.md#Requirements Inventory] — FR-7 (Save dialog, "merged.pdf"), FR-10 (gating + tooltip distinguishing cause)
- [Source: _bmad-output/planning-artifacts/architecture.md#App Structure, MVVM & Dependency Injection] — `IFilePickerService` (FileSavePicker wrapper, HWND-aware), VM owns logic, DI singletons
- [Source: _bmad-output/planning-artifacts/architecture.md#Application State & Data Model (no database)] — "Merge-enabled is a derived property: ≥1 Valid, 0 Flagged, 0 Checking (FR-10)"
- [Source: _bmad-output/planning-artifacts/architecture.md#Output / File-I/O Safety] — direct write to the destination `StorageFile` from the FileSavePicker (why the picker returns `StorageFile`, used by Story 2.2)
- [Source: _bmad-output/planning-artifacts/architecture.md#Requirements → Structure Mapping] — FR-7 → `FilePickerService` (FileSavePicker); FR-10 → `MainViewModel.CanMerge` + cause-specific tooltip
- [Source: _bmad-output/planning-artifacts/architecture.md#MVVM Patterns / Async, Threading & Cancellation / Dependency Injection / UI String Patterns] — `x:Bind` explicit, derived state re-raised, constructor DI, no inline literals
- [Source: _bmad-output/planning-artifacts/ux-designs/ux-pdf-junior-2026-06-14/EXPERIENCE.md#Microcopy Inventory] — MC-10/MC-11/MC-12 canonical strings + the "MC-10 = empty or all-flagged" / "MC-11 = ≥1 flagged, distinct from MC-10" scoping; MC-6/etc. for context
- [Source: _bmad-output/planning-artifacts/ux-designs/ux-pdf-junior-2026-06-14/EXPERIENCE.md#Component Patterns] — Merge button: enabled only when ≥1 valid/0 flagged/0 checking; disabled tooltip = MC-10/11/12 (three distinct messages)
- [Source: _bmad-output/planning-artifacts/ux-designs/ux-pdf-junior-2026-06-14/EXPERIENCE.md#State Patterns] — "Files added, checking → MC-12 until all items resolve"; "Merge pressed → Save dialog (default 'merged.pdf', .pdf); app remains interactive, no lock"; "Save dialog cancelled → silent no-op"
- [Source: _bmad-output/planning-artifacts/ux-designs/ux-pdf-junior-2026-06-14/DESIGN.md#Components] — Merge = `AccentButtonStyle`; accent only on Merge + selection; no custom colors/radii
- [Source: pdfjunior/ViewModels/MainViewModel.cs] — existing `CanMerge` (keep), `MergeDisabledReason` (reorder), `MergeAsync` stub (implement), `_filePickerService` field, `NotifyMergeStateChanged`
- [Source: pdfjunior/Services/IFilePickerService.cs] — `PickSaveFileAsync(string)` signature (already present)
- [Source: pdfjunior/Services/FilePickerService.cs] — `Hwnd` + `PickFilesAsync` HWND pattern to mirror; `PickSaveFileAsync` stub to implement
- [Source: pdfjunior/MainWindow.xaml] — Action-bar Merge button + current (non-rendering) disabled tooltip to wrap
- [Source: pdfjunior/Strings/UiStrings.cs] — MC-10/11/12 already defined; add `DefaultMergeFileName`
- [Source: pdfjunior/App.xaml.cs] — `FilePickerService` singleton; `Hwnd` set in `OnLaunched` (no change)
- [Source: pdfjunior.Tests/ViewModels/MainViewModelTests.cs] — `CreateViewModel()` factory; existing `CanMerge_*` / `MergeDisabledReason_*` tests to extend
- [Source: _bmad-output/implementation-artifacts/deferred-work.md] — open debt incl. `MergeOutcome`/`IErrorMapper` and `IPdfMergeService`/`IOutputWriter` composability (Story 2.2 concerns)
- [Source: memory project_run_tests] — build x64/win-x64 and run `pdfjunior.Tests.exe` directly; `dotnet test` does not discover

## Dev Agent Record

### Agent Model Used

claude-opus-4-8 (Claude Code Dev Story workflow)

### Debug Log References

- Baseline before changes: `pdfjunior.Tests.exe` → **43 tests, 0 failed**.
- Red phase (new tests added, production unchanged): **47 total, 3 failed** — `MergeDisabledReason_AllFlagged_ReturnsNoFilesMessage`, `MergeDisabledReason_FlaggedAndChecking_ReturnsCheckingMessage`, `Merge_MergeableList_OpensSaveDialogWithDefaultName` (the cancel test passed against the no-op stub, as expected).
- Green phase (Tasks 1/4/5 applied): **47 total, 0 failed**.
- Clean build (`-p:Platform=x64 -r win-x64`): `pdfjunior` and `pdfjunior.Tests` both **0 warnings / 0 errors**.

### Completion Notes List

- **Task 1 — `MergeDisabledReason` priority corrected.** Reordered the ladder to empty → checking → no-valid → flagged → enabled. All-flagged now returns MC-10 (`MergeDisabledNoFiles`) and flagged+checking now returns MC-12 (`MergeDisabledStillChecking`). `CanMerge` and `NotifyMergeStateChanged()` were left untouched, as instructed.
- **Task 2 — Tooltip wrapper (code complete; render verification pending F5).** The Merge `<Button>` is now wrapped in a hit-test-visible `<Border Background="Transparent">` that carries the `ToolTipService.ToolTip` binding, so the tooltip can render while the button is disabled. View-only change (no code-behind). The actual on-screen rendering (AC #5) cannot be unit-tested and is left pending the manual F5 pass (Task 7).
- **Task 3 — `UiStrings.DefaultMergeFileName = "merged.pdf"`** added near the merge strings.
- **Task 4 — `FilePickerService.PickSaveFileAsync`** implemented with a real `FileSavePicker`, mirroring the `PickFilesAsync` HWND-init pattern; `.pdf` file type added (required, non-empty `FileTypeChoices`), suggested name + default extension set. Returns `null` on cancel.
- **Task 5 — `MergeAsync`** is now `async Task`: it opens the Save dialog via the existing `_filePickerService`; cancel (`null`) is a silent no-op; on confirm it stops at "destination obtained" (Story 2.2 plugs in the write). No UI-lock/progress/banners/close-guard, per scope.
- **Task 6 — Tests.** Added 4 tests: all-flagged→MC-10, flagged+checking→MC-12, Merge invokes `PickSaveFileAsync("merged.pdf")`, and cancel = silent no-op. Suite is now **47 tests, all green** via the direct-exe path. The confirm path (real `StorageFile`) is not unit-testable (sealed WinRT type) and is an F5 item.
- **PENDING — manual F5 (Task 7 + AC #5 rendering).** The MSIX app cannot launch from the CLI, so the visual/E2E checks (tooltip actually rendering on the disabled button; Save dialog opening pre-filled with "merged.pdf"; cancel/confirm behavior) require Antoine's Visual Studio F5 pass. Left unchecked, per the story's own instruction ("Mark these pending until Antoine's F5 pass confirms").
- No new NuGet packages, no DI changes, no constructor-signature changes, no code-behind. `IFilePickerService.cs` and `App.xaml.cs` were not modified.

### File List

- `pdfjunior/ViewModels/MainViewModel.cs` — reordered `MergeDisabledReason` ladder; implemented `MergeAsync` (open Save dialog, cancel = no-op).
- `pdfjunior/Services/FilePickerService.cs` — implemented `PickSaveFileAsync` (FileSavePicker, HWND, `.pdf`, suggested name).
- `pdfjunior/Strings/UiStrings.cs` — added `DefaultMergeFileName = "merged.pdf"`.
- `pdfjunior/MainWindow.xaml` — wrapped the Merge button in a `Border` carrying the disabled-Merge tooltip.
- `pdfjunior.Tests/ViewModels/MainViewModelTests.cs` — added 4 tests (all-flagged→MC-10, flagged+checking→MC-12, Merge invokes save picker, cancel = no-op) + `using Windows.Storage;`.

### Change Log

- 2026-06-20 — Implemented Story 2.1 (Merge Gating & Save Dialog): corrected `MergeDisabledReason` tooltip priority, made the disabled-Merge tooltip render via a `Border` wrapper, added `DefaultMergeFileName`, implemented `PickSaveFileAsync`, and wired `MergeAsync` to the Save dialog (cancel = silent no-op). Added 4 unit tests (43 → 47, all green). Manual F5 verification (Task 7) pending.
