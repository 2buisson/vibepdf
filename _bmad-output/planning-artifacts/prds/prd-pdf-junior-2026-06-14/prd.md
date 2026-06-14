---
title: "PDF Junior"
status: final
created: 2026-06-14
updated: 2026-06-14
---

# PRD: PDF Junior

## 0. Document Purpose

This PRD is for the PDF Junior PM, stakeholders, and the downstream UX, architecture, and epics workflows. It is a **fresh PRD created from the legacy planning set** (`../pdfjunior-legacy/_bmad-output/planning-artifacts`), re-targeting the product from its original React Native for Windows implementation to a **native C# / WinUI 3** stack. Product scope is intentionally unchanged from the legacy plan — this is a faithful re-platform, not a re-scope. Vocabulary is anchored in the §3 Glossary; features are grouped with globally numbered FRs nested; assumptions are tagged inline as `[ASSUMPTION]` and indexed in §13. Concrete technology choices (PDF library, rendering control, packaging) live in the companion `addendum.md`, not here. A fresh UX spec will be produced downstream via `bmad-ux`; the user journeys and information architecture below are captured at PRD level to feed it.

## 1. Vision

PDF Junior is a **free, no-frills Windows 11 desktop app**, distributed through the Microsoft Store, that lets anyone **merge multiple PDF files into a single document** — locally, with no internet connection, no account, and no ads. It does one thing and does it well, then gets out of the way.

Windows has no built-in PDF merger, and the alternatives are all compromised: Adobe Acrobat is subscription-gated; web tools require uploading sensitive contracts, medical records, and financial documents to third-party servers; many "free" mergers watermark the output, cap file counts, or reveal a paywall only after the merge; and full suites like PDF24 (~400 MB) or PDFgear (~200 MB) demand a heavyweight install for a single operation. The unmet gap is clear: **local processing + no watermarks + no account + a clean modern UI** at a lightweight install footprint.

PDF Junior fills that gap as a permanent, privacy-first utility. Everything happens on the user's device; nothing leaves it. Re-platformed to native C# / WinUI 3, it inherits Windows' Fluent Design wholesale, stays under 100 MB installed, and reaches an interactive state in seconds — earning strong Store ratings through restraint and reliability rather than feature count.

## 2. Target User

**Anyone on Windows 11 who needs to combine PDFs.** No demographic, no technical expertise, no account, no configuration, no learning curve. The value is universal: the office worker combining scanned invoices into one submission, the student merging assignment sections before uploading to a portal, the home user digitising and organising documents.

### 2.1 Jobs To Be Done

- **Functional:** "Combine several PDFs into one file, in an order I control, and save it where I want."
- **Emotional:** "Do it without worrying that my documents are being uploaded somewhere or that I'll be hit with a watermark or paywall at the end."
- **Contextual:** "Get it done quickly on my own PC, offline, without installing a 400 MB suite or creating an account."

### 2.2 Non-Users (v1)

- People who need to **edit, annotate, split, compress, or reorder pages within** a PDF — PDF Junior merges whole files only.
- People wanting cloud sync, mobile, or web access.
- People with **password-protected PDFs** who expect the app to unlock them — these are detected and excluded, not supported.

### 2.3 Key User Journeys

*Named-persona narratives the product enables. Numbered UJ-1 through UJ-4, mirrored from the legacy UX experience set. FRs reference these by ID. The fresh UX spec will re-author the detailed flows; these capture intent.*

- **UJ-1. Marcus merges four handouts for the first time.**
  - **Persona + context:** Marcus, a teacher, has four PDF handouts he wants as one file to upload to the class portal.
  - **Entry state:** Fresh launch, empty file list, "Add PDFs to get started" placeholder.
  - **Path:** Clicks **Add PDF(s)** → multi-selects four PDFs in the native file picker → each appears in the file list showing a brief *checking* state, then resolves to *valid* with a page count → clicks one to preview it → selects a misordered file and clicks **Move up** (selection follows the moved file).
  - **Climax:** Clicks **Merge**; the native Save dialog opens with the filename pre-filled as `merged.pdf`. He keeps the name, navigates to the Desktop, and clicks **Save**; the merge runs (progress shown only if it exceeds 2 s); a success banner reads "Merged successfully — merged.pdf."
  - **Resolution:** Clicks **Open folder**; File Explorer opens to the output. The file list is preserved.

