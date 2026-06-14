---
name: PDF Junior
status: final
sources:
  - ../../prds/prd-pdf-junior-2026-06-14/prd.md
  - ../../prds/prd-pdf-junior-2026-06-14/addendum.md
  - ../../prds/prd-pdf-junior-2026-06-14/reconcile-ux.md
  - ../../architecture.md
updated: 2026-06-14
---

# PDF Junior — Experience Spine

> Single-window Windows 11 desktop utility. C# / WinUI 3 (Fluent Design inherited wholesale). Paired with `DESIGN.md` (visual identity — zero brand-layer overrides). This spine owns *how it works*.

## Foundation

Single-surface native Windows 11 desktop app. WinUI 3 via Windows App SDK, C# / MVVM (CommunityToolkit.Mvvm). The UI system is Fluent Design, inherited wholesale — `DESIGN.md` documents the inheritance, not overrides. One window, one screen, no navigation chrome. No account, no network, no persistence between sessions. English only for v1.

## Information Architecture

| Surface | Content | Purpose |
|---|---|---|
| Title bar | Native Windows, Mica backdrop | Window chrome — drag, minimize, maximize, close |
| Sidebar (left pane) | Scrollable File list (`ListView`, single-select) | Ordered list of added PDFs; each item shows filename + validation status/page count |
| Drag divider | Vertical resize handle between sidebar and preview | User-adjustable pane split; resets each launch |
| Preview toolbar | **[Move up] [Move down]** (grouped) — gap — **[Remove]** | Reorder and remove the Selected file; all actions require a selection |
| Preview pane (right) | Read-only rendered PDF pages, fit-to-width, vertical scroll | View the Selected file's content; placeholder states when nothing selected, checking, or flagged |
| Progress indicator | Thin `ProgressBar` above the Action bar | Visible only during merges exceeding 2 seconds; determinate by file count |
| Action bar (bottom) | Right-aligned: **[Add PDF(s)] [Merge]** | Primary actions — add files and trigger merge |

**Transient surfaces** (overlay or inline, never separate screens):
- Native `FileOpenPicker` (Add PDF(s)) — multi-select, `.pdf` filter.
- Native `FileSavePicker` (Merge) — filename + folder + overwrite in one step.
- `ContentDialog` (window-close guard during merge).
- `InfoBar` (success banner, error banner) — inline, at most one visible at a time.

**Window constraints:** minimum 640×480, default 900×640, resizable and maximizable.

The entire IA is in-place transitions within one persistent window. No tabs, no navigation rail, no sidebar nav, no pages, no routing.

**Sidebar drag-resize:** min width 200px, max 50% of window, default 280px. Drag affordance is a standard WinUI GridSplitter or equivalent. Double-click the divider to reset to default width. Width resets on each launch (PRD §9.1 — no persistence).

## Voice and Tone

Microcopy principles. Brand voice and aesthetic posture live in `DESIGN.md`.

### Per-situation tone

| Situation | Tone | Intent |
|---|---|---|
| Empty file list | Warm invitation, single line | The only moment the app speaks unprompted; one calm sentence, no exclamation |
| Empty preview (nothing selected) | Neutral pointer | Tell the user what to do to fill this space; nothing more |
| File checking | Factual, transient | A status word, not a message — it will resolve on its own |
| File valid | Silent | Page count appears; no congratulatory copy |
| File flagged (error) | Factual, non-blaming | State the problem plainly — "Password protected", "Could not read file" — never "Error:", never the user's fault |
| Merge disabled (tooltip) | Factual, actionable | Name the blocking condition and imply the remedy; distinguish empty-list from flagged-files-present |
| Merge in progress | Silent (sub-2 s) or progress bar only (>2 s) | No text, no spinner, no "Merging..." label. Absence of feedback for fast merges is the intended design, not a defect |
| Merge success | Brief, affirming, one optional action | Name the output file, offer Open folder, then get out of the way. No celebration |
| Merge failure | Honest, specific reason when available, **no apology filler** | "Merge failed — Not enough space on E:\." Not "Sorry, something went wrong!" The user needs a reason, not sympathy |
| Overwrite warning | Neutral, factual (OS-owned) | The native Save dialog handles this; the app adds nothing |
| Close-during-merge guard | Calm warning, default is safe | The default button keeps merging; closing is the deliberate second option |

