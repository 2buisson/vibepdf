---
title: 'Show merge progress in the title bar'
type: 'feature'
created: '2026-06-27'
status: 'done'
context: []
baseline_commit: '62bf6eff1742b780a80f960574976195015af95b'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Merge progress is surfaced by a determinate `ProgressBar` sitting above the action bar. The user wants that bar gone and the progress shown as text in the custom title bar instead.

**Approach:** Remove the `ProgressBar` and drive the in-window title-bar text from the view model: `"Vibe PDF — 55%"` while a merge is in progress (shown immediately, no delay), `"Vibe PDF"` otherwise. Leave the OS/taskbar window title unchanged.

## Boundaries & Constraints

**Always:**
- Show the percentage immediately while a merge runs — the title gates on `IsMerging`; no reveal delay.
- Centralize the title format string in `UiStrings`.
- Percentage is the rounded integer of `MergeProgress` (0–100), e.g. `33.33 → 33%`.

**Ask First:**
- Any change to the merge service or progress reporting cadence.

**Never:**
- Do not change the OS window title / taskbar / Alt-Tab entry (`Window.Title`, `MainWindow.xaml:12`) — it stays `"Vibe PDF"`.
- Do not add a replacement progress visual; the title text is the only progress surface.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| No merge running | `IsMerging = false` | Title = `"Vibe PDF"` | N/A |
| Merge running | `IsMerging = true`, `MergeProgress = 55` | Title = `"Vibe PDF — 55%"` | N/A |
| Merge just started | `IsMerging = true`, `MergeProgress = 0` | Title = `"Vibe PDF — 0%"` | N/A |
| Fractional progress | `IsMerging = true`, `MergeProgress = 33.33` | Title = `"Vibe PDF — 33%"` | N/A |
| Merge finished/cancelled | `IsMerging = false` (finally block) | Title reverts to `"Vibe PDF"` | N/A |

</frozen-after-approval>

## Code Map

- `vibepdf/MainWindow.xaml` — preview panel grid holds the `ProgressBar` (row 1) to remove; title-bar `TextBlock` binds `strings:UiStrings.AppTitle` and must rebind to the view model.
- `vibepdf/ViewModels/MainViewModel.cs` — `MergeProgress` (double) and `IsMerging` (bool) already exist; `IsMerging` is set true at merge start and false in the `finally`. Add the computed title gating on `IsMerging`; delete the now-redundant `IsProgressVisible` property and `StartProgressDelay()` (the old 2s delay).
- `vibepdf/Strings/UiStrings.cs` — `AppTitle = "Vibe PDF"`; add the merge title format.
- `vibepdf.Tests/ViewModels/MainViewModelTests.cs` — xUnit, `CreateViewModel()` helper; add title-state tests.

## Tasks & Acceptance

**Execution:**
- [x] `vibepdf/Strings/UiStrings.cs` -- add `public const string AppTitleMergeProgress = "{0} — {1:0}%";` (em dash, rounded percent) next to `AppTitle` -- keeps user-facing strings centralized.
- [x] `vibepdf/ViewModels/MainViewModel.cs` -- add computed `public string Title => IsMerging ? string.Format(UiStrings.AppTitleMergeProgress, UiStrings.AppTitle, MergeProgress) : UiStrings.AppTitle;`; add `[NotifyPropertyChangedFor(nameof(Title))]` to `MergeProgress` and `IsMerging`; delete `IsProgressVisible` and `StartProgressDelay()`; reset `MergeProgress = 0` before setting `IsMerging = true` so the title opens at `0%`.
- [x] `vibepdf/MainWindow.xaml` -- delete the row-1 `StackPanel`+`ProgressBar`, delete the now-unused row-1 `RowDefinition`, renumber the action-bar `Grid` from `Grid.Row="2"` to `Grid.Row="1"`, and update the preview-panel comments. Rebind the title `TextBlock.Text` to `{x:Bind ViewModel.Title, Mode=OneWay}` -- live-updating title, no bar.
- [x] `vibepdf.Tests/ViewModels/MainViewModelTests.cs` -- unit-test the matrix states: default → `"Vibe PDF"`; `IsMerging=true, MergeProgress=55` → `"Vibe PDF — 55%"`; fractional `33.33` → `"Vibe PDF — 33%"`; `IsMerging=false` regardless of `MergeProgress` → `"Vibe PDF"`.

