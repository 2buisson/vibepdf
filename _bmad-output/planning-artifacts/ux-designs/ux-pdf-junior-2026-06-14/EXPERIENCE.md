---
name: PDF Junior
status: final
sources:
  - {planning_artifacts}/prds/prd-pdf-junior-2026-06-14/prd.md
  - {planning_artifacts}/prds/prd-pdf-junior-2026-06-14/addendum.md
  - {planning_artifacts}/prds/prd-pdf-junior-2026-06-14/reconcile-ux.md
updated: 2026-06-14
---

# PDF Junior — Experience Spine

> Single-window Windows 11 desktop app, native C# / WinUI 3 (Windows App SDK). `DESIGN.md` is the visual-identity reference; this spine owns IA, behavior, states, interactions, and flows. Tokens are cross-referenced as `{path.to.token}` into `DESIGN.md`. Both spines win on conflict with any mock.

## Foundation

**Form factor:** one persistent desktop window, no navigation chrome (no tabs, nav rail, or sidebar nav). Every state is an in-place transition within that single screen. Default 900×640, minimum 640×480, resizable/maximizable.

**UI system:** WinUI 3 / Fluent Design, inherited wholesale. Behavior is built on native controls — `ListView`, `Button` / `AccentButton`, `InfoBar`, `ContentDialog`, `ProgressBar`, and the native `FileOpenPicker` / `FileSavePicker`. This spine specifies the **behavioral delta** only; visual specs live in `DESIGN.md.Components`.

**Privacy is a felt property, not a setting:** all processing is local, there is no account, no network in normal operation, and **no persistence of any kind** between sessions — relaunch always starts empty, at default geometry, with no recalled output location. The experience never asks the user to trust a promise it can't see.

## Information Architecture

One screen, four persistent regions plus transient surfaces.

| Region | Position | Purpose |
|---|---|---|
| Title bar | Top, full width | Native Windows title bar, Mica backdrop. No app controls. |
| File list (sidebar) | Left, `{spacing.sidebar-default}` wide | The ordered, scrollable list of added PDFs. Merge output follows this order. Drag-resizable (`{spacing.sidebar-min}`–`{spacing.sidebar-max}`). |
| Preview toolbar | Top of right pane, right-aligned | `[Move up] [Move down]` grouped, gap, then `[Remove]` — all act on the selected file. |
| Preview pane | Right, fills remainder | Read-only render of the selected file; fit-to-width, vertical scroll only. |
| Progress bar | Full width, above action bar | Thin `{components.progressbar-merge}`; visible only during a merge running >2s. |
| Action bar | Bottom, full width, right-aligned | `[Add PDF(s)]` then `[Merge]` (`{components.button-merge}`). No filename field — output name is set in the Save dialog. |

**Transient surfaces** (overlays/inline, not separate screens): native File picker, native Save dialog (folder + filename + OS overwrite prompt), the close-during-merge `ContentDialog`, and the success/error `InfoBar` banners.

The IA closes cleanly: every stated user need maps to a region — *add* and *order* and *see status* → File list; *inspect* → Preview pane; *reorder/remove* → Preview toolbar; *produce output* → Action bar + Save dialog; *recover/confirm* → banners + close-guard dialog.

→ See `mockups/` for any rendered key screens (added at Finalize if produced). Spine wins on conflict.

## Voice and Tone

Microcopy only; visual/brand posture lives in `DESIGN.md`. The governing rule is **silent by default**: no instructional copy appears except the two empty states. Every other state is quiet. The register is office-worker/student plain English; developer jargon ("concatenate", "render pipeline", "stdout") is banned.

**Per-situation tone:**

| Situation | Tone | Example |
|---|---|---|
| Empty state | Warm invitation, single line | "Add PDFs to get started" |
| File error | Factual, non-blaming | "Password protected" (never "bad file") |
| Merge success | Brief, affirming, one optional action | "Merged successfully — report.pdf" + Open folder |
| Merge failure | Honest, specific reason if available, **no apology filler** | "Not enough space on E:\." / "Merge failed. Try again or check the files." |
| Disabled Merge | Helpful — names the fix | "Remove or replace files with errors" |
| Overwrite | **OS-owned** (native Save dialog) | (the app authors none) |

**Canonical strings** (locked; downstream copy must match):

| Where | String |
|---|---|
| Empty list (sidebar) | `Add PDFs to get started` |
| Empty preview | `Select a file to preview it` |
| Checking caption | `Checking…` |
| Valid caption | `{N} pages` (and `1 page`) |
| Password caption | `Password protected` |
| Corrupt caption | `Could not read file` |
| Flagged preview notice | `This file has an issue — it will be excluded from the merge.` |
| Disabled Merge — empty/no-valid | `Add at least one PDF to merge` |
| Disabled Merge — flagged present | `Remove or replace files with errors` |
| Disabled Merge — still checking | `Wait for files to finish checking` |
| Success banner | `Merged successfully — {filename}` (action: `Open folder`) |
| Failure with reason | `Not enough space on E:\.` · `Access denied` · `File not found: {name}` |
| Failure generic fallback | `Merge failed. Try again or check the files.` |
| Open-folder failure | `Folder not found` |
| Close-during-merge dialog | Title `Stop the merge?` / Body `A merge is in progress. Closing now cancels it; the output file may be incomplete.` / Buttons `Keep merging` (default), `Close anyway` |
| Buttons | `Add PDF(s)` · `Merge` · `Move up` · `Move down` · `Remove` · `Open folder` |

