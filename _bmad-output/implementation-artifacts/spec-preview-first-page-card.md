---
title: 'Preview panel: first-page image in a card'
type: 'feature'
created: '2026-06-22'
baseline_commit: 0a892c7
status: 'done'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/1-4-preview-selected-file.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The preview pane embeds Edge's `WebView2` PDF viewer (the 2026-06-18 spike) — heavy, and it shows Edge's default multi-page layout/scroll/toolbar chrome instead of a controlled visual.

**Approach:** Replace the `WebView2` with a single static image of the **first page only**, rendered via `Windows.Data.Pdf`, centered inside a fixed **300px-wide card** (subtle background + 1px border + 8px rounded corners + a soft `ThemeShadow`). Every non-valid state keeps its existing centered text notice — only the `Ready` branch changes.

## Boundaries & Constraints

**Always:**
- Keep the `PreviewState` machine and each non-valid notice verbatim (MC-2/MC-7/MC-8/MC-9); only the `Ready` rendering changes.
- Render the first page **on the UI thread** (`BitmapImage` is a `DependencyObject`), mirroring `PdfValidationService`'s `Windows.Data.Pdf` + try/catch shape; no `Task.Run`, no `.Result`/`.Wait()`/`ConfigureAwait(false)`/`async void`.
- Single in-flight render via `_previewCts` (cancel+dispose+recreate each update); apply the result only past a `cts.IsCancellationRequested || !ReferenceEquals(item, SelectedFile)` guard. A `Valid` file that throws while rendering → `ExcludedCorrupt`.
- Trigger renders ONLY from `OnSelectedFileChanged` and the selected-file `Status` change in `OnFilePropertyChanged` — NEVER from `OnFilesCollectionChanged` (no reload on drag-reorder).
- House style: `[ObservableProperty]` partial props, compiled `{x:Bind}`, constructor injection, singleton in `App.ConfigureServices`. Card brushes via `ThemeResource` — no hardcoded hex.

**Ask First:** adding a NuGet package; showing more than the first page; changing the 300px width or any notice text.

**Never:** new NuGet packages (`Windows.Data.Pdf` + `BitmapImage` are in-box); any zoom/page-nav/scroll/annotation/edit affordance; constructing a `BitmapImage` off the UI thread or in tests; touching merge/validation/banner/file-list logic.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Behavior | Error Handling |
|----------|--------------|-------------------|----------------|
| No selection | `SelectedFile = null` | `Preview=None`; `PreviewImage=null`; MC-2; no render | N/A |
| Checking | status `Checking` | `Preview=Checking`; MC-7; render NOT called | N/A |
| Password | `ErrorPassword` | `Preview=ExcludedPassword`; MC-8; render NOT called | N/A |
| Corrupt/Timeout | `ErrorCorrupt`/`ErrorTimeout` | `Preview=ExcludedCorrupt`; MC-9; render NOT called | N/A |
| Valid | status `Valid` | `RenderFirstPageAsync(path)` once; image in card; `Preview=Ready` | render throws → `ExcludedCorrupt` |
| Checking → Valid | selected checking item resolves to `Valid` | render once; `Preview=Ready`, no re-click | throws → `ExcludedCorrupt` |
| Drag-reorder selected | `Files.Move`, `SelectedFile` ref unchanged | NO new render (`Received(1)` total); `Preview` unchanged | N/A |
| Superseded mid-render | select A (Valid, pending) then B before A completes | A's result dropped by guard; `Preview` reflects B | `OperationCanceledException` swallowed |
| Remove selected | Remove the selected Valid item | `Preview=None`; `PreviewImage=null`; `SelectedFile=null`; render cancelled | N/A |

</frozen-after-approval>

## Code Map

- `pdfjunior/Services/PdfValidationService.cs` -- pattern to mirror (`GetFileFromPathAsync` → `LoadFromFileAsync`, cancellation, try/catch).
- `pdfjunior/ViewModels/MainViewModel.cs` -- sync `UpdatePreview` sets `PreviewUri`/`ShowPreviewPages`; hooks `OnSelectedFileChanged`/`OnFilePropertyChanged`/`OnFilesCollectionChanged`; `RunOnUI`; 5-arg ctor.
- `pdfjunior/MainWindow.xaml` -- preview `Grid.Row="1"`: `WebView2` (`PreviewWebView`) + placeholder `TextBlock`; `local:MainWindow.BoolToVisibility`.
- `pdfjunior/MainWindow.xaml.cs` -- `InitializePreviewWebViewAsync()` + ctor call + `Microsoft.Web.WebView2.Core` using (to remove).
- `pdfjunior/App.xaml.cs` -- `ConfigureServices()` (singletons).
- `pdfjunior.Tests/ViewModels/MainViewModelTests.cs` -- `CreateViewModel()` factory + preview tests asserting `PreviewUri`.