- **UJ-2. Sophie removes two unreadable files before merging.**
  - **Persona + context:** Sophie, a paralegal, adds six PDFs; two are problematic.
  - **Path:** One file resolves to *Password protected*, one to *Could not read file*. Sophie selects each flagged file to see why (the preview pane shows an inline exclusion notice), then selects and **Remove**s each from the preview toolbar.
  - **Climax:** Five valid files remain; **Merge** re-enables and succeeds.
  - **Resolution:** A clean merge of the valid files; the flagged files were never silently included.

- **UJ-3. Marcus intentionally overwrites a previous output.**
  - **Path:** Marcus reorders files, clicks **Merge**, and in the native Save dialog navigates to the same Desktop folder where a `merged.pdf` already exists, keeping that name.
  - **Climax:** The Save dialog warns that the file already exists and asks whether to replace it. He confirms **Replace**; the merge runs.
  - **Resolution:** The existing file is overwritten only because he confirmed it in the OS dialog.

- **UJ-4. David recovers from a failed merge without losing his work.**
  - **Persona + context:** David, a home user, adds five valid scans.
  - **Path:** Clicks **Merge**, types a custom filename in the native Save dialog, and selects a USB drive that turns out to be full; the merge fails. An error banner reads "Merge failed — Not enough space on E:\." (manual dismiss). His file list is preserved.
  - **Climax:** He frees space and clicks **Merge** again to the same folder; it succeeds.
  - **Resolution:** Recovery with no re-adding of files. **Note:** a partial or incomplete output file may remain at the destination after the failure; the app does not guarantee its removal, and the user can overwrite it on a successful retry.

## 3. Glossary

*Downstream workflows must use these terms verbatim. No synonyms elsewhere in the PRD.*

- **PDF Junior** — the product: a single-window Windows 11 desktop app that merges PDFs.
- **File list** — the ordered, scrollable list of PDFs the user has added, shown in the left sidebar. Merge output reflects this order.
- **List item** — one PDF entry in the File list. Has a filename, a page count when known, and a Validation status.
- **Validation status** — the state of a List item: *checking*, *valid*, *error-password*, or *error-corrupt*. The internal state tokens map to canonical user-facing microcopy — *error-password* → "Password protected", *error-corrupt* → "Could not read file" (final wording owned by the UX spec).
- **Flagged file** — a List item with Validation status *error-password* or *error-corrupt*. Excluded from Merge; blocks Merge while present.
- **Valid file** — a List item with Validation status *valid*.
- **Selected file** — the single currently selected List item (single-select). Its content drives the Preview pane and is the target of Preview toolbar actions.
- **Preview pane** — the read-only render area for the Selected file.
- **Preview toolbar** — the controls above the Preview pane: **Move up**, **Move down** (grouped), and **Remove**, all acting on the Selected file.
- **Action bar** — the bottom bar containing **Add PDF(s)** and **Merge**.
- **File picker** — the native Windows open-file dialog invoked by **Add PDF(s)**, filtered to `.pdf`, supporting multi-select.
- **Save dialog** — the native Windows save-file picker invoked by **Merge**, where the user chooses the destination folder and the output filename (pre-filled as `merged.pdf`) in one step. The OS enforces filename validity and handles overwrite confirmation.
- **Output filename** — the name for the merged PDF, set in the Save dialog. Defaults to `merged.pdf`.
- **Merge** — combining every Valid file, in File list display order, into one PDF written to the location chosen in the Save dialog.

## 4. Features