## Component Patterns

Behavioral rules; visual specs live in `DESIGN.md.Components`.

| Component | Use | Behavioral rules |
|---|---|---|
| Two-line list item (`{components.list-item-twoline}`) | File list | Click selects the row and loads its preview (always in sync). Line 2 is the always-visible status caption — never hover-revealed. Remains selectable, removable, and reorderable in **any** status, including *checking*. |
| Preview toolbar buttons (`{components.button-standard}`) | Top of right pane | All act on the selected file. `Move up` disabled when selected is first; `Move down` disabled when last; all three disabled when nothing is selected. `Remove` is gap-separated from the Move group to prevent accidental activation. |
| Merge button (`{components.button-merge}`) | Action bar | Enabled only when ≥1 valid file, zero flagged, none checking. Disabled state shows the cause-specific tooltip (see State Patterns). Press dismisses any visible banner, then opens the Save dialog. |
| Add PDF(s) button (`{components.button-standard}`) | Action bar | Opens the native multi-select File picker filtered to `.pdf`. |
| InfoBar (`{components.infobar-success}` / `{components.infobar-error}`) | Below preview / above action bar | At most one banner at a time. Success auto-dismisses ~8s and is manually closable; Error is manual-dismiss only. Starting a new Merge clears any visible banner first. |
| ProgressBar (`{components.progressbar-merge}`) | Above action bar | Appears only after a merge passes 2s; bar only, no label; determinate when per-file progress is reported, else indeterminate. |
| ContentDialog (`{components.dialog-close-guard}`) | Modal | Close-during-merge guard. `Keep merging` is default; `Close anyway` cancels cooperatively (a partial output file may remain — no atomicity guarantee, PRD FR-11/FR-12). |

## State Patterns