### General rules

- **Silent by default.** The only instructional copy is the empty file list and empty preview placeholders. Every other state communicates through control states (enabled/disabled, selected/unselected) and inline status labels.
- **No apology filler.** "Merge failed — Access denied" not "We're sorry, but the merge could not be completed."
- **No developer jargon.** "Could not read file" not "Parse error"; "Password protected" not "Encrypted document detected."
- **No exclamation marks.** The app is calm.
- **Specific failure reasons are the expected default.** The generic fallback ("Merge failed. Try again or check the files.") fires only when the underlying error provides no actionable detail. Every mapped failure reason names the specific problem.

## Microcopy Inventory

Canonical strings. Implementation must match these verbatim (`UiStrings.cs` in the architecture). Final wording — the UX spec owns these.

| ID | Context | String | Style | Notes |
|---|---|---|---|---|
| MC-1 | Empty file list placeholder | Add PDFs to get started | Body | Centered in sidebar when File list is empty |
| MC-2 | Empty preview placeholder | Select a file to preview it | Body | Centered in preview pane when nothing selected |
| MC-3 | List item — checking status | Checking… | Caption | Transient; resolves to valid or error |
| MC-4 | List item — valid status | {N} pages | Caption | Singular: "1 page" |
| MC-5 | List item — error-password | Password protected | Caption, critical color | |
| MC-6 | List item — error-corrupt | Could not read file | Caption, critical color | |
| MC-7 | Preview — checking placeholder | Checking… | Body | Centered in preview pane while selected file is validating |
| MC-8 | Preview — password exclusion | This file is password protected and will be excluded from the merge. | Body | Centered in preview pane; no preview content rendered |
| MC-9 | Preview — corrupt exclusion | This file could not be read and will be excluded from the merge. | Body | Centered in preview pane; no preview content rendered |
| MC-10 | Merge disabled tooltip — no valid files | Add at least one PDF to merge | Caption | Shown on hover/focus of disabled Merge button when list is empty or all items are flagged |
| MC-11 | Merge disabled tooltip — flagged files present | Remove files with errors before merging | Caption | Shown when ≥1 flagged file is present (distinct from MC-10) |
| MC-12 | Merge disabled tooltip — still checking | Waiting for files to finish checking | Caption | Shown when ≥1 file is still in checking state |
| MC-13 | Success banner | Merged successfully — {filename} | Body | InfoBar Severity=Success; auto-dismiss ~8 s; manually closable |
| MC-14 | Success banner action | Open folder | Button label | Opens File Explorer to the output folder |
| MC-15 | Error — disk full | Merge failed — Not enough space on {drive}. | Body | InfoBar Severity=Error; manual dismiss only |
| MC-16 | Error — access denied | Merge failed — Access denied | Body | |
| MC-17 | Error — source file missing | Merge failed — File not found: {name} | Body | |
| MC-18 | Error — generic fallback | Merge failed. Try again or check the files. | Body | Only when no specific reason is available |
| MC-19 | Open folder — folder gone | Folder not found | Body | Replaces the explorer launch when the output folder no longer exists |
| MC-20 | Close guard — dialog title | Merge in progress | Subtitle | ContentDialog title |
| MC-21 | Close guard — dialog body | A merge is still running. Closing now may leave an incomplete file at the destination. | Body | |
| MC-22 | Close guard — primary button (default) | Keep merging | Button | Default action; safe choice |
| MC-23 | Close guard — secondary button | Close anyway | Button | Destructive; cancels the merge cooperatively |

## Component Patterns

Behavioral. Visual specs live in `DESIGN.md.Components`.

