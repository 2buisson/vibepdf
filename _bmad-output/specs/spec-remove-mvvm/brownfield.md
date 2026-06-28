# Brownfield notes — CommunityToolkit.Mvvm removal

Current-state reference for SPEC-remove-mvvm. The toolkit footprint is three source files plus the XAML; the risk is not the surface area but the concurrency logic and the behavior edge-cases the rewrite must reproduce.

## Construct → imperative replacement map

| Current (toolkit) | Location | Replacement (code-behind) |
| --- | --- | --- |
| `: ObservableObject` base | `MainViewModel`, `PdfFileItem` | None. `MainWindow` holds state in fields; `PdfFileItem` becomes a plain class with no `INotifyPropertyChanged` (see the `Status`/`PageCount` row). |
| `[ObservableProperty]` `SelectedFile`, `HasFiles`, `Preview`, `PreviewImage`, `IsMerging`, `MergeProgress` | `MainViewModel` | Private fields in `MainWindow`; each setter is a method that recomputes and pushes to the named control(s). |
| `[ObservableProperty]` `Status`, `PageCount` | `PdfFileItem` | Plain properties, no notification. The row is refreshed imperatively: when validation completes, code-behind calls `FileListView.ContainerFromItem(item)` and updates the row's `TextBlock`s (status text + critical-color brush) directly. The DataTemplate's status `x:Bind` drops to `OneTime` for the initial "Checking…" render. **Caveat:** `ContainerFromItem` returns `null` for a not-yet-realized (virtualized) row — for a small file list this is rarely hit, but the refresh must no-op safely when the container is null and re-apply on realization if it matters. |
| `[RelayCommand]` `AddFiles`, `Merge`, `Remove` (+ `CanExecute`) | `MainViewModel` | `AppBarButton`/`Button` `Click` handlers; enabled state set via `control.IsEnabled`. |
| `[NotifyPropertyChangedFor(...)]` driving `ShowPreviewImage`, `ShowPreviewPlaceholder`, `PreviewPlaceholderText`, `Title` | `MainViewModel` | Compute and set control `Visibility`/`Text` imperatively at each originating state change. |
| `[NotifyCanExecuteChangedFor(RemoveCommand)]` on `SelectedFile` | `MainViewModel` | Set `RemoveButton.IsEnabled` in the `ListView.SelectionChanged` handler. |
| Computed `CanMerge`, `MergeDisabledReason`, `Title`, `ShowPreview*`, `PreviewPlaceholderText` | `MainViewModel` | Helper methods in code-behind, called to refresh the affected controls (Merge button enabled, tooltip text, title bar text, preview card/placeholder). |
| `MergeCompleted` event + `MergeResultEventArgs` | `MainViewModel` → View | `Merge_Click` awaits the merge, then shows the `ContentDialog` inline. Event and `MergeResultEventArgs.cs` are deleted. |
| `ObservableCollection<PdfFileItem> Files` | `MainViewModel` | Moves to a `MainWindow` field (stays — BCL, not MVVM). `ListView.ItemsSource` set in code or bound to this field. |
| `Files.CollectionChanged` + per-item `PropertyChanged` subscription bookkeeping | `MainViewModel` | Re-implemented in code-behind: toggle empty-placeholder visibility, re-evaluate merge state, (re)subscribe item status changes for the selected-preview refresh. |
| `x:Bind ViewModel.*` (ItemsSource, SelectedItem, Command, Visibility, Title, ToolTip) | `MainWindow.xaml` | `x:Name`d controls wired through event handlers; no `ViewModel` property on the window. |

## Concurrency behaviors to preserve

These live in `MainViewModel` today and are the highest-risk part of the rewrite — reproduce each exactly:

- **Single in-flight preview render.** `_previewCts` is cancelled + disposed on every selection/status change; after `await RenderFirstPageAsync`, a `cts.IsCancellationRequested || !ReferenceEquals(item, SelectedFile)` guard drops a stale render of file A that lands after the user selected file B. A `Valid` file that fails to render falls back to the corrupt placeholder.
- **Validation pipeline.** 3-permit `SemaphoreSlim`; validation runs off-thread via `Task.Run`; a 5s timeout via `Task.WhenAny` flips the item to `ErrorTimeout` and cancels the work; a removed item's late completion is dropped (`Files.Contains(item)` guard before every status write); `ObjectDisposedException` on shutdown is swallowed; per-item `CancellationTokenSource` tracked in `_validationCts` and cancelled on remove.
- **UI-thread marshalling.** Validation completes on a worker thread but writes UI-bound state, so updates are marshalled via `DispatcherQueue.TryEnqueue` (`RunOnUI`). Code-behind has the same `DispatcherQueue` available; keep the marshalling.
- **Merge flow.** Per-merge `CancellationTokenSource`; `Progress<double>` captured on the UI sync context drives the title percentage; `IsMerging` lock engages only *after* the Save dialog is confirmed (cancelling Save is a silent no-op with no lock); title shows `0%` immediately; unlock happens in `finally`; the `Files` collection is preserved across a merge.
- **Remove.** Cancels any in-flight validation for the item, then applies selection-shift: the file below slides up into the removed slot; if the removed row was last, selection clears.
- **Dedupe.** Add skips paths already present, compared case-insensitively (`StringComparison.OrdinalIgnoreCase`).

## Behavior-parity checklist (F5 walkthrough)

The acceptance surface for CAP-3. Codes (MC-*) are the existing UX references in `MainViewModel`/EXPERIENCE.md.

- Add PDF(s) appends rows showing "Checking…", which resolve to "N page(s)" or an error status (password / corrupt / timeout) in the critical color.
- Empty-list placeholder and the file `ListView` toggle visibility on `HasFiles`.
- Selecting a valid file renders its first page in the 300px preview card; checking/password/corrupt/timeout selections show the matching placeholder text and no image.
- Drag-reorder preserves the selected item and the resulting merge order, and does **not** re-render the preview (no `SelectedFile` change).
- Remove selects the file below (or clears if last); Remove is disabled with no selection; a flagged file removes like any other; removing the only flagged file can re-enable Merge.
- Merge button: disabled-reason tooltips MC-10 (no/zero-valid files), MC-11 (valid + flagged present), MC-12 (still checking, outranks flagged); enabled only with ≥1 valid file and none checking or flagged.
- Merge: opens Save dialog pre-filled `merged.pdf`; cancelling Save is a silent no-op; on confirm the UI locks (Add/Remove/reorder disabled), the title bar shows live "Vibe PDF — N%", and the outcome appears in a modal `ContentDialog`.
- Result dialog: success offers "Open folder" as the primary button that does **not** dismiss the dialog; if the folder is gone it surfaces MC-19 inline; error shows the generic merge-error message.
- Chrome that is already code-behind and must keep working unchanged: custom title bar / Mica backdrop, sidebar `GridSplitter` drag, minimum window size (Win32 subclass), and title-bar theme sync on `ActualThemeChanged`.

## Files touched

- **Delete:** `ViewModels/MainViewModel.cs`, `ViewModels/MergeResultEventArgs.cs` (and the `ViewModels/` folder); `vibepdf.Tests/ViewModels/MainViewModelTests.cs` (deleted outright — regression net becomes the service-layer tests + the F5 walkthrough below); `Converters/BoolToVisibilityConverter.cs` (already unreferenced).
- **Rewrite:** `MainWindow.xaml` (drop all `ViewModel.*` bindings → named controls + handlers), `MainWindow.xaml.cs` (absorb all view-model state and logic), `Models/PdfFileItem.cs` (de-`ObservableObject`).
- **Edit:** `App.xaml.cs` (remove the `MainViewModel` registration; keep all service registrations and the `Services` provider), `vibepdf.csproj` (remove the `CommunityToolkit.Mvvm` `PackageReference`).
- **Unaffected:** everything under `Services/`, the `Models/*` enums and records, `Strings/UiStrings.cs`, and the existing service-layer tests.
