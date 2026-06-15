# Deferred Work

## Deferred from: code review of 1-1-project-setup-app-shell-layout (2026-06-15)

- Cursor management uses raw P/Invoke (LoadCursor/SetCursor) instead of WinUI InputCursor/ProtectedCursor API — works but framework may override cursor changes on its own schedule [MainWindow.xaml.cs:86-96]
- PdfFileItem lacks Equals/GetHashCode — collection operations use reference equality, which will silently break lookup-by-value if needed in future stories [Models/PdfFileItem.cs]
- MergeOutcome.Failure carries only a string Reason, not the original exception — IErrorMapper cannot be used on merge failures without the exception [Models/MergeOutcome.cs]
- IPdfMergeService takes a raw Stream output but IOutputWriter takes a StorageFile — the two interfaces are not composable without glue code. Clarify ownership when implementing merge story [Services/]
