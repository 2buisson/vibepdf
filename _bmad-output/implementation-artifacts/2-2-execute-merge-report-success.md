---
baseline_commit: ee64fc1
depends_on: 2-1-merge-gating-save-dialog
---

# Story 2.2: Execute Merge & Report Success

Status: review

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a user,
I want my files merged into a single PDF with progress feedback and a clear success message,
so that I know the merge is working and can quickly find my output file.

> **Scope orientation (read first).** Story 2.1 builds the *front half* of the merge flow (gating + Save dialog) and leaves a seam: after the user confirms a destination, `MergeAsync` obtains a `StorageFile? destination` and **stops** with a comment `// Story 2.2 plugs the off-thread merge here`. **This story fills that seam.** You will: (a) add **PDFsharp** and create the three missing service implementations (`PdfSharpMergeService`, `OutputWriter`, `FolderLauncher` — only the *interfaces* exist today); (b) wire them through DI and into `MainViewModel` (the ctor gains 3 params → the test factory changes); (c) extend `MergeAsync` to run the merge **off the UI thread**, lock the UI, show a **>2 s progress bar**, write the output, and raise a **success banner** with **Open folder**; (d) add the `InfoBar` + `ProgressBar` UI. You do **NOT** build the specific error taxonomy (`IErrorMapper`, "Not enough space", "Access denied", …) or the window-close guard — those are Story 2.3. On failure this story shows only the **generic** banner (MC-18) so the app stays whole; 2.3 refines it. See **Scope Boundaries**.

> **⚠️ Dependency: Story 2.1 must be implemented first.** This story assumes 2.1's changes are in place: `MergeDisabledReason` ladder corrected, the Merge button wrapped in a tooltip `Border`, `UiStrings.DefaultMergeFileName = "merged.pdf"`, `FilePickerService.PickSaveFileAsync` implemented, and `MergeAsync` opening the Save dialog and returning the chosen `StorageFile?`. If 2.1 is not yet done, do it first — do not re-stub the Save dialog here. The frontmatter `baseline_commit` is the pre-2.1 baseline; rebase your mental model onto the post-2.1 tree.

## Acceptance Criteria (BDD)

Strings are canonical `UiStrings` constants — **all of MC-13…MC-19 already exist** in `UiStrings.cs`; do **not** add or alter copy this story.

**Package & service (FR-8, NFR-6):**

1. **Given** the merge engine is being built **When** the developer adds the **PDFsharp 6.2.4** NuGet package and creates `PdfSharpMergeService : IPdfMergeService` **Then** the app project builds clean (0 warnings / 0 errors) for `-p:Platform=x64 -r win-x64`, and the packaged install footprint stays under 100 MB (NFR-6 — PDFsharp is pure-managed, so this holds; spot-check the publish output size).

**Execute merge (FR-8):**

2. **Given** the user has confirmed a destination in the Save dialog **When** the merge begins **Then** all **Valid** files in the File list are combined **in display order** into a single PDF and written to the chosen `StorageFile` **And** the merge runs **off the UI thread** — the app stays responsive throughout.