| Component | Use | Behavioral rules |
|---|---|---|
| **File list** (`ListView`) | Sidebar | Single-select. Clicking an item selects it and loads its preview (FR-5). Selection and preview are always in sync. Items display filename (Body) + validation status or page count (Caption). Newly added files append to the bottom. No auto-scroll — the list does not scroll to reveal newly added items; the user scrolls manually if needed. Duplicate paths (case-insensitive) are silently skipped. |
| **List item** | File list row | Shows filename and a caption-styled secondary line: "Checking…" / "{N} pages" / "Password protected" / "Could not read file". Selectable, removable, and reorderable at all times regardless of validation status (including while checking). Flagged-file captions use the critical color (`{colors.critical}`). |
| **Preview pane** (`ScrollViewer`) | Right pane | Read-only. Renders PDF pages as bitmaps fit-to-width with vertical scroll only. No editing, annotation, zoom, or page controls. When the selected file is flagged, the pane shows the exclusion notice (MC-8 or MC-9) centered — no preview content is rendered, and the notice alone is sufficient feedback. When nothing is selected: MC-2. When the file list is empty: sidebar shows MC-1. |
| **Preview toolbar** | Top of right pane | Right-aligned: **[Move up] [Move down]** grouped, then a visual gap, then **[Remove]**. All three act on the Selected file. All disabled when nothing is selected. Move up disabled at position 1; Move down disabled at last position. The gap between Move and Remove prevents accidental removal. |
| **Move up / Move down** | Preview toolbar | Move the Selected file one position in the File list. **Selection follows the moved item** — it stays selected at its new position. **The preview pane content is unchanged** (same file, no reload, no flicker). The File list visually reorders. |
| **Remove** | Preview toolbar | Removes the Selected file from the File list. After removal, selection clears — no neighbor is auto-selected. Preview pane returns to MC-2 state. |
| **Add PDF(s)** button | Action bar | Opens the native `FileOpenPicker` filtered to `.pdf`, multi-select enabled. Selected files are appended to the File list; each enters *checking* status (FR-2). Cancelling the picker is a silent no-op. Always enabled. |
| **Merge** button | Action bar | `AccentButtonStyle`. Enabled only when ≥1 valid file, 0 flagged files, 0 checking files (FR-10). When disabled, a tooltip explains why (MC-10, MC-11, or MC-12 — three distinct messages for three distinct blocking conditions). Pressing Merge **immediately dismisses any visible success/error banner** before opening the Save dialog. |
| **InfoBar** (success) | Inline banner | Severity=Success. Shows MC-13 with an **Open folder** action button (MC-14). Auto-dismisses after ~8 seconds; manually closable before then. At most one banner (success or error) visible at a time. |
| **InfoBar** (error) | Inline banner | Severity=Error. Shows the specific failure reason (MC-15–MC-18). Manual dismiss only — no auto-dismiss. At most one banner visible at a time. |
| **ProgressBar** | Above Action bar | Thin, determinate (progress by file count — architecture owns the per-file import loop). Appears **only** after a merge has been running for 2 seconds. Sub-2-second merges intentionally show no progress affordance at all — no flash, no spinner, no "Merging…" text. Absence of feedback is the designed behavior. |
| **ContentDialog** (close guard) | Overlay | Appears when the user attempts to close the window during a merge (FR-12). Title: MC-20. Body: MC-21. Primary (default): MC-22 ("Keep merging"). Secondary: MC-23 ("Close anyway"). Close-anyway cancels the merge cooperatively; a partial file may remain. |
| **Drag divider** | Between sidebar and preview | Resizable. Double-click resets to default width (280px). Cursor changes to col-resize on hover. Min 200px, max 50% of window (per IA section). Resets on launch. |

## State Patterns

