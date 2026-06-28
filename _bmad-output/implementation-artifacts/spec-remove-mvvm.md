---
title: 'Remove CommunityToolkit.Mvvm — collapse MainViewModel into MainWindow code-behind'
type: 'refactor'
created: '2026-06-27'
status: 'done'
baseline_commit: 'a69551f228f2d82b07c924c6c9dd56fd52dcb677'
context:
  - '{project-root}/_bmad-output/specs/spec-remove-mvvm/brownfield.md'
  - '{project-root}/_bmad-output/specs/spec-remove-mvvm/SPEC.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The app is built on `CommunityToolkit.Mvvm` (source-generated `[ObservableProperty]`/`[RelayCommand]`, a `MainViewModel`, an `x:Bind ViewModel.*`-driven view). The maintainer wants the dependency *and* the MVVM pattern gone — no View/ViewModel split, no command objects, no INPC-driven bindings.

**Approach:** Delete `MainViewModel` and the `ViewModels/` folder; fold all state and logic into `MainWindow.xaml.cs`; rewrite `MainWindow.xaml` to named controls driven by event handlers; make `PdfFileItem` a plain class refreshed imperatively. Behavior-preserving — zero user-visible change.

## Boundaries & Constraints

**Always:**
- Behavior/UX parity exactly per `brownfield.md` §"Behavior-parity checklist". Reproduce every invariant in §"Concurrency behaviors to preserve" 1:1 — single in-flight preview render + staleness guard, 3-permit validation semaphore, 5s validation timeout, late-completion drop on remove, per-merge CTS + `Progress<double>` title percentage, `DispatcherQueue` UI marshalling, case-insensitive add dedupe, selection-shift on remove.
- Keep `Microsoft.Extensions.DependencyInjection`, the `Services/*` interface layer, and the `Models/*` types (`ValidationStatus`, `PreviewState`, `MergeOutcome`, `StatusBarState`). Services still resolve from `App.Current.Services`.
- List rows refresh imperatively: on validation completion locate the row via `FileListView.ContainerFromItem(item)` and update its `TextBlock`s directly; no-op safely when the container is null (virtualized/unrealized row). The status `x:Bind` drops to `OneTime`.
- `ObservableCollection<PdfFileItem>` may stay as the `ListView` backing store (BCL, not MVVM).
- Build/test entry points unchanged (`-p:Platform=x64 -r win-x64`).

**Ask First:**
- Any user-visible behavior, layout, styling, or `UiStrings` change (none expected — surface it, do not silently change).
- Any concurrency invariant that cannot be reproduced exactly in code-behind.

**Never:**
- No `CommunityToolkit.Mvvm` `PackageReference` and no `using CommunityToolkit.Mvvm.*` anywhere.
- No MVVM pattern: no ViewModel class, no `ICommand`/`RelayCommand`, no `INotifyPropertyChanged`-driven view, no `{x:Bind ViewModel.*}`. (This rules out hand-rolling INPC + ICommand and keeping a VM — the pattern goes, not just the package.)
- No change to validation/preview/merge service logic or PDFsharp usage.
- Do not port `MainViewModelTests` — delete it.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Behavior | Error Handling |
|----------|--------------|-------------------|----------------|
| Stale preview drop | Select slow-rendering file A, then file B before A finishes | A's render is discarded; B's preview/placeholder shows | cancel+dispose `_previewCts`; `cts.IsCancellationRequested \|\| !ReferenceEquals(item, selectedFile)` guard |
| Selected-file status change | Select a file while "Checking…", validation later resolves Valid | Preview renders for the now-Valid selected file | validation-completion path re-triggers preview when `item == selectedFile` (no INPC) |
| Validation timeout | A file validates >5s | Row flips to `ErrorTimeout`; work cancelled | `Task.WhenAny` timeout; cancel item CTS |
| Remove mid-validation | Remove a file still "Checking…" | In-flight validation cancelled; no status written back | cancel item CTS; `Files.Contains(item)` guard before every status write |
| Cancel Save dialog | Click Merge, cancel the Save picker | Silent no-op; no UI lock, no dialog | `return` before `IsMerging`/title `0%` |
| Virtualized row refresh | Validation completes for an unrealized row | Refresh no-ops; no crash | null `ContainerFromItem` guard |

</frozen-after-approval>

## Code Map

