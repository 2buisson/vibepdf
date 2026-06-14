---
name: PDF Junior
description: Free, no-frills Windows 11 PDF merger. Native C# / WinUI 3 (Windows App SDK); inherits Fluent Design wholesale. This DESIGN.md specifies the deliberately minimal delta on top of Fluent/WinUI defaults.
status: final
sources:
  - {planning_artifacts}/prds/prd-pdf-junior-2026-06-14/prd.md
  - {planning_artifacts}/prds/prd-pdf-junior-2026-06-14/addendum.md
  - {planning_artifacts}/prds/prd-pdf-junior-2026-06-14/reconcile-ux.md
updated: 2026-06-14
colors:
  # PDF Junior adds NO custom palette. Every color is a WinUI theme resource.
  # The only "brand" color is the user's own Windows accent, used sparingly.
  accent: '{SystemAccentColorBrush}'          # Merge button fill + list selection ONLY
  accent-foreground: '{TextOnAccentFillColorPrimaryBrush}'
  text-primary: '{TextFillColorPrimaryBrush}'     # filenames, body
  text-secondary: '{TextFillColorSecondaryBrush}' # status captions, empty-state text, page counts
  error-text: '{SystemFillColorCriticalBrush}'    # error captions + flagged-preview notice
  surface: '{LayerFillColorDefaultBrush}'         # sidebar / pane backgrounds (Mica shows through chrome)
  divider: '{DividerStrokeColorDefaultBrush}'     # sidebar/preview split, list separators
  # All other tokens (control fills, info/error InfoBar severities, focus ring,
  # ProgressBar) inherit WinUI light/dark theme resources unchanged.
typography:
  # Segoe UI Variable (WinUI default ramp). No custom face. Roles map to the ramp.
  filename:
    style: 'BodyTextBlockStyle'        # ~14px, row primary line
  caption:
    style: 'CaptionTextBlockStyle'     # ~12px, status line under filename
  dialog-title:
    style: 'SubtitleTextBlockStyle'    # ~20px, ContentDialog title
  body:
    style: 'BodyTextBlockStyle'        # banners, notices, empty states
rounded:
  # Fluent defaults, no override.
  control: '{ControlCornerRadius}'     # 4px — buttons, list items, text controls
  overlay: '{OverlayCornerRadius}'     # 8px — dialogs, flyouts
spacing:
  # 4-pt grid. WinUI default rhythm.
  scale: [4, 8, 12, 16, 20, 24, 32]
  sidebar-default: 320px               # in the 900px default window
  sidebar-min: 240px
  sidebar-max: 50%                     # of window width
components:
  list-item-twoline:
    line1: '{typography.filename}'                # filename, {colors.text-primary}
    line2: '{typography.caption}'                 # status caption, {colors.text-secondary} (or {colors.error-text} on error)
    selected-background: '{colors.accent}'        # selection highlight (the only list use of accent)
    radius: '{rounded.control}'
  button-merge:
    style: 'AccentButtonStyle'                     # {colors.accent} fill, {colors.accent-foreground} text
    radius: '{rounded.control}'
  button-standard:
    style: 'DefaultButtonStyle'                    # Add PDF(s), Move up/down, Remove, Open folder
    radius: '{rounded.control}'
  infobar-success:
    severity: 'Success'                            # WinUI InfoBar
  infobar-error:
    severity: 'Error'
  progressbar-merge:
    control: 'ProgressBar'                         # thin, above the action bar; >2s only
  dialog-close-guard:
    control: 'ContentDialog'
    radius: '{rounded.overlay}'
---

# PDF Junior — Design Spine

> Native C# / WinUI 3 desktop app. **Fluent Design is the design system**; this DESIGN.md is the (intentionally thin) brand-layer delta. `EXPERIENCE.md` is the behavioral peer and cross-references the tokens above by name. Both spines win on conflict with any mock.

## Brand & Style

PDF Junior is a free, privacy-first Windows 11 utility that **merges PDFs and gets out of the way**. The entire brand posture is *restraint*: it does one thing, does it well, and never shouts. There is no logo moment in the UI, no marketing surface, no custom chrome — the product earns trust through clarity, speed, and the visible fact that nothing leaves the device.

The design discipline follows directly: **inherit Fluent Design wholesale.** PDF Junior is not a place to express a custom visual identity — it is a place to look and behave exactly like a well-made part of Windows 11, so the user's attention stays on their documents, not the app. This DESIGN.md therefore specifies almost nothing of its own; its job is to *name the few decisions* (two-line list rows, sidebar proportions, where accent is and isn't allowed) and to forbid the temptation to decorate. Customizing Fluent's defaults beyond this delta is explicitly against the brand.

## Colors

PDF Junior adds **no custom palette.** Every color is a WinUI theme resource that follows the OS light/dark theme automatically — there is no in-app theme toggle.

