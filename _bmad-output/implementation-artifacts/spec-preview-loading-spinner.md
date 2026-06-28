---
title: 'Show a loading spinner in the preview card while a new preview renders'
type: 'feature'
created: '2026-06-28'
status: 'done'
context: []
baseline_commit: '07639a26112b824d938a9569c9c4c373a41e77c5'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** When the user selects a different (valid) PDF, the preview card keeps showing the *previous* file's first-page image during the entire `RenderFirstPageAsync` await, then snaps to the new one. There is no feedback that a new preview is loading, so the stale image looks like the current selection.

**Approach:** Add a transient `Loading` preview state. The instant a valid file's render begins, clear the stale image and show a spinner (`ProgressRing`) centered inside the existing preview card; replace it with the rendered first page when the render completes.

## Boundaries & Constraints

**Always:** The spinner shows *inside* the existing `PreviewCard` (the white rounded card stays visible), in place of the image. It appears only on the valid-file render path and only while that render is in flight. The existing single-in-flight + staleness guards (`_previewCts`, `ReferenceEquals(item, _selectedFile)`) keep their current behavior — a superseded render must never overwrite a fresher state.

**Ask First:** Adding any user-visible text label (would require new localized strings in both `en-US` and `fr-FR`).

**Never:** No new resource strings, no service/threading changes, no new third-party controls (use the built-in `ProgressRing`). Do not show the spinner for the `None`, `Checking`, `ExcludedPassword`, or `ExcludedCorrupt` states — those keep their existing text placeholders. Do not leave the previous file's image visible behind the spinner.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Select valid file (from another image) | Card showing file A's image; user selects valid file B | Stale image cleared immediately; spinner shows inside the card; then B's first page replaces the spinner | N/A |
| Render fails for a Valid file | Render throws non-OCE while still selected | Spinner replaced by corrupt-exclusion text placeholder (existing `ExcludedCorrupt`) | Existing catch path |
| Rapid re-selection | B selected, then C before B finishes | B's render cancelled/dropped; C drives its own Loading→Ready; no flicker back to B | Existing OCE/staleness guards |
| Select still-checking file | File status `Checking` | "Checking…" text placeholder (no spinner); spinner appears only when it later turns Valid and renders | N/A |
| Select non-valid / deselect | Password, corrupt, or null selection | Existing text placeholder / empty state; no spinner | N/A |

</frozen-after-approval>

## Code Map

- `vibepdf/Models/PreviewState.cs` -- preview state enum; add a `Loading` member.
- `vibepdf/MainWindow.xaml` -- `PreviewCard` Border (`:146`) currently holds a single `Image`; needs a container to host the image plus a `ProgressRing`.
- `vibepdf/MainWindow.xaml.cs` -- `SetPreview` (`:331`) toggles card vs. placeholder; `UpdatePreviewAsync` (`:275`) drives the render. Both updated to handle `Loading`.

## Tasks & Acceptance

**Execution:**
- [x] `vibepdf/Models/PreviewState.cs` -- add `Loading` to the enum -- represents "render in flight" so the UI can show a spinner.
- [x] `vibepdf/MainWindow.xaml` -- wrap `PreviewImage` and a new `<ProgressRing x:Name="PreviewProgress" IsActive="False" HorizontalAlignment="Center" VerticalAlignment="Center" />` in a `Grid` inside `PreviewCard` (a Border takes a single child) -- lets the spinner sit centered inside the card, overlapping the image slot.
- [x] `vibepdf/MainWindow.xaml.cs` -- in `SetPreview`, make the card visible for `Ready` *or* `Loading`, set `PreviewProgress.IsActive = (state == Loading)`, and keep the placeholder hidden whenever the card is shown -- single chokepoint for all states.
- [x] `vibepdf/MainWindow.xaml.cs` -- in `UpdatePreviewAsync`, after creating `_previewCts` for the valid path and *before* awaiting `RenderFirstPageAsync`, call `SetPreview(PreviewState.Loading, null)` -- shows the spinner synchronously on selection change.

**Acceptance Criteria:**
- Given the card shows file A's image, when the user selects a different valid file B, then A's image is replaced by a spinner inside the card before B's first page appears.
- Given a valid file's render completes, when it is still the selected file, then the spinner is replaced by the rendered first page (no spinner left behind).
- Given the user selects a password-protected, corrupt, checking, or no file, then the existing text placeholder shows and no spinner appears.
- Given a render is superseded by a newer selection, then the stale render does not overwrite the newer file's spinner or image.

## Design Notes

`ProgressRing.IsActive=false` both stops and hides the ring (template-driven), so toggling `IsActive` alone is enough — no separate `Visibility` juggling for the ring. Clearing `PreviewImage.Source` (already done by `SetPreview(..., null)`) collapses the `Image`, so the inner `Grid` sizes to the ring during loading while the card stays 300px wide. The `Loading` and `Ready` cases fall through to the existing `_ => string.Empty` arm of the placeholder-text switch, so no new strings are needed.

## Verification

**Commands:**
- `dotnet build vibepdf/vibepdf.csproj -p:Platform=x64 -r win-x64` -- expected: build succeeds.

**Manual checks:**
- Add two valid PDFs; click between them and confirm a spinner flashes inside the card before each new first page appears, with no lingering previous image.
- Select a password-protected / unreadable file and confirm the text placeholder shows with no spinner.

## Suggested Review Order

**Trigger & state machine**

- Render path flips to `Loading` synchronously before awaiting — spinner shows the instant selection changes.
  [`MainWindow.xaml.cs:310`](../../vibepdf/MainWindow.xaml.cs#L310)

- `SetPreview` is the single chokepoint: each state maps to exactly one of {card, spinner, placeholder}.
  [`MainWindow.xaml.cs:343`](../../vibepdf/MainWindow.xaml.cs#L343)

**UI & types**

- `ProgressRing` added inside `PreviewCard`, overlapping the image slot in a `Grid`.
  [`MainWindow.xaml:168`](../../vibepdf/MainWindow.xaml#L168)

- New `Loading` enum member representing "render in flight".
  [`PreviewState.cs:3`](../../vibepdf/Models/PreviewState.cs#L3)