## Tasks & Acceptance

**Execution:**
- [x] `pdfjunior/Services/IPdfPreviewService.cs` -- NEW: `Task<BitmapImage?> RenderFirstPageAsync(string path, CancellationToken ct);` (`Microsoft.UI.Xaml.Media.Imaging.BitmapImage`).
- [x] `pdfjunior/Services/PdfPreviewService.cs` -- NEW impl (see Design Notes sketch): load doc, guard `PageCount > 0` (else throw), render `GetPage(0)` to an `InMemoryRandomAccessStream` at `DestinationWidth = 600`, build the `BitmapImage` inline on the UI thread; `ct.ThrowIfCancellationRequested()`.
- [x] `pdfjunior/ViewModels/MainViewModel.cs` -- inject `IPdfPreviewService` (6th ctor param); rename `PreviewUri`→`PreviewImage` (`BitmapImage?`) and `ShowPreviewPages`→`ShowPreviewImage` (update `[NotifyPropertyChangedFor]`); convert `UpdatePreview`→`async Task UpdatePreviewAsync` (fire-and-forget `_ = UpdatePreviewAsync(..)` from hooks). Non-valid branches stay synchronous; `Valid` branch: cancel+recreate `_previewCts`, `await RenderFirstPageAsync(item.Path, cts.Token)`, apply past the staleness guard, set image + `Preview=Ready`; `try/catch (OperationCanceledException) {}` and `catch { Preview = ExcludedCorrupt; }`.
- [x] `pdfjunior/MainWindow.xaml` -- replace the `WebView2` with a centered `Border` card: `Width="300"`, `CornerRadius="8"`, `Background="{ThemeResource CardBackgroundFillColorDefaultBrush}"`, `BorderBrush="{ThemeResource CardStrokeColorDefaultBrush}"`, `BorderThickness="1"`, `Padding="8"`, `HorizontalAlignment`/`VerticalAlignment="Center"`, `Translation="0,0,32"` + `<Border.Shadow><ThemeShadow/></Border.Shadow>`, wrapping `<Image Source="{x:Bind ViewModel.PreviewImage, Mode=OneWay}" Stretch="Uniform"/>`; card `Visibility` bound to `ShowPreviewImage`. Leave the placeholder `TextBlock` + preview toolbar untouched.
- [x] `pdfjunior/MainWindow.xaml.cs` -- delete `InitializePreviewWebViewAsync()`, its ctor call, the spike comment, and the WebView2 using.
- [x] `pdfjunior/App.xaml.cs` -- `services.AddSingleton<IPdfPreviewService, PdfPreviewService>();`.
- [x] `pdfjunior.Tests/ViewModels/MainViewModelTests.cs` -- add a 6th `IPdfPreviewService` mock to `CreateViewModel()`; replace `PreviewUri` assertions with `Preview` + `ShowPreviewImage` + `_previewService.Received(1).RenderFirstPageAsync(item.Path, Arg.Any<CancellationToken>())`; reorder test asserts `Received(1)` **total**; add a stale-render supersession test (A via `TaskCompletionSource`, select A then a Checking B, complete A → `Preview==Checking`, render received once). Cover every Matrix row.

**Acceptance Criteria:**
- Given a valid file is selected, when its render completes, then the first page shows as one static image centered in a 300px card (rounded corners, 1px border, soft shadow), with no zoom/page-nav/scroll/edit affordances.
- Given any non-valid state, when the pane is viewed, then the existing centered notice (MC-2/MC-7/MC-8/MC-9) shows exactly as before, with no card.
- Given the changed pane, then no `Microsoft.Web.WebView2` usage remains in `MainWindow`.
- Given a clean `-p:Platform=x64 -r win-x64` build, then 0 warnings / 0 errors and the direct-exe test run is all green (≥43 tests).