- **Accent (`{colors.accent}` — the user's own Windows accent color)** is used in exactly two places: the **Merge** button fill and the **list-item selection** highlight. Nowhere else. Accent means "the primary action" or "this is the selected file" — never decoration, never status.
- **Error text (`{colors.error-text}` — `SystemFillColorCriticalBrush`)** colors the status caption of a flagged row ("Password protected" / "Could not read file") and the flagged-file notice in the preview pane. It is a *semantic* color, never the accent — errors must read as errors regardless of the user's accent choice.
- **Text primary / secondary (`{colors.text-primary}` / `{colors.text-secondary}`)** — filenames are primary; status captions, page counts, and all empty-state copy are secondary. The secondary tone keeps the calm, quiet register (PRD §10).
- **Surface & divider** inherit WinUI layer/divider brushes; **Mica** backdrop shows through the title bar (and behind chrome where the platform applies it).
- **Everything else** — control fills, `InfoBar` Success/Error severities, the focus ring, the `ProgressBar` — inherits WinUI's theme resources unchanged.

Avoid: any custom hex value, gradients, decorative tints, using the accent for status or chrome, or a second brand color. The discipline is **accent-in-two-places, semantics-for-errors, theme-resources-for-everything-else.**

## Typography

**Segoe UI Variable**, the WinUI default — no custom face. Roles map to the platform type ramp:

- **Filename** — `BodyTextBlockStyle` (~14px), the primary line of each list row, in `{colors.text-primary}`.
- **Status caption** — `CaptionTextBlockStyle` (~12px), the second line of each row and the page count, in `{colors.text-secondary}` (or `{colors.error-text}` on a flagged row).
- **Dialog title** — `SubtitleTextBlockStyle` (~20px), used for the close-during-merge `ContentDialog` title ("Stop the merge?").
- **Body** — `BodyTextBlockStyle` for banner text, the flagged-file preview notice, and empty-state lines.

No display/serif role exists — there is no hero moment in this product. Type is functional throughout.

## Layout & Spacing

- **4-pt grid** (`{spacing.scale}` = 4, 8, 12, 16, 20, 24, 32), WinUI's default rhythm.
- **Single persistent window.** Default **900×640**, minimum **640×480**, resizable and maximizable. No navigation chrome — no tabs, nav rail, or sidebar nav.
- **Two-pane split:** left **File list sidebar** at `{spacing.sidebar-default}` (320px) in the default window, **preview pane** fills the rest (~580px). The user can drag the divider between `{spacing.sidebar-min}` (240px) and `{spacing.sidebar-max}` (50% of window width).
- **Vertical structure of the right pane (top → bottom):** preview toolbar (right-aligned), preview render area (scrolls), then — spanning the full window width below both panes — the thin merge progress bar (only when a merge runs >2s) and the action bar.
- **No persistence:** neither window geometry nor sidebar width is remembered; every launch starts at the defaults (PRD §9.1).

## Elevation & Depth

Inherited from Fluent. **Mica** on the title bar. `InfoBar` banners and the `ContentDialog` use WinUI's standard elevation; the two panes are flat layer surfaces separated by a `{colors.divider}` stroke, not by shadow. PDF Junior adds no elevation of its own — depth is a Fluent affordance, not a brand device.

## Shapes

Fluent defaults, unchanged: `{rounded.control}` (4px) for buttons, list items, and text controls; `{rounded.overlay}` (8px) for the `ContentDialog` and any flyout. No custom radii, no pill shapes.

## Components

PDF Junior uses these WinUI controls **as-is**, with only the behavioral specs in `EXPERIENCE.md`: `InfoBar`, `ContentDialog`, `ProgressBar`, `ListView`, `Button`, `AppBarButton`/`Button` for toolbar actions, and the native `FileOpenPicker` / `FileSavePicker`. The contract: don't restyle them.

Delta components named by this spine:

- **Two-line list item (`{components.list-item-twoline}`)** — the core surface. Line 1 is the filename (`{typography.filename}`, `{colors.text-primary}`); line 2 is the always-visible status caption (`{typography.caption}`) — `{colors.text-secondary}` for *checking* ("Checking…"), *valid* ("{N} pages" / "1 page"), and `{colors.error-text}` for *error-password* ("Password protected") / *error-corrupt* ("Could not read file"). Selected row uses `{components.list-item-twoline.selected-background}` (`{colors.accent}`) — the only list use of accent.
- **Merge button (`{components.button-merge}`)** — `AccentButtonStyle`; the single accent-filled element. Lives at the right of the action bar.
- **Standard buttons (`{components.button-standard}`)** — `Add PDF(s)` (action bar), `Move up` / `Move down` (grouped) and `Remove` (gap-separated) in the preview toolbar, and `Open folder` (in the success banner). Default button styling, no accent.
- **Success / error banners (`{components.infobar-success}` / `{components.infobar-error}`)** — WinUI `InfoBar`, Success and Error severities. One at a time.
- **Merge progress (`{components.progressbar-merge}`)** — thin `ProgressBar` above the action bar, shown only after 2s; determinate when the engine reports per-file progress, else indeterminate. No accompanying label.
- **Close-during-merge dialog (`{components.dialog-close-guard}`)** — `ContentDialog`, title "Stop the merge?".

## Do's and Don'ts

| Do | Don't |
|---|---|
| Inherit WinUI/Fluent defaults for everything not named here | Introduce a custom palette, font, or radius |
| Use the Windows accent on the Merge button and list selection **only** | Use accent for status, chrome, hover, or decoration |
| Color errors with `{colors.error-text}` (semantic) | Color errors with the accent (breaks at some accent choices) |
| Keep status captions in `{colors.text-secondary}`, calm and quiet | Add icons/spinners/illustration to states (text-only, PRD §10) |
| Let Mica + theme resources follow the OS light/dark theme | Add an in-app theme toggle |
| Two-line rows: filename + always-visible status caption | Hide status behind hover, or add a third line |
| Reset window/sidebar geometry to defaults every launch | Persist any geometry or location between sessions |
