---
title: 'Add app logo to title bar'
type: 'feature'
created: '2026-06-28'
status: 'done'
context: []
baseline_commit: '87fd67ab6aa8825c49eaae74577545967d70dd72'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The custom title bar shows only the app name as text in the top-left. There is no visual brand mark, unlike standard Windows apps that pair a small logo with the title.

**Approach:** Place the existing bundled PDF logo (16×16) immediately left of the title text inside the current `AppTitleBar` drag region, by wrapping the logo and `TitleText` in a horizontal `StackPanel`.

## Boundaries & Constraints

**Always:** Reuse the existing bundled logo asset already shipped in the package. Keep the `TitleText` `x:Name` and its `x:Bind` intact so the merge-progress text swap (`UpdateTitle()`) keeps working. The logo stays inside the draggable title-bar region.

**Ask First:** Using a different logo asset/source than `Square44x44Logo.targetsize-48.png`, or adding any new image asset to the project.

**Never:** Do not change caption-button behavior or title-bar theming. Do not change the title-bar height. Do not introduce a ViewModel, converter, or any new file. No code-behind changes.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Normal launch | App window shown | Logo visible top-left, immediately left of "Vibe PDF" | N/A |
| Merge in progress | `UpdateTitle()` swaps text to "Vibe PDF — 55%" | Logo stays visible and unchanged; only text changes | N/A |
| Light / dark theme | System/app theme toggled | Colored logo renders identically in both themes | N/A |
| Drag by logo area | User drags the logo region | Window moves (logo is non-interactive, stays draggable) | N/A |

</frozen-after-approval>

## Code Map

- `vibepdf/MainWindow.xaml` -- `AppTitleBar` grid (~lines 200–212) currently holds a single `TitleText` TextBlock; the change happens here.
- `vibepdf/MainWindow.xaml.cs` -- `UpdateTitle()` (~line 454) swaps `TitleText.Text`; relies on the `x:Name` staying. No change, but must not break.
- `vibepdf/vibepdf.csproj` -- `Square44x44Logo.targetsize-48.png` already declared as `Content` (line 29) and packaged; reference only, no change.

## Tasks & Acceptance

**Execution:**
- [x] `vibepdf/MainWindow.xaml` -- Wrap the existing `TitleText` in a horizontal `StackPanel` (`VerticalAlignment="Center"`, `Margin="16,0,0,0"`, `Spacing="8"`) and add an `Image` before it (`Width="16"`, `Height="16"`, `Source="ms-appx:///Assets/Square44x44Logo.targetsize-48.png"`). Move the existing `Margin="16,0,0,0"` from `TitleText` onto the `StackPanel`. -- Adds the brand mark to the title bar's top-left while preserving the title text and its name.

**Acceptance Criteria:**
- Given the app is launched, when the window appears, then the PDF logo is visible at the top-left of the title bar, immediately left of "Vibe PDF".
- Given a merge is in progress, when the title text shows the live percentage, then the logo remains visible and unchanged.
- Given the title bar is dragged from the logo area, when the pointer moves, then the window moves with it.
- Given the app runs in light or dark theme, when the title bar renders, then the logo displays correctly in both.

## Design Notes

- Keep `TitleText`'s `x:Name` and `x:Bind`; only wrap it. Code-behind `UpdateTitle()` continues to work untouched.
- Use `targetsize-48` (icon fills the frame, transparent background) rather than `scale-200` (padded tile), so a 16px logo isn't shrunk further by built-in padding.
- An `Image` is non-interactive, so it remains part of the `SetTitleBar(AppTitleBar)` draggable region — no `PassthroughElements`/`InputNonClientPointerSource` work needed.

## Verification

**Commands:**
- `dotnet build vibepdf/vibepdf.csproj -p:Platform=x64 -r win-x64` -- expected: build succeeds with no new warnings.

**Manual checks:**
- Launch the app; confirm the logo sits at the top-left of the title bar, left of the title text, vertically centered.
- Start a merge; confirm the logo persists while the title shows the live percentage.
- Toggle system theme (light/dark); confirm the logo renders correctly in both.

## Suggested Review Order

- Design intent: the old single `TitleText` becomes a horizontal panel holding logo + text; takes over the title's left margin.
  [`MainWindow.xaml:206`](../../vibepdf/MainWindow.xaml#L206)

- The brand mark itself — 16×16, sourced from the already-packaged PDF logo asset (targetsize variant, minimal padding).
  [`MainWindow.xaml:211`](../../vibepdf/MainWindow.xaml#L211)

- Title text unchanged: `x:Name` + `x:Bind` preserved so the merge-progress swap (`UpdateTitle()`) still works.
  [`MainWindow.xaml:216`](../../vibepdf/MainWindow.xaml#L216)