| State | Surface | Treatment |
|---|---|---|
| **App launch** | Whole window | Empty file list → sidebar shows MC-1 ("Add PDFs to get started"). Preview pane shows MC-2 ("Select a file to preview it"). Merge disabled (tooltip: MC-10). No splash screen, no onboarding, no what's-new. |
| **Files added, checking** | Sidebar + preview | New items appear at bottom with "Checking…" caption. If a checking item is selected, preview shows MC-7. Merge disabled (tooltip: MC-12) until all items resolve. |
| **Files validated, none selected** | Sidebar + preview | Items show page counts. Preview shows MC-2. Merge enabled if ≥1 valid, 0 flagged. |
| **File selected (valid)** | Preview pane | PDF pages rendered fit-to-width, vertical scroll. Preview toolbar buttons enabled per position. |
| **File selected (flagged)** | Preview pane | Exclusion notice (MC-8 or MC-9) centered in the preview pane. No preview content — the notice alone is sufficient. The file is still selectable and removable. |
| **File removed** | Sidebar + preview | Item disappears from list. Selection clears. Preview returns to MC-2. If the list is now empty, sidebar shows MC-1. Merge-enabled recalculated. |
| **File reordered** | Sidebar + preview | Item moves one position. Selection follows the moved item. **Preview content is unchanged** — no reload, no flicker. |
| **Merge pressed** | Whole window | Any visible success/error banner is **immediately dismissed**. The native Save dialog opens (default filename "merged.pdf", `.pdf` filter). The app remains interactive while the dialog is open — no lock. |
| **Save dialog cancelled** | Whole window | Silent no-op. No error, no output. UI returns to idle. Window-close guard not engaged. |
| **Merge executing (< 2 s)** | Action bar + content | UI locked: File list read-only, Add/Merge/toolbar disabled. Preview pane stays scrollable. **No progress affordance** — the merge finishes before the user would notice. |
| **Merge executing (≥ 2 s)** | Progress indicator | Thin determinate `ProgressBar` appears above the Action bar. UI remains locked. Preview pane stays scrollable. Window-close guard (FR-12) is engaged. |
| **Merge success** | Banner | InfoBar (Success): MC-13 + **Open folder** (MC-14). Auto-dismisses ~8 s. File list preserved — user can merge again or adjust and re-merge. UI unlocked. |
| **Merge failure** | Banner | InfoBar (Error): MC-15, MC-16, MC-17, or MC-18. Manual dismiss. File list preserved for retry. UI unlocked. A partial/incomplete file may remain at the destination. |
| **Open folder — folder gone** | Banner area | MC-19 ("Folder not found") shown inline instead of launching Explorer. |
| **Close during merge** | Dialog overlay | ContentDialog: MC-20/21/22/23. Default is "Keep merging" (safe). "Close anyway" cancels merge cooperatively; a partial file may remain. |
| **Validation timeout** | List item | A file that has not resolved after 5 seconds is treated as a parse failure → *error-corrupt* ("Could not read file"). This prevents a permanently stuck, non-interactive item in the list. |

## Interaction Primitives

PDF Junior is a mouse-and-touch utility for a general audience. No custom keyboard shortcuts are defined for v1. The app relies on:

- **WinUI's built-in keyboard support:** Tab navigation between controls, Space/Enter to activate buttons, arrow keys in the ListView, Escape to close dialogs.
- **Mouse:** click to select list items, click buttons to act, scroll the preview pane, drag the sidebar divider.
- **Touch:** tap equivalents of mouse actions on touch-enabled Windows devices.
- **File picker keyboard:** the native `FileOpenPicker` and `FileSavePicker` have full keyboard support from Windows.

No vim-style shortcuts, no command palette, no drag-to-reorder. The interaction surface is deliberately small — buttons and clicks.

**Banned:** drag-to-reorder (decided — Move up/down buttons instead), hover-only affordances (all actions are button-click), right-click context menus (no contextual actions beyond the toolbar), keyboard shortcuts that require learning.

## Accessibility Floor

Accessibility is **not a v1 requirement** (PRD §5). No committed keyboard-only guarantees, Narrator/screen-reader announcements, WCAG conformance, or explicit focus management.

Native WinUI controls retain Windows' baseline automation support (UIA properties, basic Tab order), but no accessibility work is scoped:
- No custom `AutomationProperties.Name` or `LiveSetting` annotations.
- No focus-management after state transitions (e.g., after Remove or after merge completion).
- No high-contrast theme testing.
- No screen-reader testing.