| State | Surface | Treatment |
|---|---|---|
| Empty list | Sidebar | Muted, centered `Add PDFs to get started` — text only, no glyph (`{colors.text-secondary}`). |
| Empty preview (nothing selected) | Preview pane | Muted, centered `Select a file to preview it` — text only. |
| Checking | Row + preview | Row caption `Checking…` (`{colors.text-secondary}`); preview pane shows `Checking…`, **text only, no spinner**. A per-file wall-clock guard resolves a never-completing check to *error-corrupt* so an item never stays permanently non-interactive (threshold tuned in architecture; legacy was 30s). |
| Valid | Row + preview | Row caption `{N} pages`; preview renders fit-to-width, vertical scroll only, no editing/annotation/page controls. |
| Flagged — password | Row + preview | Row caption `Password protected` in `{colors.error-text}`; preview shows the flagged notice; no preview content required (encrypted files generally can't render). |
| Flagged — corrupt | Row + preview | Row caption `Could not read file` in `{colors.error-text}`; preview attempts to render whatever partial content it can, with the flagged notice; if nothing renders, the notice alone is sufficient feedback. |
| Merge running ≤2s | Whole window | **No chrome at all** — no flash, no spinner, no bar. Absence of feedback is intended. The UI locks for execution (see the >2s row) but the merge finishes before any indicator would appear. |
| Merge running >2s | Above action bar | Thin `{components.progressbar-merge}` appears; UI locked for the **execution phase only** — File list read-only, Preview toolbar + `Add PDF(s)` + `Merge` disabled, **preview stays scrollable**, window-close guard responsive (NFR-1). |
| Merge success | Below preview / above action bar | `{components.infobar-success}`: `Merged successfully — {filename}` with `Open folder`. Auto-dismiss ~8s + manual close. File list preserved. |
| Merge failure | Same | `{components.infobar-error}`: specific reason preferred, else `Merge failed. Try again or check the files.` Manual dismiss only. A partial/incomplete output file **may remain** at the destination (no atomicity guarantee, PRD FR-11). UI unlocks; file list preserved for retry. |
| Open folder — missing | Inline | If the output folder no longer exists, show inline `Folder not found` instead of failing. |
| Disabled Merge | Action bar tooltip | Three distinct hover tooltips by cause: empty/no-valid → `Add at least one PDF to merge`; any flagged → `Remove or replace files with errors`; any still checking → `Wait for files to finish checking`. |
| Close during merge | Modal | `{components.dialog-close-guard}` "Stop the merge?" — `Keep merging` (default) / `Close anyway` (cancels cooperatively; a partial output file may remain). |

## Interaction Primitives

**Mouse-first, single-select.** The product is a short, linear task; there is no power-user surface.

- **Select** — click a row to select it and load its preview; selection and preview are always in sync. Clicking the selected row again is a no-op. Adding files never changes the current selection.
- **Add** — `Add PDF(s)` → native multi-select picker (`.pdf` filter). Selected files append to the **bottom** in picker order; the list **auto-scrolls to reveal the newly added items**. Duplicates (same absolute path, case-insensitive) are silently skipped. Cancelling the picker is a silent no-op.
- **Reorder** — `Move up` / `Move down` act on the selected file; **selection follows the moved file and the preview pane does not reload or flicker** (the same file stays rendered at its new position). No drag-to-reorder.
- **Remove** — `Remove` (selected file only) deletes the row, **clears selection** (no neighbor auto-selected), and returns the preview to `Select a file to preview it`. A flagged file is removed exactly like any other.
- **Merge** — pressing `Merge` first dismisses any visible banner, then opens the native Save dialog (filename pre-filled `merged.pdf`, `.pdf` type). The merge begins only after the dialog returns a confirmed destination; cancelling the dialog aborts silently and leaves the app idle and unlocked.

**Banned:** drag-to-reorder (v1); hover-to-reveal status; any spinner/flash on sub-2s merges; silent inclusion of a not-yet-checked or flagged file; silent overwrite (the OS Save dialog owns overwrite confirmation).

## Accessibility Floor

**Accessibility is explicitly out of scope for v1** (PRD §5, §6.2): no committed keyboard-only guarantees, no Narrator/screen-reader announcements, no WCAG conformance target, no custom focus management. The app retains **only** the baseline UI Automation that native WinUI controls provide for free (ListView arrow-key navigation, default Tab order, control names) — no accessibility work is scoped, and none should be implied by downstream stories. Visual contrast inherits WinUI theme defaults (`DESIGN.md`).

## Key Flows

Named-protagonist journeys carried from PRD §2.3 (UJ-1…UJ-4), with the emotional climax beats restored.

### Flow 1 — Marcus merges four handouts for the first time (UJ-1)

*Marcus, a teacher, has four PDF handouts he needs as one file for the class portal.*

1. He launches PDF Junior. The sidebar reads `Add PDFs to get started`; the preview reads `Select a file to preview it`.
2. He clicks `Add PDF(s)`, multi-selects four PDFs in the native picker. Each appears at the bottom, briefly showing `Checking…`, then resolving to `12 pages`, `8 pages`, etc.
3. He clicks one to preview it — it loads fit-to-width on the right. He notices a handout is out of order, selects it, and clicks `Move up`; the preview keeps showing that same file, now one row higher, without a flicker.
4. **Climax:** He clicks `Merge`. The native Save dialog opens with `merged.pdf` pre-filled; he keeps the name, picks the Desktop, and clicks Save. The merge is quick enough that no progress bar ever appears — and a banner reads `Merged successfully — merged.pdf`. **Four handouts, one file, done — uploaded before the bell.**
5. He clicks `Open folder`; File Explorer opens to the output. His file list is still there.

*Failure path:* if the picker is cancelled at step 2, nothing is added — no error, no banner.

### Flow 2 — Sophie removes two unreadable files before merging (UJ-2)

*Sophie, a paralegal, adds six PDFs; two are problematic.*

1. She adds six files. Four resolve to page counts; one resolves to `Password protected` and one to `Could not read file`, both captions in `{colors.error-text}`.
2. `Merge` is disabled. Hovering it reads `Remove or replace files with errors`.
3. She clicks the password-protected row; the preview shows `This file has an issue — it will be excluded from the merge.` She clicks `Remove`; selection clears. She does the same for the corrupt file.
4. **Climax:** Five valid files remain, `Merge` re-enables, and the merge succeeds. **The flagged files were never silently swept into the output — Sophie exhales, knowing exactly what's in the file she's about to file with the court.**

### Flow 3 — Marcus intentionally overwrites a previous output (UJ-3)

*Marcus reorders his files and merges again to the same folder.*

1. He reorders, clicks `Merge`, and in the Save dialog navigates to the same Desktop folder where a `merged.pdf` already exists, keeping that name.
2. **Climax:** The **native** Save dialog warns the file already exists and asks whether to replace it. He confirms Replace; the merge runs. **The overwrite happened because Windows asked and he said yes — the app never decided that for him.**

### Flow 4 — David recovers from a failed merge without losing his work (UJ-4)

*David, a home user, adds five valid scans.*

1. He clicks `Merge`, types a custom filename, and chooses a USB drive that turns out to be full. The merge fails.
2. An error banner reads `Not enough space on E:\.` and stays until he dismisses it. His file list is untouched — a partial, unusable file may be left on the drive, which a successful retry to the same name overwrites.
3. **Climax:** He frees space and clicks `Merge` again to the same folder; it succeeds. **No re-adding, no re-ordering, no lost work — the app held everything through the failure.**

*Window-close variant:* if David tried to close the window mid-merge, `Stop the merge?` would appear — `Keep merging` keeps it running; `Close anyway` cancels the merge (a partial output file may remain).