3. **Given** exactly one Valid file is in the File list **When** the user merges **Then** a valid single-file merge is produced (a copy of that file's pages at the chosen destination).

**UI lock during merge (FR-8 / from EXPERIENCE State Patterns):**

4. **Given** a merge is in progress **When** the File list and bars are viewed **Then** the File list is **read-only** (drag-reorder disabled — `CanReorderItems`/`CanDragItems`/`AllowDrop` all `False`), and **Add PDF(s)**, **Merge**, and **Remove** are all **disabled** **And** the Preview pane remains scrollable.

5. **Given** the Save dialog is still open (destination not yet confirmed) **When** the app is viewed **Then** the UI is **not** locked — the lock engages only **after** the destination is confirmed and merge execution begins. *(2.1 guarantee: "the app remains interactive while the dialog is open — no lock.")*

**Progress (FR-8, NFR-1):**

6. **Given** a merge has been running **less than 2 seconds** **When** the user views the app **Then** **no** progress indicator appears (no flash, no spinner, no "Merging…" text).

7. **Given** a merge has been running **2 seconds or more** **When** the user views the app **Then** a thin **determinate** `ProgressBar` (progress **by file count**) is visible above the Action bar, advancing as each file is imported.

**Success (FR-9):**

8. **Given** the merge completes successfully **When** the success banner appears **Then** it shows **`UiStrings.MergeSuccess`** formatted with the output filename — "Merged successfully — {filename}" (MC-13), as an `InfoBar` `Severity="Success"`, **auto-dismisses after ~8 s**, and is **manually closable** before then.

9. **Given** the success banner is visible **When** the user clicks **Open folder** (`UiStrings.MergeSuccessOpenFolder`, MC-14) **Then** File Explorer opens to the folder containing the output file **And** if that folder no longer exists, the inline message **`UiStrings.FolderNotFound`** = "Folder not found" (MC-19) is shown instead of launching Explorer.

10. **Given** a merge has completed successfully **When** the user views the File list **Then** it is **preserved** — no files removed — so a second merge can be produced without re-adding files, and the UI is unlocked.

11. **Given** a previous success **or error** banner is visible **When** a new merge is started (Merge pressed) **Then** the previous banner is **immediately cleared** before the Save dialog opens.

## Tasks / Subtasks

- [x] **Task 1 — Add PDFsharp and implement `PdfSharpMergeService`** (AC: #1, #2, #3)
  - [x] In `pdfjunior/pdfjunior.csproj`, add `<PackageReference Include="PDFsharp" Version="6.2.4" />` (the pure-managed core package — **not** `PDFsharp-gdi`/`-wpf`; merge does no rendering, so the GDI variants are unneeded and would bloat the footprint).
  - [x] Create `pdfjunior/Services/PdfSharpMergeService.cs` implementing the **existing** `IPdfMergeService.MergeAsync(IReadOnlyList<string> paths, Stream output, IProgress<double>? progress, CancellationToken ct)`:
    ```csharp
    using PdfSharp.Pdf;
    using PdfSharp.Pdf.IO;
    using pdfjunior.Models;

    namespace pdfjunior.Services;

    public class PdfSharpMergeService : IPdfMergeService
    {
        public Task<MergeOutcome> MergeAsync(
            IReadOnlyList<string> paths, Stream output, IProgress<double>? progress, CancellationToken ct)
            => Task.Run<MergeOutcome>(() =>
            {
                try
                {
                    using var merged = new PdfDocument();
                    for (var i = 0; i < paths.Count; i++)
                    {
                        ct.ThrowIfCancellationRequested();
                        using var input = PdfReader.Open(paths[i], PdfDocumentOpenMode.Import);
                        for (var p = 0; p < input.PageCount; p++)
                            merged.AddPage(input.Pages[p]);
                        progress?.Report(100.0 * (i + 1) / paths.Count); // determinate by file count
                    }
                    merged.Save(output, closeStream: false); // keep the MemoryStream open for the VM to rewind
                    return new MergeOutcome.Success(string.Empty);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex) { return new MergeOutcome.Failure(ex.Message); }
            }, ct);
    }
    ```
  - [x] **Off-thread:** the `Task.Run` makes the synchronous PDFsharp work non-blocking (AC #2). Do **not** call PDFsharp on the UI thread.
  - [x] **Cancellation:** check `ct` **between** file imports (the close-guard in 2.3 cancels it). For this story the token comes from a VM-owned CTS that is never triggered by a user action yet — plumb it anyway.
  - [x] `MergeOutcome.Success.Path` is **unused** here (the service writes to a `Stream`, not a path — the VM owns the destination). Pass `string.Empty`. **Do not** change the `MergeOutcome` shape; 2.3 will revisit it to carry the exception for `IErrorMapper`.

- [x] **Task 2 — Implement `OutputWriter`** (AC: #2)
  - [x] Create `pdfjunior/Services/OutputWriter.cs` implementing the **existing** `IOutputWriter.WriteAsync(Stream source, StorageFile destination)` using the architecture's picker-write pattern:
    ```csharp
    using Windows.Storage;
    using Windows.Storage.Provider;

    namespace pdfjunior.Services;

    public class OutputWriter : IOutputWriter
    {
        public async Task WriteAsync(Stream source, StorageFile destination)
        {
            CachedFileManager.DeferUpdates(destination);
            using (var outStream = await destination.OpenStreamForWriteAsync())
            {
                outStream.SetLength(0); // truncate when overwriting a larger existing file
                await source.CopyToAsync(outStream);
            }
            await CachedFileManager.CompleteUpdatesAsync(destination);
        }
    }
    ```
  - [x] `OpenStreamForWriteAsync()` is the `System.IO` extension on `StorageFile` (in-box). `SetLength(0)` guards the intentional-overwrite case (Flow 3) so no trailing bytes of a larger previous file remain.
  - [x] Keep `WriteAsync` free of merge logic — it only copies a finished stream to the destination. **No temp file, no rollback** (architecture "Output / File-I/O Safety" — partial output is permitted).

- [x] **Task 3 — Implement `FolderLauncher`** (AC: #9)
  - [x] Create `pdfjunior/Services/FolderLauncher.cs` implementing the **existing** `IFolderLauncher.LaunchFolderAsync(string folderPath)`:
    ```csharp
    using Windows.Storage;
    using Windows.System;

    namespace pdfjunior.Services;

    public class FolderLauncher : IFolderLauncher
    {
        public async Task<bool> LaunchFolderAsync(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
                return false; // VM shows MC-19 "Folder not found"
            var folder = await StorageFolder.GetFolderFromPathAsync(folderPath);
            await Launcher.LaunchFolderAsync(folder);
            return true;
        }
    }
    ```
  - [x] Return `false` (not throw) when the folder is gone, so the VM can show **MC-19** inline (AC #9). The `Directory.Exists` pre-check avoids the WinRT exception path.

- [x] **Task 4 — Register the three services in DI** (AC: #2, #9)
  - [x] In `pdfjunior/App.xaml.cs` `ConfigureServices()`, add as singletons (matching the existing pattern):
    ```csharp
    services.AddSingleton<IPdfMergeService, PdfSharpMergeService>();
    services.AddSingleton<IOutputWriter, OutputWriter>();
    services.AddSingleton<IFolderLauncher, FolderLauncher>();
    ```
  - [x] Leave the existing `FilePickerService`/`IFilePickerService`/`IPdfValidationService`/`MainViewModel` registrations unchanged.

- [x] **Task 5 — Extend `MainViewModel`: inject services, add merge state** (AC: #2, #4, #6, #7, #8, #10, #11)
  - [x] **Constructor:** add the three new dependencies (constructor injection only):
    ```csharp
    public MainViewModel(
        IFilePickerService filePickerService,
        IPdfValidationService validationService,
        IPdfMergeService mergeService,
        IOutputWriter outputWriter,
        IFolderLauncher folderLauncher)
    ```
    Store each in a `private readonly` field. *(This changes the ctor signature → update the test factory in Task 8.)*
  - [x] **UI-lock state:** add `[ObservableProperty] public partial bool IsMerging { get; set; }` and a derived `public bool CanReorderFiles => !IsMerging;`. In an `OnIsMergingChanged(bool)` partial method, re-raise everything the lock affects:
    ```csharp
    partial void OnIsMergingChanged(bool value)
    {
        OnPropertyChanged(nameof(CanReorderFiles));
        NotifyMergeStateChanged();                 // re-raises CanMerge + MergeDisabledReason + MergeCommand
        AddFilesCommand.NotifyCanExecuteChanged();
        RemoveCommand.NotifyCanExecuteChanged();
    }
    ```
  - [x] **Gate the commands on `IsMerging`:**
    - `CanMerge` → append `&& !IsMerging` to the existing expression.
    - `AddFilesAsync` → give it `[RelayCommand(CanExecute = nameof(CanAddFiles))]` with `private bool CanAddFiles() => !IsMerging;` (it is currently a bare `[RelayCommand]`, always enabled).
    - `CanRemove()` → change to `SelectedFile is not null && !IsMerging`.
  - [x] **Progress state:** add `[ObservableProperty] public partial double MergeProgress { get; set; }` (0–100) and `[ObservableProperty] public partial bool IsProgressVisible { get; set; }`.
  - [x] **Banner state** (both `InfoBar`s bind `IsOpen` two-way, so these setters must stay public — the `[ObservableProperty]` default):
    - `[ObservableProperty] public partial bool IsSuccessBannerOpen { get; set; }`
    - `[ObservableProperty] public partial string? SuccessBannerText { get; set; }`
    - `[ObservableProperty] public partial bool IsErrorBannerOpen { get; set; }`
    - `[ObservableProperty] public partial string? ErrorBannerText { get; set; }`
  - [x] Keep a `private string? _lastOutputFolder;` field (captured on success, used by Open folder) and a `private CancellationTokenSource? _mergeCts;` field (created per merge; the close-guard in 2.3 will cancel it — this story only creates/disposes it).

- [x] **Task 6 — Extend `MergeAsync` to run the merge end-to-end** (AC: #2, #3, #4, #6, #7, #8, #10, #11)
  - [x] Replace 2.1's seam (`// Story 2.2 plugs the off-thread merge here`) so the full method reads:
    ```csharp
    [RelayCommand(CanExecute = nameof(CanMerge))]
    private async Task MergeAsync()
    {
        // AC #11 — Merge press clears any visible banner before the dialog opens.
        IsSuccessBannerOpen = false;
        IsErrorBannerOpen = false;

        var destination = await _filePickerService.PickSaveFileAsync(UiStrings.DefaultMergeFileName);
        if (destination is null)
            return; // FR-7: cancelling the Save dialog is a silent no-op (no lock engaged)

        var paths = Files
            .Where(f => f.Status == ValidationStatus.Valid)
            .Select(f => f.Path)
            .ToList();

        _mergeCts = new CancellationTokenSource();
        IsMerging = true;          // AC #4 — lock engages only AFTER confirm (AC #5)
        MergeProgress = 0;
        var showProgress = StartProgressDelay(); // AC #6/#7 — reveal the bar only after 2 s

        try
        {
            var progress = new Progress<double>(p => MergeProgress = p); // captures UI sync context
            using var buffer = new MemoryStream();
            var outcome = await _mergeService.MergeAsync(paths, buffer, progress, _mergeCts.Token);

            if (outcome is MergeOutcome.Failure)
            {
                ShowError(UiStrings.MergeErrorGeneric); // 2.2 = generic only; 2.3 refines
                return;
            }

            buffer.Position = 0;
            await _outputWriter.WriteAsync(buffer, destination);

            _lastOutputFolder = System.IO.Path.GetDirectoryName(destination.Path);
            ShowSuccess(string.Format(UiStrings.MergeSuccess, destination.Name)); // AC #8
        }
        catch (OperationCanceledException)
        {
            // Cooperative cancel (close-guard, 2.3). No banner this story.
        }
        catch
        {
            ShowError(UiStrings.MergeErrorGeneric); // 2.2 generic; 2.3 maps specific reasons
        }
        finally
        {
            showProgress.Cancel();
            IsProgressVisible = false;
            IsMerging = false;     // AC #10 — UI unlocks; Files untouched (preserved)
            _mergeCts.Dispose();
            _mergeCts = null;
        }
    }
    ```
  - [x] **`StartProgressDelay()` helper** — reveal the bar only after 2 s, cancel if the merge finishes first:
    ```csharp
    private CancellationTokenSource StartProgressDelay()
    {
        var cts = new CancellationTokenSource();
        _ = Task.Delay(TimeSpan.FromSeconds(2), cts.Token)
                .ContinueWith(
                    _ => RunOnUI(() => IsProgressVisible = true),
                    cts.Token,
                    TaskContinuationOptions.OnlyOnRanToCompletion,
                    TaskScheduler.Default);
        return cts;
    }
    ```
    Reuse the existing `RunOnUI` helper (it already no-ops to inline when `_dispatcherQueue` is null, so this is test-host safe).
  - [x] **Banner helpers** (enforce "at most one banner visible"):
    ```csharp
    private void ShowSuccess(string text)
    {
        IsErrorBannerOpen = false;
        SuccessBannerText = text;
        IsSuccessBannerOpen = true;
        StartSuccessAutoDismiss(); // ~8 s one-shot
    }
    private void ShowError(string text)
    {
        IsSuccessBannerOpen = false;
        ErrorBannerText = text;
        IsErrorBannerOpen = true;  // manual dismiss only — no auto-dismiss
    }
    ```
  - [x] **Success auto-dismiss (~8 s, AC #8):** use a `DispatcherQueueTimer` from `_dispatcherQueue` (guard `_dispatcherQueue is not null`; in the test host it is null, so auto-dismiss is F5-only):
    ```csharp
    private void StartSuccessAutoDismiss()
    {
        if (_dispatcherQueue is null) return;
        _successDismissTimer?.Stop();
        _successDismissTimer = _dispatcherQueue.CreateTimer();
        _successDismissTimer.Interval = TimeSpan.FromSeconds(8);
        _successDismissTimer.IsRepeating = false;
        _successDismissTimer.Tick += (_, _) => IsSuccessBannerOpen = false;
        _successDismissTimer.Start();
    }
    ```
    (`private DispatcherQueueTimer? _successDismissTimer;` field. Manual dismiss via the `InfoBar`'s two-way `IsOpen` is independent and fine — a late timer tick just re-sets `false`, harmless.)
  - [x] **`OpenFolderCommand` (AC #9):**
    ```csharp
    [RelayCommand]
    private async Task OpenFolderAsync()
    {
        if (_lastOutputFolder is null) return;
        var ok = await _folderLauncher.LaunchFolderAsync(_lastOutputFolder);
        if (!ok)
        {
            SuccessBannerText = UiStrings.FolderNotFound; // MC-19, shown inline in the open banner
        }
    }
    ```
  - [x] Do **NOT** add the close-guard, `IErrorMapper`, or specific error strings (2.3). Do **NOT** auto-scroll the list or auto-select anything after merge.

- [x] **Task 7 — Wire the View (`MainWindow.xaml`)** (AC: #4, #7, #8, #9, #11)
  - [x] **Bind the ListView lock** — replace the three hardcoded `True` attached properties on `FileListView` so reorder/drag disable during merge:
    ```xml
    CanDragItems="{x:Bind ViewModel.CanReorderFiles, Mode=OneWay}"
    AllowDrop="{x:Bind ViewModel.CanReorderFiles, Mode=OneWay}"
    CanReorderItems="{x:Bind ViewModel.CanReorderFiles, Mode=OneWay}"
    ```
  - [x] **Row 1 — replace the collapsed `ProgressBar` placeholder** (`<ProgressBar Grid.Row="1" Visibility="Collapsed" />`) with a `StackPanel` holding both banners and the determinate progress bar (all "above the Action bar"):
    ```xml
    <StackPanel Grid.Row="1" Spacing="4" Padding="16,0">
        <InfoBar
            Severity="Success"
            IsOpen="{x:Bind ViewModel.IsSuccessBannerOpen, Mode=TwoWay}"
            Message="{x:Bind ViewModel.SuccessBannerText, Mode=OneWay}"
            IsClosable="True">
            <InfoBar.ActionButton>
                <Button
                    Content="{x:Bind strings:UiStrings.MergeSuccessOpenFolder}"
                    Command="{x:Bind ViewModel.OpenFolderCommand, Mode=OneTime}" />
            </InfoBar.ActionButton>
        </InfoBar>
        <InfoBar
            Severity="Error"
            IsOpen="{x:Bind ViewModel.IsErrorBannerOpen, Mode=TwoWay}"
            Message="{x:Bind ViewModel.ErrorBannerText, Mode=OneWay}"
            IsClosable="True" />
        <ProgressBar
            Minimum="0" Maximum="100"
            Value="{x:Bind ViewModel.MergeProgress, Mode=OneWay}"
            Visibility="{x:Bind local:MainWindow.BoolToVisibility(ViewModel.IsProgressVisible), Mode=OneWay}" />
    </StackPanel>
    ```
  - [x] **Disable Add/Merge/Remove during merge** comes for free: their `Command.CanExecute` now returns `false` while `IsMerging` (Task 5), so the bound buttons disable automatically. No extra XAML needed beyond what 2.1 wired.
  - [x] This is **view-only** wiring — no code-behind. The `strings:` and `local:` xmlns are already declared; `BoolToVisibility` already exists in `MainWindow.xaml.cs`. Verify rendering in the Task 9 F5 pass.

- [x] **Task 8 — Update the test factory and add VM tests** (AC: #4, #11)
  - [x] In `pdfjunior.Tests/ViewModels/MainViewModelTests.cs`, add the three new mocks and extend `CreateViewModel()` (this is the **only** place the new ctor params surface — all existing tests keep working):
    ```csharp
    private readonly IPdfMergeService _mergeService = Substitute.For<IPdfMergeService>();
    private readonly IOutputWriter _outputWriter = Substitute.For<IOutputWriter>();
    private readonly IFolderLauncher _folderLauncher = Substitute.For<IFolderLauncher>();

    private MainViewModel CreateViewModel() =>
        new(_pickerService, _validationService, _mergeService, _outputWriter, _folderLauncher);
    ```
  - [x] Add UI-lock derived-state tests (set `IsMerging` directly — its generated setter is public):
    - **`IsMerging_True_CanMergeFalse`:** seed a mergeable list (≥1 `Valid`), assert `CanMerge` true, then `vm.IsMerging = true` → assert `!vm.CanMerge`.
    - **`IsMerging_True_DisablesAddRemoveReorder`:** with a selected file, `vm.IsMerging = true` → assert `!vm.AddFilesCommand.CanExecute(null)`, `!vm.RemoveCommand.CanExecute(null)`, and `!vm.CanReorderFiles`.
  - [x] Add banner-clear-on-merge-press tests (mock the picker to **cancel** so no `StorageFile` is needed — AC #11):
    - **`MergePressed_ClearsSuccessBanner`:** `vm.IsSuccessBannerOpen = true;` `_pickerService.PickSaveFileAsync(Arg.Any<string>()).Returns((StorageFile?)null);` seed a mergeable list; `await vm.MergeCommand.ExecuteAsync(null);` → assert `!vm.IsSuccessBannerOpen` and `vm.Files` unchanged and `!vm.IsMerging`.
    - **`MergePressed_ClearsErrorBanner`:** same with `IsErrorBannerOpen`.
  - [x] Add Open-folder-gone test: `_folderLauncher.LaunchFolderAsync(Arg.Any<string>()).Returns(false);` invoke `vm.OpenFolderCommand.ExecuteAsync(null)` — guard: this is a no-op when `_lastOutputFolder` is null (the happy path that sets it needs a real `StorageFile`). So either (a) skip this as F5-only, or (b) only assert it does not throw. **Prefer (a)** — note it pending in the F5 checklist; do not fake a `StorageFile`.
  - [x] Create `pdfjunior.Tests/Services/PdfSharpMergeServiceTests.cs` (mirrors `PdfValidationServiceTests` — `FixturePath(name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", name)`, fixtures already copy to output):
    - **Single valid file (AC #3):** merge `["valid.pdf"]` into a `MemoryStream`; assert outcome is `MergeOutcome.Success`; reopen the stream with `PdfReader.Open(stream, PdfDocumentOpenMode.Import)` and assert its `PageCount` equals the source's page count.
    - **Two files concatenate in order (AC #2):** merge `["valid.pdf", "valid.pdf"]`; assert output `PageCount == 2 × source` (order isn't visually assertable from the fixture, but the count proves concatenation; if a distinct second fixture is added later, assert order then).
    - **Progress reported (AC #7):** collect `IProgress<double>` callbacks into a list; merge 2 files; assert the list ends at `100` and has one entry per file.
    - **Cancellation (FR-12 plumbing):** pass an already-cancelled token → `await Assert.ThrowsAsync<OperationCanceledException>(...)` (the `Task.Run(..., ct)` + `ThrowIfCancellationRequested` path).
  - [x] Add `using Windows.Storage;` to `MainViewModelTests.cs` for the `(StorageFile?)null` cast (the interface already exposes `StorageFile`). Add `using pdfjunior.Services;` if not present.
  - [x] Run the suite the project's way (Testing Notes): build `pdfjunior.Tests` for `-p:Platform=x64 -r win-x64` and run the produced `pdfjunior.Tests.exe` — `dotnet test` does **not** discover these. Keep the existing suite green (**43** at `ee64fc1`; ~47 after Story 2.1) and add to it.

- [ ] **Task 9 — Verify end-to-end (manual VS F5; MSIX cannot launch from CLI)** (AC: #1–#11)
  - [x] Build clean (0 warnings / 0 errors) for `-p:Platform=x64 -r win-x64`; sanity-check publish output size stays well under 100 MB (AC #1).
  - [ ] F5 checks: (a) add 3–4 valid PDFs, **Merge**, confirm "merged.pdf" on Desktop → fast merge shows **no** progress bar, then **success banner** "Merged successfully — merged.pdf"; **Open folder** opens Explorer to the Desktop. (b) Merge a **large** set (or throttle) so it exceeds 2 s → the thin **determinate** progress bar appears and advances; during it the file list cannot be reordered and Add/Merge/Remove are disabled, but the preview still scrolls. (c) **Single** valid file → merges to a valid 1-file PDF. (d) After success, the **file list is preserved**; merge again → the **previous banner clears** on the second Merge press. (e) Re-merge to the same filename → OS overwrite prompt (Flow 3); confirm → success. (f) Open folder after deleting the output folder → **"Folder not found"** inline. (g) Success banner **auto-dismisses ~8 s** and is closable before then. Mark pending until Antoine's F5 pass confirms.

## Dev Notes

### Scope Boundaries (what is and isn't this story)

| In scope (2.2) | Out of scope — later story |
|---|---|
| PDFsharp package + `PdfSharpMergeService` (import + save, off-thread, progress, cancel-checked) | Specific error taxonomy `IErrorMapper` + MC-15/16/17 mapping → **2.3** |
| `OutputWriter` (CachedFileManager direct write) + `FolderLauncher` | Window-close guard `ContentDialog` (FR-12) + `AppWindow.Closing` → **2.3** |
| DI registration of the 3 services; VM ctor + test factory update | Changing `MergeOutcome` to carry the exception → **2.3** |
| `MergeAsync` execution: merge → write → success banner → Open folder | Triggering the merge `CancellationTokenSource` from a user action → **2.3** (close guard) |
| UI-lock (`IsMerging`): read-only list, disabled Add/Merge/Remove | Preview rendering / validation changes (Epic 1, done) |
| `>2 s` determinate `ProgressBar`; `InfoBar` success + error **surfaces** | — |
| Banner clearing on Merge press (AC #11); generic MC-18 on failure | Per-exception error messages (the *content* of the error banner) → **2.3** |

This story shows **only** the **generic** MC-18 on any failure so the app stays whole; 2.3 replaces that generic path with the `IErrorMapper`-driven specific reasons and adds the close guard. Do not pull 2.3 work forward.

### Service composability decision (resolves the open deferred item — read carefully)

The deferred-work log flags: *"`IPdfMergeService` takes a raw `Stream` output but `IOutputWriter` takes a `StorageFile` — the two are not composable without glue."* **This story resolves it via a `MemoryStream` orchestrated in the VM:**

```
MainViewModel.MergeAsync:
   MemoryStream buffer
   ├─ PdfSharpMergeService.MergeAsync(validPaths, buffer, progress, ct)   // Stream in, MergeOutcome out
   ├─ buffer.Position = 0
   └─ OutputWriter.WriteAsync(buffer, destination)                        // Stream → StorageFile
```

Rationale (do not "improve" this away):
- **Testability:** `PdfSharpMergeService` never touches `StorageFile`, so it is **fully unit-testable** with a `MemoryStream` + the existing `Fixtures/valid.pdf` (the architecture's `PdfSharpMergeServiceTests` plan — page order, single-file, cancellation — needs exactly this). `StorageFile` is sealed/unconstructable in tests, so any design that pushed `StorageFile` into the merge service would make the merge logic untestable.
- **Boundary purity (architecture):** the merge service encapsulates PDFsharp and exposes only `Stream`/`MergeOutcome`; `OutputWriter` encapsulates the `StorageFile`/`CachedFileManager` write. The VM orchestrates both (it already owns the `destination` from the picker). No service-to-service coupling, no `StorageFile` leaking into PDFsharp code.
- For typical merge sizes the in-memory buffer is fine (NFR has no size limit, but real-world merges are modest; revisit only if memory pressure appears — same posture as the deferred "preview virtualization" note).

`PdfDocument.Save(output, closeStream: false)` is **mandatory** — without `closeStream: false`, PDFsharp disposes the `MemoryStream` and the subsequent `buffer.Position = 0` / `WriteAsync` throws `ObjectDisposedException`.

### Current state of the files you will touch (read before modifying)

- **`pdfjunior/Services/IPdfMergeService.cs`** *(no change):* `Task<MergeOutcome> MergeAsync(IReadOnlyList<string> paths, Stream output, IProgress<double>? progress, CancellationToken ct)` already declared. Implement `PdfSharpMergeService` against it.
- **`pdfjunior/Services/IOutputWriter.cs`** *(no change):* `Task WriteAsync(Stream source, StorageFile destination)` already declared (`using Windows.Storage;`). Implement `OutputWriter`.
- **`pdfjunior/Services/IFolderLauncher.cs`** *(no change):* `Task<bool> LaunchFolderAsync(string folderPath)` already declared. Implement `FolderLauncher`.
- **`pdfjunior/Models/MergeOutcome.cs`** *(no change):* `abstract record MergeOutcome` with `Success(string Path)` | `Failure(string Reason)`. Use as-is; `Success.Path` is unused here (pass `""`).
- **`pdfjunior/ViewModels/MainViewModel.cs`** *(primary change):*
  - The ctor is currently `(IFilePickerService, IPdfValidationService)` — add the 3 new params. `_filePickerService` and `RunOnUI`/`_dispatcherQueue` already exist; reuse them.
  - `CanMerge` (lines ~53–57) — append `&& !IsMerging`. `MergeDisabledReason` — **2.1** owns its ladder; do not re-touch it beyond the `IsMerging` re-raise that flows through `NotifyMergeStateChanged()`.
  - `MergeAsync` — **post-2.1** it opens the Save dialog and returns at the seam comment; replace from that seam onward per Task 6. Keep `[RelayCommand(CanExecute = nameof(CanMerge))]`.
  - `AddFilesAsync` is a bare `[RelayCommand]` (always enabled) — add `CanExecute = nameof(CanAddFiles)`.
  - `CanRemove()` returns `SelectedFile is not null` — add `&& !IsMerging`.
  - `NotifyMergeStateChanged()` already re-raises `CanMerge` + `MergeDisabledReason` + `MergeCommand` — call it from `OnIsMergingChanged`.
  - `RunOnUI` already no-ops inline when `_dispatcherQueue` is null (test host) — reuse it for progress/timer marshaling.
- **`pdfjunior/MainWindow.xaml`** *(view wiring):* `FileListView` has hardcoded `CanDragItems/AllowDrop/CanReorderItems="True"` (lines ~40–42) → bind to `CanReorderFiles`. Row 1 holds `<ProgressBar … Visibility="Collapsed" />` (lines ~117–120) → replace with the banner+progress `StackPanel`. `xmlns:strings` and `xmlns:local` already declared; `BoolToVisibility` static helper already in code-behind.
- **`pdfjunior/MainWindow.xaml.cs`** *(no change):* `BoolToVisibility` exists. No new code-behind — the merge UI is all VM-bound.
- **`pdfjunior/Strings/UiStrings.cs`** *(no change):* MC-13 `MergeSuccess` = "Merged successfully — {0}", MC-14 `MergeSuccessOpenFolder` = "Open folder", MC-18 `MergeErrorGeneric`, MC-19 `FolderNotFound` — **all already defined**. Do not add strings.
- **`pdfjunior/App.xaml.cs`** *(register 3 services):* `ConfigureServices()` currently registers `FilePickerService`, `IFilePickerService`, `IPdfValidationService`, `MainViewModel`. Add the merge/output/folder singletons (Task 4).
- **`pdfjunior.Tests/ViewModels/MainViewModelTests.cs`** *(factory + new tests):* `CreateViewModel()` injects 2 mocks today; add 3 more. Existing `CanMerge_*`/`Remove_*`/`Preview_*` tests are your templates for direct-state assertions.

### Progress, threading & the 2-second rule

- **Determinate by file count:** the merge owns the per-file import loop, so progress is `100 × (filesDone / totalFiles)` reported after each `AddPage` loop. The `ProgressBar` uses default `Minimum=0`/`Maximum=100`, `Value` bound to `MergeProgress`.
- **2 s reveal:** `IsProgressVisible` flips true only after a 2 s delay that is **cancelled** the instant the merge finishes (`showProgress.Cancel()` in `finally`). Sub-2 s merges therefore show nothing — *absence of feedback for fast merges is the intended design, not a defect* (EXPERIENCE Voice & Tone).
- **`Progress<double>` must be constructed on the UI thread** so its callback marshals back to the UI (it captures `SynchronizationContext` at construction). In `MergeAsync` you are on the UI thread → `new Progress<double>(...)` is correct. In the test host there is no sync context, but the full happy path isn't unit-tested (StorageFile), so this is F5-verified.
- **Never block the UI thread:** the synchronous PDFsharp work lives inside `Task.Run` in the service; the VM only `await`s. No `.Result`/`.Wait()`/`ConfigureAwait(false)` (WinUI sync context).

### UI-lock specifics (AC #4/#5)

- The lock is a single `IsMerging` flag. It gates **derived** state (`CanMerge`, `CanReorderFiles`) and **command** `CanExecute` (`Add`, `Merge`, `Remove`) — *no imperative disabling in code-behind*. The bound buttons disable automatically when their command's `CanExecute` returns false.
- **Drag-reorder off:** bind the ListView's `CanReorderItems`/`CanDragItems`/`AllowDrop` to `CanReorderFiles` (= `!IsMerging`). This is exactly the mechanism the 2026-06-18 change note prescribed for the merge lock (disable drag-reorder, **not** Move up/down buttons — those no longer exist).
- **Lock timing:** set `IsMerging = true` **after** `PickSaveFileAsync` returns a non-null destination — never while the dialog is open (AC #5; 2.1's "no lock while dialog open"). Cancel path returns before the flag is ever set.
- **Preview stays scrollable:** the `WebView2`/preview pane is not bound to `IsMerging` — leave it alone.

### Banner behavior (AC #8/#9/#11)

- **One at a time:** `ShowSuccess` closes the error banner and vice-versa; Merge press closes both (AC #11). Two distinct `InfoBar`s (Success, Error severities) with mutually-exclusive `IsOpen` is simpler than one severity-swapping bar and matches DESIGN's component table.
- **Success auto-dismiss ~8 s** via a one-shot `DispatcherQueueTimer`; **error is manual-dismiss only** (no timer). `InfoBar.IsClosable="True"` gives the user the manual X; because `IsOpen` is two-way, a manual close just sets the flag false — a later auto-dismiss tick re-setting false is harmless.
- **Open folder** uses the captured `_lastOutputFolder`; on `false` (folder gone) show **MC-19** inline (reuse the open success banner's `Message`). Do not throw, do not pop a dialog.

### Architecture compliance (guardrails)

- **MVVM:** all merge orchestration lives in `MainViewModel`; the View is pure `{x:Bind}` (explicit mode) with **no code-behind logic**. Use CommunityToolkit generators (`[ObservableProperty]`, `[RelayCommand]`) — no hand-rolled INPC, no `event` handlers for logic.
- **DI:** constructor injection only; the 3 new services are singletons registered in the single composition root (`App.ConfigureServices`). No `GetService`/service-locator in the VM.
- **Services boundary:** PDFsharp types (`PdfDocument`, `PdfReader`) stay inside `PdfSharpMergeService`; `StorageFile`/`CachedFileManager` stay inside `OutputWriter`/`FolderLauncher`. Only `Stream`, `MergeOutcome`, `StorageFile` (picker result), and `string` folder paths cross into the VM.
- **Strings:** every banner/label is an existing `UiStrings` constant via `string.Format` where needed; **no inline literals**. File-system facts (`.pdf`, folder paths) are not microcopy.
- **Theming:** `InfoBar Severity="Success"/"Error"` and `ProgressBar` use stock WinUI styling — **no custom colors/brushes/radii**. Accent stays only on Merge + list selection.
- **Async/threading:** off-thread merge via `Task.Run` in the service; UI/collection writes marshaled via `RunOnUI`/`Progress<T>`; never block the UI thread.
- **Nullable:** honor `<Nullable>enable</Nullable>` — null-check `destination`, `_lastOutputFolder`, `_mergeCts` rather than `!`.
- **Privacy:** PDFsharp is local/offline; no network/telemetry; any logging is `System.Diagnostics.Debug` only. The only new dependency is PDFsharp (MIT, pure-managed) — no GDI/WPF variant.

### Anti-patterns to avoid

- Do **not** push `StorageFile` into `PdfSharpMergeService` — keep it `Stream`-only (testability). Use the VM `MemoryStream` bridge.
- Do **not** omit `closeStream: false` on `PdfDocument.Save` — the MemoryStream must survive for `OutputWriter`.
- Do **not** call PDFsharp on the UI thread; do **not** `await` the merge without `Task.Run` inside the service.
- Do **not** show the progress bar for sub-2 s merges (no flash); do **not** make it indeterminate — it is determinate by file count.
- Do **not** lock the UI while the Save dialog is open (set `IsMerging` only after confirm).
- Do **not** implement `IErrorMapper`, the specific MC-15/16/17 strings, or the close guard here (2.3). On failure show **only** MC-18 generic.
- Do **not** change `MergeOutcome`'s shape, `MergeDisabledReason`'s ladder (2.1), or `CanMerge`'s core conditions (only append `&& !IsMerging`).
- Do **not** remove or reorder files after a successful merge — the list is preserved (AC #10).
- Do **not** auto-select a neighbor or auto-scroll the list during/after merge.
- Do **not** add a NuGet GDI/WPF PDFsharp variant; use the base `PDFsharp` package (footprint, NFR-6).
- Do **not** construct or mock `StorageFile` in tests; the merge happy path (real destination) is F5-only.

### Previous story intelligence

**From Story 2.1 (the seam you extend):**
- `MergeAsync` post-2.1 is `async Task` and ends at `// Story 2.2 plugs the off-thread merge here` after obtaining `destination`. Build forward from there; the cancel-returns-silently branch (`if (destination is null) return;`) stays.
- The HWND-aware `FileSavePicker` is implemented in `FilePickerService.PickSaveFileAsync` (2.1); the VM already calls it with `UiStrings.DefaultMergeFileName`.
- The Merge button is wrapped in a tooltip `Border` (2.1); the disabled tooltip uses `MergeDisabledReason`. Your `IsMerging` gating disables Merge during a merge — the wrapper then shows no tooltip (reason is for the *pre*-merge disabled states; during a merge the button is simply disabled, which is acceptable — no AC requires a "merging" tooltip).

**From Epic 1 (done) and the reorder pivot:**
- `MainViewModel` already wires `Files.CollectionChanged` + per-item `PropertyChanged` → `NotifyMergeStateChanged()`, which re-raises `CanMerge` + `MergeDisabledReason` + `MergeCommand.NotifyCanExecuteChanged()`. Calling it from `OnIsMergingChanged` is enough to propagate the lock to gating.
- `RunOnUI` runs inline when `_dispatcherQueue` is null (test host), so VM logic that touches observable state is synchronously testable; UI-thread-only bits (DispatcherQueueTimer auto-dismiss, 8 s timing, real picker) are **F5-only**.
- `[ObservableProperty]`/`[RelayCommand]` use **partial** members for the WinUI generators. `PdfFileItem` uses reference equality (fine — merge reads `Status`/`Path`).
- Tests run via the **direct-exe** path; MSIX visuals (InfoBar, ProgressBar, Explorer launch, Save dialog) are **manual VS F5**. Mirror 1.x/2.1: state F5 items pending, never fake automated E2E.

**Still-open deferred items relevant here:**
- `MergeOutcome.Failure` carries only a `string` (no exception) — **fine for 2.2** (generic banner); 2.3 must change it to carry the exception before `IErrorMapper` can map specifics.
- The `IPdfMergeService` (Stream) vs `IOutputWriter` (StorageFile) composability is **resolved by this story** (MemoryStream bridge — see decision above); update/close that deferred-work line when done.
- Orphaned `Task.Run` after validation timeout and the other Epic-1 debt are unrelated to merge — leave them.

### Git intelligence

Recent cadence is one focused `feat:` commit per story (`dd65d1e` 1.2, `07e2d78` 1.3, `859232b`/`03155fb`/`0bcd2ce` reorder+1.4+webview) plus `docs:` reconciliations (`3ebe699`, `ee64fc1`). Follow the same one-clean-`feat:`-commit convention (e.g. `feat: implement story 2-2 execute merge and report success`). This story **adds new surface** (3 service impls + PDFsharp + merge UI) on top of 2.1 — it is larger than 2.1; keep the commit cohesive and the build green.

### Project Structure Notes

```
pdfjunior/
└── pdfjunior/
    ├── pdfjunior.csproj                 [UPDATE] — add PackageReference PDFsharp 6.2.4
    ├── App.xaml.cs                       [UPDATE] — register IPdfMergeService/IOutputWriter/IFolderLauncher singletons
    ├── MainWindow.xaml                   [UPDATE] — bind ListView reorder to CanReorderFiles; Row 1 InfoBars + determinate ProgressBar
    ├── Services/
    │   ├── PdfSharpMergeService.cs       [NEW]    — PDFsharp import+save, off-thread, progress, cancel-checked → MergeOutcome
    │   ├── OutputWriter.cs               [NEW]    — CachedFileManager direct write (Stream → StorageFile, truncate)
    │   └── FolderLauncher.cs             [NEW]    — Launcher.LaunchFolderAsync; false when folder gone (MC-19)
    └── ViewModels/
        └── MainViewModel.cs              [UPDATE] — +3 ctor deps; IsMerging/CanReorderFiles/MergeProgress/IsProgressVisible/
                                                     banner state; MergeAsync execution; OpenFolderCommand; CanAddFiles; CanRemove+IsMerging
pdfjunior.Tests/
├── ViewModels/MainViewModelTests.cs     [UPDATE] — factory +3 mocks; IsMerging lock tests; merge-press-clears-banner tests
└── Services/PdfSharpMergeServiceTests.cs[NEW]    — single-file, two-file page-count, progress, cancellation (MemoryStream + Fixtures)
```

No `UiStrings` changes, no `MergeOutcome`/interface changes, no new code-behind. `IPdfMergeService.cs`, `IOutputWriter.cs`, `IFolderLauncher.cs`, `MainWindow.xaml.cs` are unchanged.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story 2.2: Execute Merge & Report Success] — story statement + AC seed (FR-8, FR-9)
- [Source: _bmad-output/planning-artifacts/epics.md#Requirements Inventory] — FR-8 (off-thread merge, progress >2 s, determinate by file count), FR-9 (success banner names file, ~8 s auto-dismiss, Open folder, list preserved), NFR-1 (non-blocking), NFR-6 (<100 MB)
- [Source: _bmad-output/planning-artifacts/architecture.md#PDF Engine & Library Strategy] — PDFsharp 6.2.4 (MIT, pure-managed): `PdfDocumentOpenMode.Import`, append pages in list order, save to stream
- [Source: _bmad-output/planning-artifacts/architecture.md#Concurrency & Cancellation Model] — single `Task.Run` + `CancellationToken`; `IProgress<double>` determinate by file count; progress bar only after 2 s; token checked between file imports
- [Source: _bmad-output/planning-artifacts/architecture.md#Output / File-I/O Safety] — direct write to destination `StorageFile` via `CachedFileManager.DeferUpdates`/`CompleteUpdatesAsync`; no temp file, no rollback, partial output permitted
- [Source: _bmad-output/planning-artifacts/architecture.md#App Structure, MVVM & Dependency Injection] — `IPdfMergeService` (IProgress+CT), `IOutputWriter` (writes to destination stream), `IFolderLauncher` (open Explorer); singletons; VM orchestrates services
- [Source: _bmad-output/planning-artifacts/architecture.md#Requirements → Structure Mapping] — FR-8 → `PdfSharpMergeService` + `OutputWriter`; FR-9 → VM + `InfoBar` + `FolderLauncher`
- [Source: _bmad-output/planning-artifacts/architecture.md#Implementation Patterns & Consistency Rules] — `x:Bind` explicit, constructor DI, result types, `UiStrings` for copy, theme resources only, never block UI thread
- [Source: _bmad-output/planning-artifacts/ux-designs/ux-pdf-junior-2026-06-14/EXPERIENCE.md#Microcopy Inventory] — MC-13 success, MC-14 Open folder, MC-18 generic, MC-19 folder-gone (verbatim strings)
- [Source: _bmad-output/planning-artifacts/ux-designs/ux-pdf-junior-2026-06-14/EXPERIENCE.md#State Patterns] — "Merge pressed → banner dismissed"; "<2 s → no progress; ≥2 s → determinate bar, UI locked, preview scrollable"; "Success → InfoBar + Open folder, ~8 s, list preserved, UI unlocked"
- [Source: _bmad-output/planning-artifacts/ux-designs/ux-pdf-junior-2026-06-14/EXPERIENCE.md#Component Patterns] — InfoBar (success auto-dismiss ~8 s; error manual; one at a time); ProgressBar thin/determinate/only after 2 s; Merge press dismisses banners
- [Source: _bmad-output/planning-artifacts/ux-designs/ux-pdf-junior-2026-06-14/DESIGN.md#Components] — `InfoBar Severity=Success/Error`, `ProgressBar` determinate, stock WinUI, no custom colors/radii
- [Source: _bmad-output/implementation-artifacts/sprint-status.yaml] — 2026-06-18 change note: merge UI-lock disables drag-reorder (`CanReorderItems`/`CanDragItems`/`AllowDrop`=False) rather than Move up/down
- [Source: _bmad-output/implementation-artifacts/2-1-merge-gating-save-dialog.md] — the `MergeAsync` seam this story extends; `PickSaveFileAsync`, `DefaultMergeFileName`, tooltip `Border`
- [Source: _bmad-output/implementation-artifacts/deferred-work.md] — `MergeOutcome.Failure` lacks exception (2.3); `IPdfMergeService`(Stream)/`IOutputWriter`(StorageFile) composability (resolved here via MemoryStream)
- [Source: pdfjunior/Services/IPdfMergeService.cs] — `MergeAsync(IReadOnlyList<string>, Stream, IProgress<double>?, CancellationToken)` to implement
- [Source: pdfjunior/Services/IOutputWriter.cs] — `WriteAsync(Stream, StorageFile)` to implement
- [Source: pdfjunior/Services/IFolderLauncher.cs] — `LaunchFolderAsync(string) : Task<bool>` to implement
- [Source: pdfjunior/Models/MergeOutcome.cs] — `Success(string Path)` | `Failure(string Reason)` (use as-is)
- [Source: pdfjunior/ViewModels/MainViewModel.cs] — ctor to extend; `CanMerge`/`CanRemove`/`AddFilesAsync`/`MergeAsync`/`NotifyMergeStateChanged`/`RunOnUI`
- [Source: pdfjunior/MainWindow.xaml] — `FileListView` reorder props (lines ~40–42); Row 1 collapsed `ProgressBar` to replace
- [Source: pdfjunior/Strings/UiStrings.cs] — MC-13/14/18/19 already defined (no change)
- [Source: pdfjunior/App.xaml.cs] — `ConfigureServices()` to add 3 singletons
- [Source: pdfjunior.Tests/Services/PdfValidationServiceTests.cs] — fixture-loading pattern `Path.Combine(AppContext.BaseDirectory, "Fixtures", name)` to mirror for the merge-service tests
- [Source: pdfjunior.Tests/ViewModels/MainViewModelTests.cs] — `CreateViewModel()` factory + `IsMerging`/banner test templates
- [Source: memory project_run_tests] — build x64/win-x64 and run `pdfjunior.Tests.exe` directly; `dotnet test` does not discover
- [Source: memory feedback_msix_packaging] — `WindowsPackageType=MSIX` (app project) — unchanged this story
- [Source: memory feedback_use_yarn] — yarn is for JS tooling only; PDFsharp is a **NuGet** package (`PackageReference` / `dotnet add package`), not a yarn dependency

## Dev Agent Record

### Agent Model Used

claude-opus-4-8 (Claude Opus 4.8)

### Debug Log References

- **PDFsharp rejects the `valid.pdf` fixture.** Initial `PdfSharpMergeServiceTests` reused `Fixtures/valid.pdf` (per the story's test plan) and failed: PDFsharp 6.2.4 threw `Unexpected token 'endobj' in PDF stream. The file may be corrupted.` That fixture is a minimal hand-crafted PDF the WinRT renderer (`Windows.Data.Pdf`, used by `PdfValidationService`) accepts but PDFsharp's stricter parser cannot. Since the merge engine **is** PDFsharp, the tests now generate their PDF inputs with PDFsharp (`PdfDocument` + `AddPage()`, known page counts, temp files cleaned up via `IDisposable`) — self-contained and deterministic. No production-code change was needed; the merge code was correct.
- **`InformationOnly` is obsolete in PDFsharp 6.2.4** ("use Import instead") — the page-count helper was switched to `PdfDocumentOpenMode.Import` to keep the build at 0 warnings.
- **Cancellation assertion uses `ThrowsAnyAsync`.** `Task.Run(delegate, ct)` with a pre-cancelled token yields a `TaskCanceledException` (a subclass of `OperationCanceledException`); xUnit's exact-type `ThrowsAsync<OperationCanceledException>` would fail, so the test uses `Assert.ThrowsAnyAsync<OperationCanceledException>`.

### Completion Notes List

- **Tasks 1–8 complete and verified.** Clean build (0 warnings / 0 errors) for `-p:Platform=x64 -r win-x64`; full suite **56/56 green** (47 pre-2.2 + 9 new: 4 `PdfSharpMergeServiceTests` + `IsMerging_True_CanMergeFalse`, `IsMerging_True_DisablesAddRemoveReorder`, `MergePressed_ClearsSuccessBanner`, `MergePressed_ClearsErrorBanner`, `OpenFolder_NoPriorMerge_SafeNoOp`).
- **Composability resolved via the VM `MemoryStream` bridge** exactly as the story's decision section prescribes: `PdfSharpMergeService` (Stream-only) → `buffer.Position = 0` → `OutputWriter.WriteAsync(buffer, destination)`. `PdfDocument.Save(output, closeStream: false)` keeps the buffer alive for the writer.
- **AC #1 / NFR-6:** PDFsharp is pure-managed; its assemblies add ~1.5 MB total to the build output — far under the 100 MB cap.
- **Validation/merge engine mismatch (observation, not a 2.2 defect):** files are validated with the WinRT renderer but merged with PDFsharp. A pathological file could pass validation yet fail the PDFsharp parse, surfacing the **generic** MC-18 banner — acceptable for 2.2 (the app stays whole). Story 2.3's `IErrorMapper` will refine the failure messaging.
- **Scope honored:** no `IErrorMapper`, no specific error strings, no close-guard, no `MergeOutcome` shape change, no `MergeDisabledReason`/`CanMerge`-core changes (only appended `&& !IsMerging`). No new `UiStrings`, no new code-behind.
- **Task 9 F5 pass is PENDING (Antoine).** The MSIX app cannot launch from the CLI, so the visual/E2E checks (no-progress fast merge, ≥2 s determinate bar + UI lock, success banner + Open folder, overwrite prompt, folder-gone "Folder not found", ~8 s auto-dismiss) are F5-only — mirroring the 1.x / 2.1 convention. The story advances to **review** for that pass plus code review.

### File List

- `pdfjunior/pdfjunior.csproj` (modified) — added `PackageReference PDFsharp 6.2.4`
- `pdfjunior/Services/PdfSharpMergeService.cs` (new) — PDFsharp import + save, off-thread, progress, cancel-checked → `MergeOutcome`
- `pdfjunior/Services/OutputWriter.cs` (new) — `CachedFileManager` direct write (Stream → StorageFile, truncate)
- `pdfjunior/Services/FolderLauncher.cs` (new) — `Launcher.LaunchFolderAsync`; returns false when the folder is gone (MC-19)
- `pdfjunior/App.xaml.cs` (modified) — registered `IPdfMergeService`/`IOutputWriter`/`IFolderLauncher` singletons
- `pdfjunior/ViewModels/MainViewModel.cs` (modified) — +3 ctor deps; `IsMerging`/`CanReorderFiles`/`MergeProgress`/`IsProgressVisible`/banner state; `MergeAsync` execution; `StartProgressDelay`/`ShowSuccess`/`ShowError`/`StartSuccessAutoDismiss`/`OpenFolderCommand`; `CanAddFiles`; `CanRemove` + `IsMerging`; `CanMerge` + `IsMerging`
- `pdfjunior/MainWindow.xaml` (modified) — bound ListView reorder to `CanReorderFiles`; replaced the collapsed Row 1 ProgressBar with success/error `InfoBar`s + determinate `ProgressBar`
- `pdfjunior.Tests/ViewModels/MainViewModelTests.cs` (modified) — factory +3 mocks; UI-lock and merge-press-clears-banner tests; Open-folder no-op guard test
- `pdfjunior.Tests/Services/PdfSharpMergeServiceTests.cs` (new) — single-file, two-file page-count, progress, cancellation (PDFsharp-generated inputs)

## Change Log

| Date | Version | Description |
|---|---|---|
| 2026-06-20 | 0.2.0 | Implemented Story 2.2 — Execute Merge & Report Success. Added PDFsharp 6.2.4 + `PdfSharpMergeService`/`OutputWriter`/`FolderLauncher`; wired DI; extended `MainViewModel` with merge execution, UI-lock, >2 s progress, and success/error banners; added InfoBar + ProgressBar UI. 9 new tests; suite 56/56 green. F5 verification pending. |