This is a deliberate scope decision, not an oversight. Accessibility may be added in a future version.

## Key Flows

### Flow 1 — Marcus merges four handouts (first use)

Marcus, a middle-school teacher, has four PDF handouts he made this morning — chapter summaries for tomorrow's class. He wants one file to upload to the class portal before his next period starts in twelve minutes.

1. He launches PDF Junior from the Start menu. The window opens in under three seconds: an empty sidebar that says "Add PDFs to get started" and a blank preview pane.
2. He clicks **Add PDF(s)**. The native file picker opens to his Documents folder. He multi-selects the four handouts and clicks Open.
3. Four items appear in the sidebar, each briefly showing "Checking…" and then resolving: "4 pages", "3 pages", "2 pages", "6 pages."
4. He clicks the third file to check it — the preview pane fills with a fit-to-width render of the handout. He notices it should come second. He clicks **Move up** — the item slides to position two, stays selected, and the preview doesn't flicker.
5. **Climax:** He clicks **Merge**. The Save dialog opens with "merged.pdf" pre-filled. He keeps the name, navigates to the Desktop, and clicks Save. The merge finishes in under a second — no progress bar, no spinner, just a green banner: "Merged successfully — merged.pdf." He clicks **Open folder**; Explorer opens to the Desktop. Four PDFs, one click, done. He uploads the file before the bell rings.

### Flow 2 — Sophie removes two unreadable files (error recovery)

Sophie, a paralegal, is assembling a document package for a filing. She has six PDFs from different sources — four from the firm's scanner, one from a client email, one from opposing counsel.

1. She adds all six via the file picker. Four resolve to valid with page counts. One shows "Password protected" (the client's file was encrypted). One shows "Could not read file" (the opposing counsel attachment was a corrupted export).
2. She clicks the password-protected file. The preview pane shows: "This file is password protected and will be excluded from the merge." The Merge button is disabled; its tooltip reads "Remove files with errors before merging."
3. She selects the password-protected file and clicks **Remove**. Then selects the corrupt file and clicks **Remove**. Four valid files remain.
4. **Climax:** Merge enables. She clicks it, saves as "filing-package.pdf" to her case folder, and the merge succeeds. Sophie exhales — the flagged files were surfaced clearly, never silently included, and removing them took two clicks. The package is clean.

### Flow 3 — Marcus overwrites a previous output (intentional overwrite)

Marcus has already merged once today. He reordered two files and wants to produce an updated version at the same location with the same name.

1. He clicks **Merge**. The Save dialog opens (default "merged.pdf"). He navigates to the Desktop where the earlier `merged.pdf` sits.
2. He keeps the same filename. The OS Save dialog warns: "merged.pdf already exists. Do you want to replace it?"
3. **Climax:** He confirms. The merge runs; the old file is overwritten. The success banner confirms "Merged successfully — merged.pdf." The overwrite decision was the OS's dialog, not the app's — PDF Junior never second-guesses the user.

### Flow 4 — David recovers from a failed merge (disk error)

David, a home user, has five valid scans of family medical records. He wants one file for his records folder on a USB drive.

1. He adds the five scans. All valid. He clicks **Merge**, and in the Save dialog navigates to his USB drive (E:\) and names the file "medical-records.pdf."
2. The merge starts. After two seconds a thin progress bar appears. Partway through, the write fails — the USB drive is full.
3. An error banner appears: "Merge failed — Not enough space on E:\." The banner stays until he dismisses it (no auto-dismiss on errors). His file list is intact — all five files, in order, still there.
4. He frees space on the USB drive, comes back to PDF Junior, and clicks **Merge** again. The stale error banner is immediately dismissed when he presses Merge. The Save dialog opens again; he picks the same folder and name.
5. **Climax:** The merge completes. "Merged successfully — medical-records.pdf." The app held his work through the failure — no re-adding files, no re-ordering, no lost state. He clicks **Open folder** and the Explorer window appears on his USB drive.