### 4.1 File List Management

**Description:** The left sidebar holds the File list. Users add PDFs through the native Windows file picker, and each new List item is validated asynchronously. Users remove and reorder List items before merging. Selecting a List item drives the Preview pane (§4.2). Realizes UJ-1, UJ-2.

**Functional Requirements:**

#### FR-1: Add PDF files

The user can add one or more PDFs via the File picker; multiple selection in a single invocation is supported. Selected files are appended to the end of the File list. Realizes UJ-1.

**Consequences (testable):**
- The picker filters to `.pdf` files.
- Selecting multiple files in one picker invocation appends all of them, preserving picker order, to the bottom of the File list.
- Duplicate files (same absolute path, case-insensitive per NTFS semantics) are silently skipped, not added twice.
- Cancelling the picker is a silent no-op — no items added, no error.
- Each newly added List item enters *checking* Validation status (FR-2).

#### FR-2: Validate added files

Each added List item is validated asynchronously to determine its Validation status. Realizes UJ-2.

**Consequences (testable):**
- A List item begins in *checking* status and resolves to *valid*, *error-password*, or *error-corrupt*.
- A file that cannot be parsed as a valid PDF resolves to *error-corrupt*, shown as "Could not read file" (e.g., an image renamed to `.pdf`).
- A password-protected/encrypted file resolves to *error-password*, shown as "Password protected".
- A *valid* List item displays its page count.
- A validation that does not complete is treated as a parse failure and resolves to *error-corrupt* ("Could not read file"); a per-file wall-clock guard bounds an unresponsive parse. `[ASSUMPTION: the guard is per-file wall-clock and its exact threshold is tuned in architecture so large but valid PDFs are not false-flagged (NFR-1); the legacy value was 30 s.]`
- A List item remains selectable, removable, and reorderable at any time regardless of Validation status (including while *checking*).

#### FR-3: Remove a file

The user can remove any List item before merging by selecting it and using **Remove** in the Preview toolbar. Realizes UJ-2.

**Consequences (testable):**
- **Remove** is enabled only when a file is selected.
- After removal, selection clears (no neighbor is auto-selected); the Preview pane returns to its "Select a file to preview it" state.
- A Flagged file can be selected and removed exactly like any other List item.

#### FR-4: Reorder files

The user can change the position of the Selected file using **Move up** and **Move down** buttons in the Preview toolbar. Merge output reflects File list order. Realizes UJ-1, UJ-3.

**Consequences (testable):**
- **Move up** / **Move down** act on the Selected file; selection follows the moved item.
- The Preview pane continues to show the moved (still-Selected) file without reloading or flicker.
- **Move up** is disabled when the Selected file is first; **Move down** is disabled when it is last; both are disabled when nothing is selected.
- Reordering changes the order in which files are combined by Merge (FR-8).

**Notes:** Reorder is **Move up / Move down buttons** (decided — carries the legacy 2026-06-10 reversal of an earlier drag-and-drop design). The control is fixed; drag-reorder is not in scope.

### 4.2 PDF Preview

**Description:** The right pane shows a read-only preview of the Selected file — fit-to-width, vertical scroll only, no editing, annotation, page selection, or manipulation. Preview is a **confirmed v1 capability**: native C#/WinUI renders PDFs directly via the platform PDF API (see `addendum.md`), so the legacy "deferrable spike" risk does not carry over. Realizes UJ-1, UJ-2.

**Functional Requirements:**

#### FR-5: Preview the selected file

The user can view a read-only preview of the Selected file in the Preview pane. Realizes UJ-1.

**Consequences (testable):**
- Clicking a List item selects it and loads its preview; selection and preview are always in sync (single-select).
- A *valid* Selected file renders fit-to-width with vertical scroll only; no editing/annotation/page controls are present.
- When the Selected file is still *checking*, the Preview pane shows a "Checking…" placeholder instead of a preview.
- When the Selected file is a Flagged file, the Preview pane shows an inline exclusion notice naming the reason ("Password protected" or "Could not read file"); no preview content is required (an encrypted file generally cannot be rendered).
- With nothing selected, the Preview pane shows "Select a file to preview it"; with an empty File list, the sidebar shows "Add PDFs to get started."