**Acceptance Criteria:**
- Given a merge is in progress, when `MergeProgress` updates, then the title bar shows the live rounded percentage immediately (no reveal delay).
- Given the build output, when the app compiles, then no `ProgressBar` remains in the preview panel and the action bar still renders at the bottom of the preview column.
- Given any merge state, when the title changes, then the OS taskbar / Alt-Tab entry still reads `"Vibe PDF"`.

## Spec Change Log

- **2026-06-27 — Human renegotiated the reveal behavior (during review).** Original frozen intent kept the 2-second reveal delay (title stayed `"Vibe PDF"` for sub-2s merges). The human instead chose to **remove the delay and show the percentage immediately**, then verify the race the Edge-Case Hunter flagged was gone. Amended: Approach, Boundaries (Always/Ask-First), I/O matrix, Code Map, Tasks. **Known-bad avoided:** the Edge-Case Hunter found a race where `StartProgressDelay`'s `Task.Delay(2s).ContinueWith(RunOnUI(IsProgressVisible = true))` could fire *after* the merge's `finally`, leaving the title stuck on `"Vibe PDF — 100%"`. **KEEP:** deleting `StartProgressDelay` + `IsProgressVisible` and gating `Title` solely on `IsMerging` (UI-thread-only writes) eliminates the race structurally — no background continuation writes title state. Reset `MergeProgress = 0` before `IsMerging = true` so the title opens at `0%` rather than a stale value.

## Design Notes

The `strings:` xmlns stays — it's still used for `EmptyFileListPlaceholder`. `Title` gates on the existing `IsMerging` flag (no new state); since `IsMerging` is written only on the UI thread (true at start, false in `finally`) and `MergeProgress` is marshaled to the UI thread via `Progress<double>`, there is no background writer and thus no stuck-title race. The `{1:0}` format token rounds `MergeProgress` to an integer.

## Verification

**Commands:**
- `dotnet build vibepdf/vibepdf.csproj -p:Platform=x64 -r win-x64` -- expected: build succeeds, no XAML binding errors.
- `dotnet test vibepdf.Tests/vibepdf.Tests.csproj -p:Platform=x64 -r win-x64` -- expected: all tests pass, including the new `Title` tests.

## Suggested Review Order

**Title logic (start here)**

- The whole feature: title gates on `IsMerging`, formats the live percentage, else app name.
  [`MainViewModel.cs:63`](../../vibepdf/ViewModels/MainViewModel.cs#L63)

- Merge flow — `MergeProgress = 0` set before `IsMerging = true` so the title opens at `0%`; `finally` reverts via `IsMerging = false`. The old `StartProgressDelay` (the race's source) is deleted here.
  [`MainViewModel.cs:354`](../../vibepdf/ViewModels/MainViewModel.cs#L354)

- Centralized format string (em dash, `{1:0}` rounds the percent).
  [`UiStrings.cs:9`](../../vibepdf/Strings/UiStrings.cs#L9)

**UI binding & layout**

- Title bar rebound from the static `AppTitle` to the live `ViewModel.Title`.
  [`MainWindow.xaml:206`](../../vibepdf/MainWindow.xaml#L206)

- Preview grid collapsed from 3 rows to 2 after removing the `ProgressBar`.
  [`MainWindow.xaml:140`](../../vibepdf/MainWindow.xaml#L140)

- Action bar renumbered to `Grid.Row="1"`, still bottom-pinned by the star row.
  [`MainWindow.xaml:176`](../../vibepdf/MainWindow.xaml#L176)

**Tests**

- Four title-state tests covering the I/O matrix.
  [`MainViewModelTests.cs:53`](../../vibepdf.Tests/ViewModels/MainViewModelTests.cs#L53)
