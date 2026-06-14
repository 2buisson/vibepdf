---
name: PDF Junior
description: Free, no-frills Windows 11 PDF merger. WinUI 3 / Fluent Design; zero brand-layer overrides.
status: final
sources:
  - ../../prds/prd-pdf-junior-2026-06-14/prd.md
  - ../../architecture.md
updated: 2026-06-14
colors:
  # No brand overrides. All colors inherited from WinUI theme resources.
  # The user's Windows accent color (SystemAccentColor) is the only chromatic
  # element — used on the Merge button (AccentButtonStyle) and list selection.
  # Errors use the semantic SystemFillColorCriticalBrush, never accent.
  accent: '{SystemAccentColor}'
  accent-foreground: '{TextOnAccentFillColorPrimaryBrush}'
  background: '{ApplicationPageBackgroundThemeBrush}'
  surface: '{CardBackgroundFillColorDefaultBrush}'
  text-primary: '{TextFillColorPrimaryBrush}'
  text-secondary: '{TextFillColorSecondaryBrush}'
  text-disabled: '{TextFillColorDisabledBrush}'
  border: '{CardStrokeColorDefaultBrush}'
  critical: '{SystemFillColorCriticalBrush}'
  caution: '{SystemFillColorCautionBrush}'
  success: '{SystemFillColorSuccessBrush}'
typography:
  # No overrides. Segoe UI Variable via WinUI 3 type ramp.
  body:
    fontFamily: Segoe UI Variable
    fontSize: 14px
    fontWeight: '400'
    lineHeight: '20px'
    note: 'WinUI BodyTextBlockStyle'
  caption:
    fontFamily: Segoe UI Variable
    fontSize: 12px
    fontWeight: '400'
    lineHeight: '16px'
    note: 'WinUI CaptionTextBlockStyle'
  subtitle:
    fontFamily: Segoe UI Variable
    fontSize: 20px
    fontWeight: '600'
    lineHeight: '28px'
    note: 'WinUI SubtitleTextBlockStyle'
rounded:
  # WinUI ControlCornerRadius defaults. No overrides.
  control: 4px
  overlay: 8px
  note: 'WinUI ControlCornerRadius / OverlayCornerRadius'
spacing:
  # WinUI 4pt grid. No overrides.
  base: 4px
  note: 'Standard Fluent 4-pt increment scale'
components:
  merge-button:
    style: AccentButtonStyle
    foreground: '{colors.accent-foreground}'
    background: '{colors.accent}'
    radius: '{rounded.control}'
  info-bar-success:
    severity: Success
    background: '{colors.success}'
  info-bar-error:
    severity: Error
    background: '{colors.critical}'
  content-dialog:
    radius: '{rounded.overlay}'
  list-view-item-selected:
    background: '{SystemAccentColorLight2}'
---

## Brand & Style

PDF Junior is a free, single-purpose Windows 11 utility that merges PDF files. The brand posture is *no posture* — the app inherits Fluent Design wholesale and adds nothing on top. No custom palette, no decorative illustration, no brand typeface, no splash screen. The product earns trust through restraint: it looks like a piece of Windows, not a product trying to impress you.

The aesthetic contract is "respect the OS." PDF Junior follows the user's Windows accent color, follows the OS light/dark theme, and uses Mica on the title bar — the same materials the user sees in File Explorer, Settings, and Calculator. A user who installs PDF Junior should feel like they installed a missing Windows feature, not a third-party app.

## Colors

PDF Junior uses **zero custom colors**. Every brush comes from WinUI theme resources, which adapt automatically to the user's light/dark mode and accent color.

- **Accent (`{SystemAccentColor}`)** appears in exactly two places: the **Merge** button (`AccentButtonStyle`) and the **selected list item** highlight. Nowhere else. The accent means "the action" and "the thing you picked."
- **Critical (`{SystemFillColorCriticalBrush}`)** colors the status text for flagged files (password-protected, corrupt). Never used on accent surfaces; never used decoratively.
- **All other surfaces** — background, card, border, text primary/secondary/disabled — inherit from WinUI theme resources and change automatically with the OS theme.

Avoid: custom hex values, brand colors, gradient fills, colored badges, tinted backgrounds, accent on anything other than Merge and selection. The discipline is zero-brand-colors.

## Typography

Segoe UI Variable, the WinUI 3 default. No override, no secondary typeface. The type ramp follows WinUI's standard text styles:

- **Body** (`BodyTextBlockStyle`, 14/20) — file names, microcopy, banner text, dialog body.
- **Caption** (`CaptionTextBlockStyle`, 12/16) — page counts, status labels ("Password protected", "Checking..."), disabled-Merge tooltip text.
- **Subtitle** (`SubtitleTextBlockStyle`, 20/28) — not used in v1's single-window layout, but available if a section header is needed.

No display type, no decorative type, no bold-for-emphasis in running copy. The type ramp is flat on purpose — PDF Junior has no content hierarchy complex enough to need more than body and caption.

## Layout & Spacing

WinUI's 4-pt spacing grid, inherited as-is. Key layout dimensions:

- **Window:** minimum 640×480, default 900×640, resizable and maximizable.
- **Two-pane layout:** left sidebar (File list) + right pane (Preview toolbar + Preview). A vertical drag divider separates them. Sidebar default width: 280px, min 200px, max 50% of window width. Resets on each launch — no persistence.
- **Action bar:** full-width bar at the bottom. Buttons right-aligned: **[Add PDF(s)] [Merge]**.
- **Progress indicator:** thin `ProgressBar` above the Action bar, visible only during merges exceeding 2 seconds.
- **Margins and padding:** follow WinUI NavigationView content-area defaults (48px top where the title bar allows, 16–24px horizontal). Exact content margins follow WinUI defaults; no custom overrides.

No multi-column grid, no responsive breakpoints (single fixed desktop surface), no fluid typography.

## Elevation & Depth

WinUI's material hierarchy, unchanged:

- **Title bar:** Mica backdrop (the thin translucent material that shows the desktop wallpaper color).
- **Window body:** standard `ApplicationPageBackgroundThemeBrush` — solid, flat.
- **ContentDialog (close guard):** Fluent's standard overlay elevation with smoke-layer backdrop.
- **InfoBar (success/error banners):** inline, no elevation — sits flat within the content flow.

No custom shadows, no layered cards, no floating toolbars. The app is visually flat by design.

## Shapes

WinUI defaults. `ControlCornerRadius` (4px) on buttons, text inputs, list items. `OverlayCornerRadius` (8px) on dialogs. No pills, no circles, no custom radii.

## Components

PDF Junior uses standard WinUI controls exclusively. No custom components.

| WinUI Control | Role | Visual spec |
|---|---|---|
| `ListView` | File list (sidebar) | Single-select. Selected-item highlight uses accent. Items show filename + caption-sized status/page-count. |
| `Button` | **Add PDF(s)** | Default style (no accent). Right-aligned in the Action bar. |
| `Button` | **Merge** | `AccentButtonStyle`. Right-aligned in the Action bar, adjacent to Add. |
| `Button` | **Move up**, **Move down** | Default style, icon-only or compact. Grouped in the Preview toolbar, left of Remove. |
| `Button` | **Remove** | Default style. Preview toolbar, separated from Move buttons by a gap. |
| `InfoBar` | Success banner | `Severity="Success"`. Inline, above the Action bar or at the top of the content area. Auto-dismisses after ~8 seconds; manually closable. Contains an **Open folder** action button. |
| `InfoBar` | Error banner | `Severity="Error"`. Same position. Manual dismiss only (no auto-dismiss). |
| `ProgressBar` | Merge progress | Thin, determinate (by file count). Above the Action bar. Visible only after 2 seconds of merge time. |
| `ContentDialog` | Window-close guard | Standard overlay. Primary button: "Keep merging" (default). Secondary: "Close anyway". |
| `ScrollViewer` | Preview pane | Vertical scroll only. Contains rendered PDF page images (fit-to-width). |
| `TextBlock` | State placeholders | Body or caption style. "Add PDFs to get started", "Select a file to preview it", "Checking...", exclusion notices. |

No custom-drawn controls, no third-party control libraries, no animated components.

## Do's and Don'ts

| Do | Don't |
|---|---|
| Use `{ThemeResource ...}` for every color, brush, and spacing value | Hardcode hex colors, pixel values, or custom brushes |
| Use `AccentButtonStyle` on **Merge** only | Apply accent to any other button, icon, or surface |
| Use `{SystemFillColorCriticalBrush}` for error/flagged states | Use accent or custom red for errors |
| Follow the OS light/dark theme automatically | Add an in-app theme toggle or force a theme |
| Use Mica on the title bar | Use Acrylic, custom backdrops, or gradient fills |
| Let WinUI controls render at their default sizes and corner radii | Override ControlCornerRadius, min-heights, or padding |
| Show text-only states ("Checking...", "Password protected") | Add spinners, icons, or glyphs to text state labels |
| Use the standard WinUI type ramp (Body, Caption, Subtitle) | Add custom font sizes, weights, or typefaces |