- `vibepdf/ViewModels/MainViewModel.cs` — **DELETE.** All state/logic absorbed into the code-behind.
- `vibepdf/ViewModels/MergeResultEventArgs.cs` — **DELETE.** Merge result shown inline in the Merge handler.
- `vibepdf/MainWindow.xaml.cs` — **REWRITE.** Owns fields, handlers, and helper methods; keeps existing static helpers (`BoolToVisibility(Inverse)`, `FormatStatus`, `StatusForeground`) and all title-bar/splitter/Win32 chrome unchanged.
- `vibepdf/MainWindow.xaml` — **REWRITE.** `{x:Bind ViewModel.*}`/`Command=` → `x:Name`d controls + event handlers; status TextBlock → `OneTime`.
- `vibepdf/Models/PdfFileItem.cs` — De-`ObservableObject`; `Status`/`PageCount` become plain auto-properties.
- `vibepdf/App.xaml.cs` — Remove the `MainViewModel` registration; keep all service registrations + the provider.
- `vibepdf/vibepdf.csproj` — Remove the `CommunityToolkit.Mvvm` `PackageReference`.
- `vibepdf/Converters/BoolToVisibilityConverter.cs` — **DELETE** (already unreferenced; XAML uses the static helpers).
- `vibepdf.Tests/ViewModels/MainViewModelTests.cs` — **DELETE** (regression net = service tests + F5 walkthrough).
- `_bmad-output/specs/spec-remove-mvvm/brownfield.md` — authoritative construct→replacement map, concurrency invariants, parity checklist.

## Tasks & Acceptance

**Execution (dependency order):**
- [x] `vibepdf/Models/PdfFileItem.cs` — Drop `ObservableObject` base + `[ObservableProperty]`; make `Status`/`PageCount` plain auto-properties — plain model, no INPC.
- [x] `vibepdf/MainWindow.xaml.cs` — Absorb all `MainViewModel` state (fields) and logic (handlers + helper methods); reproduce every concurrency invariant (`brownfield.md` §Concurrency); imperative row refresh via `ContainerFromItem` with null-safe no-op; drive the selected-file preview from the validation-completion path; resolve services from `App.Current.Services`; show the merge-result `ContentDialog` inline — code-behind owns everything.
- [x] `vibepdf/MainWindow.xaml` — Replace every `{x:Bind ViewModel.*}` and `Command=` with `x:Name`d controls wired to `Click`/`SelectionChanged` handlers; set `ItemsSource`/`SelectedItem`; name the status `TextBlock` and drop it to `OneTime`; keep title-bar/splitter/preview layout — binding-free view.
- [x] `vibepdf/App.xaml.cs` — Remove `services.AddSingleton<MainViewModel>()`; keep all other registrations — DI of services stays.
- [x] `vibepdf/vibepdf.csproj` — Remove the `CommunityToolkit.Mvvm` `PackageReference` — dependency gone.
- [x] `vibepdf/ViewModels/MainViewModel.cs`, `vibepdf/ViewModels/MergeResultEventArgs.cs` (+ the `ViewModels/` folder), `vibepdf/Converters/BoolToVisibilityConverter.cs`, `vibepdf.Tests/ViewModels/MainViewModelTests.cs` — **DELETE** — remove the pattern, dead code, and VM tests.

**Acceptance Criteria:**
- Given the refactored solution, when `dotnet build vibepdf/vibepdf.csproj -p:Platform=x64 -r win-x64` runs, then it succeeds with no `CommunityToolkit.Mvvm` `PackageReference` and no `using CommunityToolkit.Mvvm.*` anywhere.
- Given the codebase, when searched, then no `MainViewModel`, no `ViewModels/` folder, no `ICommand`/`RelayCommand`, no INPC-driven binding, and no `{x:Bind ViewModel.*}` remain.
- Given an F5 run, when the full add → validate → preview → reorder → remove → merge → progress-in-title → result-dialog → open-folder walkthrough is performed, then every item in `brownfield.md` §"Behavior-parity checklist" behaves identically to today.
- Given `dotnet test vibepdf.Tests/vibepdf.Tests.csproj -p:Platform=x64 -r win-x64`, when run, then `PdfValidationServiceTests` + `PdfSharpMergeServiceTests` stay green and the suite compiles without `MainViewModelTests`.

## Design Notes

- **State translation.** Each former `[ObservableProperty]` becomes a private field plus a small setter-method that recomputes derived UI and pushes to named controls (e.g. `SetSelectedFile` updates `RemoveButton.IsEnabled` and kicks the preview render). Computed members (`CanMerge`, `MergeDisabledReason`, `Title`, `ShowPreview*`, `PreviewPlaceholderText`) become helper methods called at each originating change to refresh the Merge button enabled state, its tooltip, the title-bar text, and the preview card/placeholder.
- **Lost INPC, replaced imperatively.** Today `OnFilePropertyChanged` re-renders the preview when the *selected* file's `Status` changes and re-evaluates merge state when *any* item's status changes. With `PdfFileItem` no longer notifying, both must be driven from the validation-completion path: after writing the item's status, refresh its row, re-evaluate merge state, and if it is the selected file, re-render the preview.
- **Imperative row refresh (golden example).** Name the status `TextBlock` in the `DataTemplate` (`x:Name="StatusText"`, `OneTime` for the initial "Checking…"), then update it via the realized container:

```csharp
void RefreshRow(PdfFileItem item)
{
    if (FileListView.ContainerFromItem(item) is not ListViewItem c) return; // virtualized → no-op
    if (c.ContentTemplateRoot is FrameworkElement root
        && root.FindName("StatusText") is TextBlock t)
    {
        t.Text = FormatStatus(item.Status, item.PageCount);
        t.Foreground = StatusForeground(item.Status);
    }
}
```

- **Files collection.** Keep the `ObservableCollection`; re-subscribe its `CollectionChanged` in code-behind to toggle the empty-placeholder visibility and re-evaluate merge state. No per-item subscriptions are needed anymore (items don't notify).

## Verification

**Commands:**
- `dotnet build vibepdf/vibepdf.csproj -p:Platform=x64 -r win-x64` — expected: build succeeds.
- `dotnet test vibepdf.Tests/vibepdf.Tests.csproj -p:Platform=x64 -r win-x64` — expected: service-layer tests green; suite compiles without `MainViewModelTests`.
- `rg -in "CommunityToolkit|MainViewModel|RelayCommand|ObservableProperty|x:Bind ViewModel" vibepdf` — expected: no matches.

**Manual checks:**
- F5 walkthrough of `brownfield.md` §"Behavior-parity checklist" — every item behaves identically, with focus on the six race/edge scenarios in the I/O matrix above.

## Suggested Review Order

**Design intent (start here)**

- The window now owns all former view-model state in plain fields — the whole design pivot.
  [`MainWindow.xaml.cs:31`](../../vibepdf/MainWindow.xaml.cs#L31)
- Constructor resolves services from DI and seeds the initial UI state imperatively.
  [`MainWindow.xaml.cs:56`](../../vibepdf/MainWindow.xaml.cs#L56)

**Lost INPC, re-driven imperatively (most interesting)**

- Validation-completion path replaces `PropertyChanged`: refresh row, re-gate merge, re-preview if selected.
  [`MainWindow.xaml.cs:161`](../../vibepdf/MainWindow.xaml.cs#L161)
- Imperative row refresh via the realized container; null-safe no-op when virtualized.
  [`MainWindow.xaml.cs:171`](../../vibepdf/MainWindow.xaml.cs#L171)

**Concurrency invariants preserved (highest risk)**

- Validation pipeline: 3-permit semaphore, 5s timeout, late-completion drop guard.
  [`MainWindow.xaml.cs:191`](../../vibepdf/MainWindow.xaml.cs#L191)
- Single in-flight preview render with the `ReferenceEquals` staleness guard.
  [`MainWindow.xaml.cs:248`](../../vibepdf/MainWindow.xaml.cs#L248)
- Merge flow: per-merge CTS, progress→title, lock-after-confirm, fire-and-forget result dialog.
  [`MainWindow.xaml.cs:349`](../../vibepdf/MainWindow.xaml.cs#L349)
- Cross-thread state writes marshalled onto the UI dispatcher.
  [`MainWindow.xaml.cs:472`](../../vibepdf/MainWindow.xaml.cs#L472)

**State translation (ObservableProperties → setter methods)**

- Selection setter guards reorder, toggles Remove, kicks the preview render.
  [`MainWindow.xaml.cs:117`](../../vibepdf/MainWindow.xaml.cs#L117)
- Merge gating computed on demand (enabled state + disabled-reason tooltip).
  [`MainWindow.xaml.cs:321`](../../vibepdf/MainWindow.xaml.cs#L321)
- Merge UI-lock setter toggles Add/Remove/reorder and the title.
  [`MainWindow.xaml.cs:398`](../../vibepdf/MainWindow.xaml.cs#L398)
- Preview state pushed to the card/placeholder controls.
  [`MainWindow.xaml.cs:302`](../../vibepdf/MainWindow.xaml.cs#L302)

**Binding-free view**

- Named ListView + Click/SelectionChanged handlers replace every `x:Bind ViewModel.*`.
  [`MainWindow.xaml:75`](../../vibepdf/MainWindow.xaml#L75)
- Status `TextBlock` dropped to `OneTime` for the initial "Checking…"; refreshed imperatively after.
  [`MainWindow.xaml:89`](../../vibepdf/MainWindow.xaml#L89)
- Tooltip host Border stays hit-test-visible so the disabled-Merge tooltip renders.
  [`MainWindow.xaml:185`](../../vibepdf/MainWindow.xaml#L185)

**Model, DI & dependency removal (peripherals)**

- `PdfFileItem` is now a plain class — no `ObservableObject`, no notifications.
  [`PdfFileItem.cs:3`](../../vibepdf/Models/PdfFileItem.cs#L3)
- `MainViewModel` registration removed; all service registrations kept.
  [`App.xaml.cs:30`](../../vibepdf/App.xaml.cs#L30)
- `CommunityToolkit.Mvvm` `PackageReference` removed.
  [`vibepdf.csproj:43`](../../vibepdf/vibepdf.csproj#L43)