## Spec Change Log

## Design Notes

Threading/cancellation mirror story 1-4's pre-webview render (same `_previewCts` + `ReferenceEquals` idiom as `_validationCts`). The render runs on the UI thread; `Windows.Data.Pdf` awaits resume on the WinUI sync context, so the `BitmapImage` is built on the UI thread without `Task.Run`.

Test accommodation: the mocked `RenderFirstPageAsync` returns `null` (NSubstitute's default `Task<BitmapImage?>`), so no `BitmapImage` is constructed in the test host. The VM sets `Preview=Ready` whenever the render returns without throwing — a `null` result (only the mock) still yields `Ready`; assertions key on `Preview`/`ShowPreviewImage`/`Received(..)`, never pixels (as 1-4 did with an empty list). `Valid` keeps the prior `Preview` value during the brief render, then flips to `Ready`.

```csharp
var file = await StorageFile.GetFileFromPathAsync(path);
ct.ThrowIfCancellationRequested();
var doc = await PdfDocument.LoadFromFileAsync(file);
if (doc.PageCount == 0) throw new InvalidOperationException("empty pdf");
using var page = doc.GetPage(0);
using var stream = new InMemoryRandomAccessStream();
await page.RenderToStreamAsync(stream, new PdfPageRenderOptions { DestinationWidth = 600 });
stream.Seek(0);
var bmp = new BitmapImage();
await bmp.SetSourceAsync(stream);
return bmp;
```
`ThemeShadow` needs `Translation` Z on the card; the Z depth (≈32) and corner radius are visual knobs to fine-tune at F5.

## Verification

**Commands:**
- Build `pdfjunior` for `-p:Platform=x64 -r win-x64` -- expected: 0 warnings / 0 errors.
- Build `pdfjunior.Tests` (x64/win-x64) and run the produced `pdfjunior.Tests.exe` directly (`project_run_tests` memory; `dotnet test` does not discover) -- expected: all green, ≥43 tests, 0 failed/skipped.

**Manual checks (MSIX cannot launch from CLI — Antoine's VS F5 pass):**
- Valid PDF → first page only, centered in a 300px card with rounded corners, border, soft shadow; no zoom/page/scroll/edit controls.
- checking / password / corrupt / nothing → exact MC-7 / MC-8 / MC-9 / MC-2 text, unchanged, no card.
- Checking→Valid shows the image without re-click; drag-reorder the selected file → no flicker/reload; Remove → returns to MC-2.

## Suggested Review Order

**Render pipeline (ViewModel ↔ service)**

- Entry point — async first-page render with cancel + `ReferenceEquals` staleness guard; the heart of the change.
  [`MainViewModel.cs:139`](../../pdfjunior/ViewModels/MainViewModel.cs#L139)

- First-page render via `Windows.Data.Pdf` (page 0 → 600px → `BitmapImage`), mirroring `PdfValidationService`.
  [`PdfPreviewService.cs:13`](../../pdfjunior/Services/PdfPreviewService.cs#L13)

- The injectable abstraction the VM depends on; nullable return is the test seam.
  [`IPdfPreviewService.cs:10`](../../pdfjunior/Services/IPdfPreviewService.cs#L10)

- New singleton registration in the single composition root.
  [`App.xaml.cs:38`](../../pdfjunior/App.xaml.cs#L38)

- 6th constructor dependency.
  [`MainViewModel.cs:125`](../../pdfjunior/ViewModels/MainViewModel.cs#L125)

**View state & bindings**

- `BitmapImage?` replaces `PreviewUri`; re-raises the derived `ShowPreviewImage` gate.
  [`MainViewModel.cs:48`](../../pdfjunior/ViewModels/MainViewModel.cs#L48)

- The 300px card (background + border + 8px radius + `ThemeShadow`) wrapping the centered `Image`.
  [`MainWindow.xaml:103`](../../pdfjunior/MainWindow.xaml#L103)

- Status-change hook: a selected checking file auto-renders when it resolves to Valid (no re-click).
  [`MainViewModel.cs:252`](../../pdfjunior/ViewModels/MainViewModel.cs#L252)

**Tests**

- New: a slow render of A landing after B is selected is dropped by the staleness guard.
  [`MainViewModelTests.cs:692`](../../pdfjunior.Tests/ViewModels/MainViewModelTests.cs#L692)
