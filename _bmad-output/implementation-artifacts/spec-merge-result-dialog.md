---
title: 'Replace merge result info bars with a ContentDialog'
type: 'feature'
created: '2026-06-27'
status: 'done'
context: []
baseline_commit: '11d2a84b2a15d130e5c79d628501156efeb509e7'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** When a merge finishes, the result is shown in two inline `InfoBar` banners (success with an "Open folder" action, and error). The user wants the outcome surfaced in a modal `ContentDialog` instead, so the result is unmissable and acknowledged before continuing.

**Approach:** Delete both merge `InfoBar`s and the view-model banner state. The view model raises a `MergeCompleted` event carrying severity + message; the View shows a modal `ContentDialog`. On success the dialog offers an "Open folder" button that opens the folder **without dismissing the dialog**; on error it offers only dismiss.

## Boundaries & Constraints

**Always:** Keep MVVM separation — the view model stays UI-control-agnostic and only raises the event; the View (code-behind) builds the dialog. Reuse the existing `UiStrings` message constants (MC-13/18/19). Set `ContentDialog.XamlRoot` and `RequestedTheme` so the dialog renders in the window and matches the active theme. Leave the determinate `ProgressBar`, the merge UI-lock, gating, and validation behavior untouched.

**Ask First:** Reworded wording of the existing merge messages (MC-13–MC-19); adding icons/custom content to the dialog beyond title + message + buttons.

**Never:** Reintroduce `InfoBar` for merge results; auto-dismiss the dialog; block the UI thread; touch merge gating / validation / save-dialog flow.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Merge succeeds | write completes | Modal `ContentDialog`: success message, "Open folder" + "Close" buttons; no InfoBar | N/A |
| Merge fails | `MergeAsync` returns Failure or throws (non-cancel) | Modal `ContentDialog`: error message, "Close" only | N/A |
| Open folder, folder present | user clicks "Open folder" | Folder opens; dialog stays open | N/A |
| Open folder, folder gone | `LaunchFolderAsync` → false | Same dialog stays open; its content changes to "Folder not found" | Inline (no second dialog) |
| Save dialog cancelled | picker returns null | No dialog; silent no-op | N/A |
| Merge cancelled (close-guard) | `OperationCanceledException` | No dialog | N/A |

</frozen-after-approval>

## Code Map

- `vibepdf/ViewModels/MergeResultEventArgs.cs` -- already present (untracked); defines `MergeResultSeverity` (Success/Error) + `MergeResultEventArgs(severity, message)`. Reuse as-is.
- `vibepdf/ViewModels/MainViewModel.cs` -- owns merge flow + banner state (`IsSuccessBannerOpen`/`SuccessBannerText`/`IsErrorBannerOpen`/`ErrorBannerText`, `ShowSuccess`/`ShowError`, `OpenFolderAsync`).
- `vibepdf/MainWindow.xaml` -- two merge `InfoBar`s + `ProgressBar` in the row-1 StackPanel (lines ~149-168).
- `vibepdf/MainWindow.xaml.cs` -- View code-behind; will own dialog construction. No `ContentDialog` exists yet.
- `vibepdf/Strings/UiStrings.cs` -- `AppTitle`, `MergeSuccess`, `MergeSuccessOpenFolder`, `MergeErrorGeneric`, `FolderNotFound`.
- `vibepdf.Tests/ViewModels/MainViewModelTests.cs` -- `MergePressed_ClearsSuccessBanner`/`...ErrorBanner`, `OpenFolder_NoPriorMerge_SafeNoOp`.

## Tasks & Acceptance