### 4.3 Merge and Output

**Description:** A single **Merge** action combines all Valid files in display order into one PDF. It opens the native Save dialog so the user picks the destination folder and filename (default `merged.pdf`) in one step — with the OS enforcing filename validity and overwrite confirmation — then reports success with a shortcut to the output. Merge runs off the UI thread so the app stays responsive on large inputs. Realizes UJ-1, UJ-3, UJ-4.

**Functional Requirements:**

#### FR-6: Trigger the merge

The user initiates the merge via the **Merge** button in the Action bar. Realizes UJ-1.

**Consequences (testable):**
- **Merge** is enabled only when the File list contains at least one Valid file, no Flagged files remain, and no file is still *checking* (see FR-10).
- The UI lock applies to the *merge-execution* phase (after the Save dialog returns a destination), not while the Save dialog is open: during execution the File list is read-only and the Preview toolbar, **Add PDF(s)**, and **Merge** are disabled (the Preview pane stays scrollable).

#### FR-7: Choose the output destination

On **Merge**, the native Windows Save dialog opens; the user selects the destination folder and the output filename in one step. Realizes UJ-1, UJ-3, UJ-4.

**Consequences (testable):**
- The Save dialog pre-fills the filename as `merged.pdf` and is configured for the `.pdf` file type (the OS appends `.pdf` if absent).
- The user can edit the filename in the dialog; the OS rejects invalid filenames (illegal NTFS characters, reserved device names) — the app performs no separate filename validation.
- If a file of the chosen name already exists in the chosen folder, the OS Save dialog asks the user to confirm replacement; no silent overwrite occurs.
- Clicking **Merge** opens the Save dialog first; the merge (FR-8) begins only after the dialog returns a confirmed destination. Cancelling the dialog aborts silently (no error, no output) and returns the app to its idle pre-Merge state with the UI unlocked and the window-close guard (FR-12) not engaged.
- The app does not itself persist or restore the destination between sessions; the dialog uses OS defaults. `[ASSUMPTION: carries the spirit of legacy decision #11 — the app keeps no memory of the last output location; the native dialog's own MRU is OS behavior, not app state.]`

#### FR-8: Execute the merge

All Valid files in the File list are combined in display order into a single PDF written to the chosen location. Realizes UJ-1.

**Consequences (testable):**
- The output contains every Valid file's pages, in File list order; Flagged files are excluded.
- A single Valid file is a valid merge (it produces a copy at the chosen destination).
- The merge runs off the UI thread; the app remains responsive throughout (NFR-1).
- If the merge has not completed within 2 seconds of starting, a progress indicator is displayed until the merge completes, fails, or is cancelled; merges that finish within 2 seconds show no progress affordance.
- The progress indicator is determinate when the merge engine can report per-file progress, otherwise indeterminate. `[ASSUMPTION: determinacy depends on the chosen PDF library's API and is resolved in architecture.]`

#### FR-9: Report success

On success, the user receives an inline success banner with an option to open the output folder. Realizes UJ-1.

**Consequences (testable):**
- The success banner names the output file and auto-dismisses after ~8 seconds; it can also be dismissed manually before then (standard `InfoBar` close affordance).
- At most one banner (success or error) is shown at a time; starting a new Merge clears any visible banner.
- **Open folder** opens File Explorer to the output folder; if the folder no longer exists, an inline "Folder not found" message is shown instead of failing.
- The File list is preserved after a successful merge (so a second output can be produced without re-adding files).

### 4.4 Error Handling and Safety

