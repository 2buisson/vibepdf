# Deferred Work

## Deferred from: code review of 1-1-project-setup-app-shell-layout (2026-06-15)

- Cursor management uses raw P/Invoke (LoadCursor/SetCursor) instead of WinUI InputCursor/ProtectedCursor API — works but framework may override cursor changes on its own schedule [MainWindow.xaml.cs:86-96]
- PdfFileItem lacks Equals/GetHashCode — collection operations use reference equality, which will silently break lookup-by-value if needed in future stories [Models/PdfFileItem.cs]
- MergeOutcome.Failure carries only a string Reason, not the original exception — IErrorMapper cannot be used on merge failures without the exception [Models/MergeOutcome.cs]
- ~~IPdfMergeService takes a raw Stream output but IOutputWriter takes a StorageFile — the two interfaces are not composable without glue code. Clarify ownership when implementing merge story [Services/]~~ — RESOLVED in story 2-2: `MainViewModel.MergeAsync` orchestrates a `MemoryStream` bridge — `PdfSharpMergeService` writes to the buffer (`Save(output, closeStream: false)`), then `OutputWriter.WriteAsync(buffer, destination)` copies it to the `StorageFile`. No service-to-service coupling; merge stays Stream-only and fully unit-testable.

## Deferred from: code review of 1-2-add-validate-pdf-files (2026-06-16)

- Orphaned Task.Run continues after timeout — semaphore released but WinRT LoadFromFileAsync keeps running in background, can exceed semaphore concurrency bound [MainViewModel.cs:148]
- ~~Semaphore slots held by removed files — in-flight validations not cancelled on item removal, blocks other validations [MainViewModel.cs:18]~~ — RESOLVED in story 1-3: per-item `_validationCts` cancelled on Remove, freeing the semaphore slot
- Inline string literals in XAML empty-state placeholders — should use UiStrings via x:Bind [MainWindow.xaml:57,114]
- GetRequiredService called in MainWindow constructor instead of injection from composition root [MainWindow.xaml.cs:30]
- IsPasswordError relies on locale-dependent ex.Message.Contains("password") as fallback for HResult check [PdfValidationService.cs:27]
- Bare catch in PdfValidationService classifies all unknown errors (including OOM, access-denied) as ErrorCorrupt [PdfValidationService.cs:23]
- Task.Delay(200) in test WaitForValidation — timing-dependent; replace with deterministic sync when tests grow [MainViewModelTests.cs:263]
- ~~Validation may write status to PdfFileItem after it has been removed from Files collection [MainViewModel.cs:162]~~ — RESOLVED in story 1-3: every `RunOnUI` status-write callback in `ValidateFileAsync` now guards with `if (!Files.Contains(item)) return;`

## Deferred from: code review of spec-merge-result-dialog (2026-06-27)

- No guard against a second concurrent `ContentDialog` (`async void` + unguarded `ShowAsync` throws `COMException`) — not reachable now (merge UI-lock + modal block re-entry); becomes reachable when story 2.3's close-guard dialog can overlap. Address with 2.3 [MainWindow.xaml.cs:91]
- `MergeCompleted += OnMergeCompleted` is never unsubscribed — benign for a single-window app with a singleton VM; revisit if a second MainWindow can ever be created against the same VM [MainWindow.xaml.cs:38]
- Repeated "Open folder" clicks (dialog stays open, button enabled) launch concurrent `LaunchFolderAsync` calls / multiple Explorer windows — minor UX; would need a re-entrancy guard [MainWindow.xaml.cs:83]
- Spec Execution checklist says PrimaryButtonClick "takes a deferral" but the amendment, Design Notes, and code all use no deferral — reconcile the stale checklist text [spec-merge-result-dialog.md:53]

## Deferred from: code review of spec-remove-mvvm (2026-06-28)

- ~~Preview-render error `catch` in `UpdatePreviewAsync` guards only on `ReferenceEquals(item, _selectedFile)`, not also on `cts.IsCancellationRequested` like the success path does — a superseded-then-re-selected file whose stale render later throws a non-OCE could overwrite a freshly-rendered Valid preview with the corrupt placeholder. Faithful 1:1 port of the pre-MVVM-removal code (parity was the bar), so deferred rather than fixed here [MainWindow.xaml.cs UpdatePreviewAsync catch]~~ — RESOLVED in spec-remove-mvvm-deferred-fixes: catch guard now also gates on `!cts.IsCancellationRequested`, symmetric with the success path
- ~~`AddButton_Click` / `MergeButton_Click` are now `async void` event handlers with the file-picker `await` outside any try; an exception from `PickFilesAsync`/`PickSaveFileAsync` would surface as an unhandled crash rather than the old command's unobserved-task fault. Near-unreachable (pickers rarely throw) but a genuine exception-semantics shift introduced by replacing `AsyncRelayCommand` with event handlers [MainWindow.xaml.cs:97,346]~~ — RESOLVED in spec-remove-mvvm-deferred-fixes: each picker `await` is wrapped in try/catch that no-ops, restoring the old non-crash semantics

## Deferred from: code review of spec-localization-french (2026-06-28)

- `ResourceLoader.GetString` returns an empty string (never throws) for a key missing from all locales — no guard or visible fallback, so a missing/mistyped key renders blank UI instead of failing loudly. Latent until story 2.3 wires the currently-unused `MergeError*`/`CloseGuard*` keys; a single dropped translation then produces blank UI with no build error [MainWindow.xaml.cs]
- No automated guard enforcing key-set + format-token parity across `en-US/Resources.resw` and `fr-FR/Resources.resw`. Parity holds today (32/32) but nothing prevents drift; a future one-sided edit silently falls back to en-US or empty at runtime. Consider a small unit test that diffs the two key sets and checks placeholder counts [vibepdf/Strings/**]
- Unguarded `string.Format` at `UpdateTitle` (:461) and `FormatStatus` (:580) — translation correctness is now the only thing keeping format strings valid; a localized value with an unbalanced `{` throws `FormatException` at these sites (the merge-success site at :420 is inside try/catch). First-party strings make this low-risk; a format-validation test would cover it. Pre-existing pattern [MainWindow.xaml.cs:461,580]
- French 0-count renders the plural ("0 pages"); French treats 0 as singular ("0 page"). The `pageCount == 1 ? Singular : Plural` selection rule was not localized when the plural strings were. Likely unreachable for a Valid PDF (0-page valid file) [MainWindow.xaml.cs:580]
- Number formatting uses `CultureInfo.CurrentCulture` (regional format) while the string is chosen by MRT from the OS display language — a user with display-language English + region France (or reverse) gets a mismatched string/number pairing. Negligible practical impact for these integer page counts and 0-decimal percentages (no separators rendered); no `IFormatProvider` passed anywhere [MainWindow.xaml.cs:463,580]
- `static readonly ResourceLoader Resources = new()` first-touch (ctor, :83) is unguarded — if `resources.pri` cannot be loaded the type initializer throws `TypeInitializationException` at startup with no fallback. Mitigated by the MSIX packaging guarantee [MainWindow.xaml.cs:31]