**Execution:**
- [x] `vibepdf/ViewModels/MainViewModel.cs` -- Remove the four banner observable properties, the `ShowSuccess`/`ShowError` helpers, and the AC#11 banner-clear lines at the top of `MergeAsync`. Add `public event EventHandler<MergeResultEventArgs>? MergeCompleted;` and a `RaiseMergeResult(severity, message)` helper. Raise `Success` after a successful write, `Error` on Failure/exception (keep the `OperationCanceledException` silent path). Replace the `[RelayCommand] OpenFolderAsync` with `public Task<bool> TryOpenLastFolderAsync()` returning whether the folder opened (false when `_lastOutputFolder` is null or `LaunchFolderAsync` fails) — no event raised. Keep `_lastOutputFolder`, progress, and UI-lock unchanged.
- [x] `vibepdf/MainWindow.xaml` -- Delete both `<InfoBar>` elements; keep the `<ProgressBar>` (and its StackPanel/row).
- [x] `vibepdf/MainWindow.xaml.cs` -- Subscribe to `ViewModel.MergeCompleted` in the constructor. Handler builds a `ContentDialog` (`XamlRoot = Content.XamlRoot`, `RequestedTheme` from the root's `ActualTheme`, `Title = UiStrings.AppTitle`, `Content = e.Message`, `CloseButtonText = UiStrings.DialogClose`); for `Success` also set `PrimaryButtonText = UiStrings.MergeSuccessOpenFolder`, `DefaultButton = Primary`, and a `PrimaryButtonClick` handler that sets `args.Cancel = true` (keep the dialog open), takes a deferral, calls `TryOpenLastFolderAsync()`, and on false sets the dialog `Content` to `FolderNotFound`. `await ShowAsync()`.
- [x] `vibepdf/Strings/UiStrings.cs` -- Add `public const string DialogClose = "Close";`.
- [x] `vibepdf.Tests/ViewModels/MainViewModelTests.cs` -- Delete the two `MergePressed_Clears*Banner` tests (state removed). Extend `Merge_SaveDialogCancelled_SilentNoOp` and `OpenFolder_NoPriorMerge_SafeNoOp` (now calling `TryOpenLastFolderAsync()`, asserting it returns false) to subscribe to `MergeCompleted` and assert it is **not** raised on those paths.

**Acceptance Criteria:**
- Given a merge completes successfully, when the file is written, then a modal `ContentDialog` shows the success message with "Open folder" and "Close" buttons and no `InfoBar` is rendered.
- Given the success dialog, when the user clicks "Open folder", then the output folder opens and the dialog stays open.
- Given a merge fails (non-cancel), when reported, then a modal `ContentDialog` shows the error message with only "Close".
- Given "Open folder" is clicked but the folder is gone, when launch fails, then the same dialog stays open and its content changes to "Folder not found" (no second dialog).
- Given the Save dialog is cancelled, when no merge runs, then no `ContentDialog` appears and `MergeCompleted` is not raised.

## Spec Change Log

- **2026-06-27 — "Open folder" must keep the dialog open** (human renegotiation during review). *Was:* "Open folder" was the dialog's primary button, which dismissed the dialog, and an open-folder failure popped a *second* `ContentDialog`. *Amended:* the success dialog cancels its close on `PrimaryButtonClick` (Cancel set before the await — no deferral needed) and opens the folder, so it stays open; the view model's `OpenFolderCommand` became `TryOpenLastFolderAsync()` returning a bool, and folder-not-found is shown **inline** in the still-open dialog. *Known-bad avoided:* a second `ContentDialog` raised while the first is still open throws (WinUI allows only one at a time). *KEEP:* the `MergeCompleted` event for the success/merge-error dialogs, the app-name title, and the `XamlRoot`/`RequestedTheme` wiring.

## Design Notes

Title both dialogs with `AppTitle` ("Vibe PDF") rather than a severity-derived title so the success and error dialogs read consistently; the message text carries the specific meaning. "Open folder" must not dismiss the dialog — set `args.Cancel = true` in `PrimaryButtonClick` *before* the first `await` (the dialog reads `Cancel` when the synchronous part returns, so no deferral is needed) and open the folder in the continuation. Because a folder-gone result must not raise a second dialog while the first is open (WinUI permits only one), `TryOpenLastFolderAsync()` returns a bool and the View surfaces "Folder not found" inline. This is the first `ContentDialog` in the app — story 2.3's close-guard dialog (not yet built) will follow the same `XamlRoot`/`RequestedTheme` wiring.

Handler shape:

```csharp
private async void OnMergeCompleted(object? sender, MergeResultEventArgs e)
{
    var dialog = new ContentDialog
    {
        XamlRoot = Content.XamlRoot,
        Title = UiStrings.AppTitle,
        Content = e.Message,
        CloseButtonText = UiStrings.DialogClose,
    };
    if (Content is FrameworkElement root) dialog.RequestedTheme = root.ActualTheme;
    if (e.Severity == MergeResultSeverity.Success)
    {
        dialog.PrimaryButtonText = UiStrings.MergeSuccessOpenFolder;
        dialog.DefaultButton = ContentDialogButton.Primary;
        dialog.PrimaryButtonClick += async (dlg, clickArgs) =>
        {
            clickArgs.Cancel = true;                  // keep the dialog open (set before await)
            if (!await ViewModel.TryOpenLastFolderAsync())
                dlg.Content = UiStrings.FolderNotFound;
        };
    }
    await dialog.ShowAsync();
}
```

## Verification

**Commands:**
- `dotnet build vibepdf/vibepdf.csproj -p:Platform=x64 -r win-x64` -- expected: builds clean; no remaining references to the removed banner properties.
- `dotnet test vibepdf.Tests/vibepdf.Tests.csproj -p:Platform=x64 -r win-x64` -- expected: all tests pass.

**Manual checks:**
- F5: merge valid files → success dialog with "Open folder"; click it → Explorer opens at the output folder. Trigger a failure (e.g. read-only destination) → error dialog with "Close" only.

## Suggested Review Order

**View-model: signalling the outcome (start here)**

- Entry point — the view model now only raises an event; the View owns presentation.
  [`MainViewModel.cs:64`](../../vibepdf/ViewModels/MainViewModel.cs#L64)

- Success after the write replaces the old success banner.
  [`MainViewModel.cs:369`](../../vibepdf/ViewModels/MainViewModel.cs#L369)

- Merge failure raises Error (same in the catch-all at line 377).
  [`MainViewModel.cs:361`](../../vibepdf/ViewModels/MainViewModel.cs#L361)

- Thin helper that fires the event.
  [`MainViewModel.cs:403`](../../vibepdf/ViewModels/MainViewModel.cs#L403)

- Opens the last folder; returns false (folder gone) so the View shows MC-19 inline.
  [`MainViewModel.cs:408`](../../vibepdf/ViewModels/MainViewModel.cs#L408)

**View: the modal dialog**

- Builds + shows the modal ContentDialog; app-name title keeps success/error consistent.
  [`MainWindow.xaml.cs:63`](../../vibepdf/MainWindow.xaml.cs#L63)

- "Open folder" cancels the close (before the await) so the dialog stays open.
  [`MainWindow.xaml.cs:83`](../../vibepdf/MainWindow.xaml.cs#L83)

- Handler wired up at construction.
  [`MainWindow.xaml.cs:38`](../../vibepdf/MainWindow.xaml.cs#L38)

- Both merge InfoBars removed; only the progress bar remains in row 1.
  [`MainWindow.xaml:149`](../../vibepdf/MainWindow.xaml#L149)

**Supporting types, strings & tests**

- Severity + message carried from view model to View (pre-existing untracked file).
  [`MergeResultEventArgs.cs:11`](../../vibepdf/ViewModels/MergeResultEventArgs.cs#L11)

- New dialog dismiss-button label.
  [`UiStrings.cs:73`](../../vibepdf/Strings/UiStrings.cs#L73)

- No-op path now asserts no result dialog is raised (banner tests deleted).
  [`MainViewModelTests.cs:379`](../../vibepdf.Tests/ViewModels/MainViewModelTests.cs#L379)

- Cancel path likewise asserts no dialog.
  [`MainViewModelTests.cs:326`](../../vibepdf.Tests/ViewModels/MainViewModelTests.cs#L326)

## Review Findings

_Code review 2026-06-27 — adversarial (Blind Hunter + Edge Case Hunter + Acceptance Auditor). 0 decision-needed · 2 patch · 4 defer · 5 dismissed._

**Patch (resolved 2026-06-27):**

- [x] [Review][Patch] `LaunchFolderAsync` can throw (TOCTOU folder-deleted-after-`Directory.Exists`, access-denied, UNC) → propagates through `TryOpenLastFolderAsync` into the `async void` Open-folder lambda → unobserved UI-thread exception → **app crash** instead of the spec'd inline "Folder not found". Violates the I/O matrix "Open folder, folder gone → returns false → inline MC-19" contract. [vibepdf/Services/FolderLauncher.cs:12] — FIXED: wrapped the `GetFolderFromPathAsync`/`LaunchFolderAsync` calls in `try/catch` returning `false`.
- [x] [Review][Patch] Misleading comments: the `MergeCompleted` decl comment ("…or Open-folder fails") and the `OnMergeCompleted` comment ("the Open-folder-failure case is Error severity") both describe a path that does not exist — open-folder failure never raises the event; it returns `false` and the View patches `Content` inline. [vibepdf/ViewModels/MainViewModel.cs:62] [vibepdf/MainWindow.xaml.cs:60] — FIXED: both comments corrected.

**Deferred:**

- [x] [Review][Defer] No guard against a second concurrent `ContentDialog` (`async void` + unguarded `ShowAsync` throws `COMException`) — not reachable now (merge lock + modal block re-entry); becomes reachable with story 2.3's close-guard dialog. [vibepdf/MainWindow.xaml.cs:91] — deferred to 2.3
- [x] [Review][Defer] `MergeCompleted += OnMergeCompleted` is never unsubscribed — benign for a single-window app with a singleton VM. [vibepdf/MainWindow.xaml.cs:38] — deferred, benign today
- [x] [Review][Defer] Repeated "Open folder" clicks (dialog stays open, button enabled) launch concurrent `LaunchFolderAsync` / multiple Explorer windows. [vibepdf/MainWindow.xaml.cs:83] — deferred, minor UX
- [x] [Review][Defer] Spec Execution checklist says PrimaryButtonClick "takes a deferral"; the 2026-06-27 amendment, Design Notes, and the implementation all use no deferral — stale checklist text. [spec-merge-result-dialog.md:53] — deferred, doc reconciliation

**Dismissed (5):** dangling banner bindings (grep-verified: zero remaining refs); `Content.XamlRoot` dereferenced before the `FrameworkElement` guard (Content always set post-load — not reachable); setting `Content` on a closing dialog (benign, no throw); ConfigureAwait/threading (Edge Hunter verified the handler is UI-thread-affine); `MergeCompleted` naming covering errors (acceptable — a merge "completes" with success or error).