**Description:** PDF Junior never silently produces a wrong result, and never silently includes a Flagged file. Unsupported and unreadable files are flagged and excluded; merge failures are explained (a partial output file may remain on disk); closing the window mid-merge is guarded. Realizes UJ-2, UJ-4.

**Functional Requirements:**

#### FR-10: Block merge on invalid state

**Merge** is enabled only when the File list contains at least one Valid file, zero Flagged files, and no file still *checking*; otherwise it is disabled. Realizes UJ-2.

**Consequences (testable):**
- With at least one Flagged file present, **Merge** is disabled until every Flagged file is removed.
- With zero Valid files (empty or all-flagged list), **Merge** is disabled.
- While any file is still *checking*, **Merge** is disabled, so a file whose validity is not yet known is never silently excluded.
- A disabled **Merge** explains why via a hover tooltip (acceptable under §10 "silent by default"), distinguishing the no-valid-file case from the flagged-file-present case.

#### FR-11: Handle merge failure

On failure (e.g., disk full, write permission denied, source file disappeared), the user receives a descriptive error. A partial or incomplete output file may remain at the destination; the app does not guarantee its removal. Realizes UJ-4.

**Consequences (testable):**
- The error banner states a specific reason when available (e.g., "Not enough space on E:\.", "Access denied", "File not found: {name}") and otherwise a generic fallback; it is dismissed manually (no auto-dismiss).
- Output is **not** guaranteed to be atomic: a failed or cancelled merge may leave a partial, incomplete, or zero-byte file at the chosen destination. The app performs no temp-file staging or rollback. `[Decision 2026-06-14: supersedes the earlier no-partial-output / atomic-write-then-move guarantee — partial output is now permitted on failure.]`
- After a failure the UI unlocks: the File list, Preview toolbar, **Add PDF(s)**, and **Merge** are re-enabled so the user can retry.
- The File list is preserved for retry.

#### FR-12: Guard window close during merge

If the user attempts to close the window while a merge is running, they are asked to confirm. Realizes UJ-4.

**Consequences (testable):**
- A confirmation dialog offers **Keep merging** (default) and **Close anyway**.
- **Close anyway** cancels the in-progress merge (cancellation is cooperative); a partial or incomplete output file may remain at the destination (see FR-11 — no atomicity guarantee).

## 5. Non-Goals (Explicit)

PDF Junior is a **merge-only** utility. It is not, and in v1 will not become:

- A PDF **editor, annotator, or form filler**.
- A tool for **page-level** operations — page-range selection, page reordering within a file, splitting, or extraction.
- A **compression** tool.
- A tool that **opens or unlocks password-protected** PDFs.
- A **cloud / network** product — no upload, sync, or network storage integration.
- A **multi-language** app (English only for v1).
- A **mobile or web** product.
- A **monetized** product — no paid tier, in-app purchases, or ads, ever. `[NON-GOAL for MVP and beyond]`
- An **accessibility-targeted** product in v1 — no committed keyboard-only guarantees, Narrator/screen-reader announcements, WCAG conformance, or explicit focus management. Native WinUI controls retain Windows' baseline automation support, but no accessibility work is scoped for v1.

## 6. MVP Scope

### 6.1 In Scope

- Add PDFs via native file picker (multi-select, append, duplicate-skip).
- Asynchronous per-file validation with *checking* / *valid* / *error-password* / *error-corrupt* states and page counts.
- Remove and reorder (Move up/down buttons) List items; selection drives preview.
- Read-only in-app PDF preview (**confirmed v1**).
- Merge of Valid files in order, off the UI thread, with progress indicator over 2 s.
- Native Save dialog for output (folder + filename, default `merged.pdf`; OS-enforced validity and overwrite confirmation).
- Success banner with **Open folder**.
- Error handling: flagged files block merge; descriptive merge-failure errors (a partial output file may remain); window-close-during-merge guard.
- MSIX packaging, Windows 11 minimum, Microsoft Store submission, public privacy policy.

### 6.2 Out of Scope for MVP

