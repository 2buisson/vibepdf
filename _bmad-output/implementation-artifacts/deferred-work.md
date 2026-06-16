# Deferred Work

## Deferred from: code review of 1-1-project-setup-app-shell-layout (2026-06-15)

- Cursor management uses raw P/Invoke (LoadCursor/SetCursor) instead of WinUI InputCursor/ProtectedCursor API — works but framework may override cursor changes on its own schedule [MainWindow.xaml.cs:86-96]
- PdfFileItem lacks Equals/GetHashCode — collection operations use reference equality, which will silently break lookup-by-value if needed in future stories [Models/PdfFileItem.cs]
- MergeOutcome.Failure carries only a string Reason, not the original exception — IErrorMapper cannot be used on merge failures without the exception [Models/MergeOutcome.cs]
- IPdfMergeService takes a raw Stream output but IOutputWriter takes a StorageFile — the two interfaces are not composable without glue code. Clarify ownership when implementing merge story [Services/]

## Deferred from: code review of 1-2-add-validate-pdf-files (2026-06-16)

- Orphaned Task.Run continues after timeout — semaphore released but WinRT LoadFromFileAsync keeps running in background, can exceed semaphore concurrency bound [MainViewModel.cs:148]
- ~~Semaphore slots held by removed files — in-flight validations not cancelled on item removal, blocks other validations [MainViewModel.cs:18]~~ — RESOLVED in story 1-3: per-item `_validationCts` cancelled on Remove, freeing the semaphore slot
- Inline string literals in XAML empty-state placeholders — should use UiStrings via x:Bind [MainWindow.xaml:57,114]
- GetRequiredService called in MainWindow constructor instead of injection from composition root [MainWindow.xaml.cs:30]
- IsPasswordError relies on locale-dependent ex.Message.Contains("password") as fallback for HResult check [PdfValidationService.cs:27]
- Bare catch in PdfValidationService classifies all unknown errors (including OOM, access-denied) as ErrorCorrupt [PdfValidationService.cs:23]
- Task.Delay(200) in test WaitForValidation — timing-dependent; replace with deterministic sync when tests grow [MainViewModelTests.cs:263]
- ~~Validation may write status to PdfFileItem after it has been removed from Files collection [MainViewModel.cs:162]~~ — RESOLVED in story 1-3: every `RunOnUI` status-write callback in `ValidateFileAsync` now guards with `if (!Files.Contains(item)) return;`
