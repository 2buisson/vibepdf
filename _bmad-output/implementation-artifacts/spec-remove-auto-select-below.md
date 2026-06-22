---
title: 'Auto-select the file below after removing a file'
type: 'feature'
created: '2026-06-22'
status: 'done'
route: 'plan-code-review'
baseline_commit: 'ae1a84f16e2b84c8fc839008658a4d5810f1ec5b'
---

# Auto-select the file below after removing a file

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Removing the selected PDF clears the selection entirely (`Remove()` sets `SelectedFile = null`). The preview empties and the user must manually click another row to keep going — there's no continuity when pruning a list.

**Approach:** After a remove, select the file that slides up into the removed slot (the row directly **below** the removed one). If the removed file was the last row, there is no file below, so leave nothing selected. Make the ListView's visual highlight follow the view-model by two-way binding `SelectedItem` to `ViewModel.SelectedFile`, replacing the one-way code-behind `SelectionChanged` handler so VM-driven selection is reflected on screen.

## Boundaries & Constraints

**Always:**
- Selection target after remove = the item now occupying the removed item's former index (its former "below" neighbor).
- Removing the **last** row (nothing below) clears selection; removing the **only** row leaves the list empty with no selection.
- `SelectedFile` stays the single source of truth — preview rendering and merge-gating keep reacting to it with no behavior change.
- Preserve the existing in-flight-validation cancel on remove.

**Ask First:**
- Selecting the row **above** when removing the last row (chosen behavior is "select nothing" — changing it is a human renegotiation of intent).

**Never:**
- Don't add a second selection field or duplicate selection state in code-behind.
- Don't touch drag-reorder, validation, merge, or add-file logic.
- Don't auto-change selection on add or on a validation status change.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Remove first/middle | Files `[A,B,C]`, selected `B` (idx 1) | `B` removed; `C` shifts to idx 1 and becomes selected; preview renders `C` | N/A |
| Remove last row | Files `[A,B]`, selected `B` (idx 1, last) | `B` removed; no row below → selection cleared; preview empties | N/A |
| Remove only row | Files `[A]`, selected `A` | `A` removed; list empty; no selection; empty placeholder | N/A |
| Remove during validation | selected item still `Checking` | item removed; in-flight validation cancelled; below-neighbor selected | late completion still dropped |

</frozen-after-approval>

## Code Map

- `pdfjunior/ViewModels/MainViewModel.cs` (`Remove()`, ~L447-460) — capture the removed item's index before `Files.Remove`, then set `SelectedFile` to the below-neighbor or `null`.
- `pdfjunior/MainWindow.xaml` (ListView, ~L79-87) — bind `SelectedItem="{x:Bind ViewModel.SelectedFile, Mode=TwoWay}"`; remove the `SelectionChanged` attribute.
- `pdfjunior/MainWindow.xaml.cs` (`FileListView_SelectionChanged`, ~L84-87) — delete; the TwoWay binding replaces it.
- `pdfjunior.Tests/ViewModels/MainViewModelTests.cs` — update the remove-clears-selection test to the new below-neighbor behavior; add last-row and middle-row cases.

## Tasks & Acceptance

**Execution:**
- [x] `pdfjunior/ViewModels/MainViewModel.cs` -- in `Remove()`, read `var index = Files.IndexOf(item)` before removal, then after `Files.Remove(item)` set `SelectedFile = index < Files.Count ? Files[index] : null` -- selects the shifted-up below-neighbor, clears when the removed row was last/only.
- [x] `pdfjunior/MainWindow.xaml` -- add `SelectedItem="{x:Bind ViewModel.SelectedFile, Mode=TwoWay}"` to the file ListView and drop `SelectionChanged="FileListView_SelectionChanged"` -- lets VM-driven selection move the on-screen highlight while preserving click-to-select.
- [x] `pdfjunior/MainWindow.xaml.cs` -- delete the now-unused `FileListView_SelectionChanged` handler -- redundant once selection is two-way bound.
- [x] `pdfjunior.Tests/ViewModels/MainViewModelTests.cs` -- replace `Remove_SelectedFile_RemovedAndSelectionCleared` with a below-neighbor assertion (`Remove_FirstSelectedFile_SelectsFileBelow`); add `Remove_MiddleSelectedFile_SelectsFileBelow` and `Remove_LastSelectedFile_ClearsSelection`.

**Acceptance Criteria:**
- Given files `[A,B,C]` with `B` selected, when Remove runs, then `B` is gone and `C` (now at `B`'s old index) is `SelectedFile`.
- Given files `[A,B]` with the last row `B` selected, when Remove runs, then `B` is gone and `SelectedFile` is `null`.
- Given a single file `A` selected, when Remove runs, then the list is empty and `SelectedFile` is `null`.
- Given the new selection is a Valid file, when Remove auto-selects it, then the preview re-renders for that file (existing `OnSelectedFileChanged` path).
- Given click-to-select on a row, when the user clicks, then `SelectedFile` still updates via the TwoWay binding (no regression from removing the code-behind handler).

## Design Notes

The list mutates via `ObservableCollection`, so after `Files.Remove(item)` every row below the removed index shifts up by one — the "file below" is therefore at the **same index** the removed item held. `index < Files.Count` is true exactly when a below-neighbor existed; `index == Files.Count` means the removed row was last → clear. Today selection only flows View→VM through the code-behind handler, so the VM cannot move the highlight; the TwoWay `SelectedItem` binding closes that loop (the removed item leaving the collection no longer matters because we immediately re-point `SelectedItem` at a still-present item).

## Verification

**Commands:**
- `dotnet build pdfjunior.Tests/pdfjunior.Tests.csproj -p:Platform=x64 -r win-x64` -- expected: 0 warnings, 0 errors.
- `pdfjunior.Tests/bin/x64/Debug/net10.0-windows10.0.22621.0/win-x64/pdfjunior.Tests.exe` -- expected: all tests pass, including the updated/new remove cases.

**Manual checks (MSIX app, VS F5 — no CLI launch):**
- Add 3+ PDFs, select a middle row, click Remove → the row below is highlighted and its preview shows.
- Select the last row, click Remove → no row highlighted, preview empty.
- Click rows directly → selection + preview still track the click.

## Suggested Review Order

**Selection-after-remove logic**

- Entry point — captures the index before removal so the below-neighbor can be re-selected.
  [`MainViewModel.cs:453`](../../pdfjunior/ViewModels/MainViewModel.cs#L453)

- Core rule — after removal the below-neighbor sits at the same index; `index == Count` means last row → clear.
  [`MainViewModel.cs:465`](../../pdfjunior/ViewModels/MainViewModel.cs#L465)

**Selection ↔ UI binding**

- TwoWay `SelectedItem` binding lets the VM-driven selection move the on-screen highlight.
  [`MainWindow.xaml:83`](../../pdfjunior/MainWindow.xaml#L83)

- Code-behind `FileListView_SelectionChanged` handler deleted — replaced by the TwoWay binding.
  [`MainWindow.xaml.cs:84`](../../pdfjunior/MainWindow.xaml.cs#L84)

**Tests**

- Below-neighbor selection for first- and middle-row removal; last-row clears selection.
  [`MainViewModelTests.cs:432`](../../pdfjunior.Tests/ViewModels/MainViewModelTests.cs#L432)