- **Page-range selection** — deferred to a possible v1.1. `[NOTE FOR PM]` This is the one feature the legacy plan named as a "future consideration"; flag for revisit if there is demand.
- PDF splitting, compression, editing/annotation — not planned.
- Password-protected PDF support — detected and excluded only.
- Cloud / network storage integration — not planned.
- Multi-language UI — English only for v1.
- Mobile or web versions — not planned.
- Paid tier, IAP, or ads — permanently out.
- Accessibility support — not a v1 requirement (see §5).

## 7. Success Metrics

*Each SM cross-references the FR(s)/NFR(s) it validates. Counter-metrics counterbalance specific primary or secondary metrics.*

**Primary**
- **SM-1 — Store rating:** Microsoft Store rating ≥ 4.0 within 3 months of launch (after ≥ 20 reviews). Validates the product's core promise of reliability and simplicity (FR-1…FR-12 collectively).
- **SM-2 — Crash-free sessions:** ≥ 99% crash-free session rate. Validates the product holistically (notably FR-2, FR-5, FR-8, FR-11).

**Secondary**
- **SM-3 — Merge success rate:** ≥ 98% successful merges on non-password-protected PDFs. Validates FR-8, FR-11, and the *valid*-detection path of FR-2.
- **SM-4 — Adoption:** 1,000 Store downloads in the first 30 days. Validates the vision/positioning (§1).

**Counter-metrics (do not optimize)**
- **SM-C1 — Feature count:** Do **not** grow scope to chase ratings; simplicity is the differentiator. Counterbalances SM-1 and SM-4.
- **SM-C2 — Session duration:** Do **not** optimize for time-in-app; the app should get the user to a merged file and out of the way fast. Counterbalances any engagement framing of SM-4.

## 8. Cross-Cutting NFRs

- **NFR-1 — Performance / non-blocking:** No file-count or file-size limit. Merge runs off the UI thread (never on the UI dispatcher); during a merge of arbitrarily large input the UI thread is never blocked — the window repaints, the Preview pane stays scrollable, and the window-close guard (FR-12) remains responsive (verifiable by interacting with the app mid-merge). A progress indicator appears once a merge has been running 2 seconds (see FR-8); shorter merges show no progress affordance.
- **NFR-2 — Privacy / local-only:** No backend; all processing is local. No network requests in normal operation; no telemetry, analytics, or crash data leaves the device.
- **NFR-3 — Microsoft Store compliance:** Packaged as **MSIX**; capability declarations minimal (file access only, no `broadFileSystemAccess`); must pass Store certification. **Minimum OS: Windows 11.**
- **NFR-4 — No account:** No sign-in, registration, or onboarding flow of any kind.
- **NFR-5 — Startup time:** Reaches interactive state within 3 seconds on a mid-range Windows 11 PC.
- **NFR-6 — Install size:** Installed footprint stays under **100 MB** — a hard constraint and a core competitive differentiator, not a stretch goal.
- **NFR-7 — Privacy policy:** A publicly accessible privacy policy page is produced and linked in the Store listing before submission, confirming local-only processing, no data collection, and no third-party sharing.

## 9. Constraints and Guardrails

### 9.1 Privacy
- Files are reached **only** through user-initiated file/save dialogs — never by raw path; no `broadFileSystemAccess` capability. This is both a privacy stance and a Store-certification driver.
- **No persistence of any kind** by the app between sessions — testable: a relaunch always starts with an empty File list, the default window/sidebar geometry, and no recalled output location. No recent-files list, no remembered window/sidebar geometry, no memory of the last output location. In-memory for the session only.

### 9.2 Platform
- Target: **Windows 11**, native **C# / WinUI 3**, distributed via the Microsoft Store as MSIX. English only for v1. (Implementation specifics — PDF library, rendering control, packaging project — are in `addendum.md`.)

### 9.3 Monetization
- **Permanently free.** No paid tier, no in-app purchases, no ads. This is a positioning commitment, not a v1 deferral.

## 10. Aesthetic and Tone

- **Calm, capable, does one thing well.** A no-frills, privacy-first utility that never shouts. Trust is earned through clarity and restraint.
- **Silent by default.** No instructional copy unless the File list is empty. Microcopy is plain, direct, and never alarmist, in an office-worker/student register — developer jargon ("concatenate", "stdout", "render pipeline") is banned.
- **Tone by situation.** Errors are non-blaming and explanatory; failures state the reason plainly with no apology-filler; success is brief and affirming, then gets out of the way.
- **Respect the OS.** Inherits Fluent Design wholesale: no custom brand palette, no decorative illustration. Uses the user's Windows accent color (on **Merge** and selection only) and follows the OS light/dark theme with no in-app toggle. Mica backdrop on the title bar.
- **Holds the user's work.** The File list persists across both success and failure, so retrying or producing a second output never requires re-adding files.
- **Safe destructive actions.** **Remove** is isolated in the Preview toolbar, requires a Selected file, and is grouped apart from Move up/down to avoid accidental activation. Output overwrite is confirmed by the native Save dialog.
- **Hard anti-patterns (Don'ts):** no splash screen, no onboarding or what's-new modals, no upsell/ads/rating prompts, no account gates, no AI-feature prompts.

## 11. Information Architecture

*A single persistent window with no navigation chrome (no tabs, nav rail, or sidebar nav). All states are in-place transitions within one screen. The fresh UX spec (`bmad-ux`) will detail layout, spacing, and microcopy; this captures the IA shape carried from the legacy plan.*

- **Title bar** — native Windows, Fluent Mica backdrop.
- **Sidebar (left)** — the scrollable File list. Resizable via a drag divider; resets on each launch (no persistence).
- **Preview toolbar (top of right pane)** — right-aligned: **[Move up] [Move down]** (grouped), gap, then **[Remove]** — all acting on the Selected file.
- **Preview pane (right)** — read-only render of the Selected file; fit-to-width, vertical scroll only.
- **Progress indicator** — a thin bar above the Action bar, visible only during a merge exceeding 2 s.
- **Action bar (bottom, full width)** — right-aligned **[Add PDF(s)] [Merge]**. (No filename field — the output name is set in the Save dialog.)
- **Transient surfaces** (overlays/inline, not separate screens): native file picker, native Save dialog (filename + folder + overwrite), window-close-during-merge dialog, success banner, error banner.
- **Window constraints:** minimum 640×480, default 900×640, resizable and maximizable.

*The sidebar drag-resize behavior (min/max widths, drag affordance) is owned by the downstream UX spec; at PRD level only the non-persistence guarantee is fixed — neither sidebar width nor window geometry persists across launches (§9.1). The verbatim microcopy inventory (legacy MC-1…MC-20) is likewise specified by the UX spec, which should reuse the legacy strings as its starting point.*

## 12. Open Questions

1. **PDF library selection (deferred to architecture):** Which .NET PDF library performs the merge, and does it also cover validation (password/corrupt detection) and page-count extraction, or are those handled by the platform PDF API? *(See `addendum.md`; resolve in `bmad-create-architecture`.)*

## 13. Assumptions Index

*Every `[ASSUMPTION]` in this document, surfaced for confirmation:*

- **§4.1 FR-2** — The validation guard is per-file wall-clock; its exact threshold is tuned in architecture so large but valid PDFs are not false-flagged (legacy used 30 s).
- **§4.3 FR-7** — The app keeps no memory of the last output location between sessions (carries the spirit of legacy decision #11; the native Save dialog's own MRU is OS behavior).
- **§4.3 FR-8** — Progress-indicator determinacy (determinate vs. indeterminate) depends on the chosen PDF library's API and is resolved in architecture.

*(The initial draft's assumptions on output-filename handling, reorder control, and accessibility were resolved by user decisions on 2026-06-14 and are no longer open.)*
