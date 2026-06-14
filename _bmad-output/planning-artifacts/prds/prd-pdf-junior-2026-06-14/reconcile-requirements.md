# Requirement Reconciliation — Legacy PRD vs. New Draft (2026-06-14)

**Purpose:** Surface any legacy requirement, capability, edge case, success metric, or constraint that is missing from, or materially weaker in, the new C#/WinUI 3 PRD draft — excluding the five deliberate changes listed in the task.

**Legacy sources reconciled:**
- `pdfjunior-legacy/.../prd-pdf-junior-2026-06-01/prd.md`
- `pdfjunior-legacy/.../prd-pdf-junior-2026-06-01/addendum.md`
- `pdfjunior-legacy/.../epics.md` (treated as authoritative: it carries the UX Design Requirements UX-DR1–27 and microcopy MC-1–20, which encode capability-level detail the source UX docs fed into)

**New draft reconciled:**
- `pdfjunior/.../prd-pdf-junior-2026-06-14/prd.md`
- `pdfjunior/.../prd-pdf-junior-2026-06-14/addendum.md`

**Deliberate changes excluded from gap analysis (intended, not flagged):**
1. Output filename field removed; name+folder chosen in native Save dialog (default `merged.pdf`); OS handles validity + overwrite.
2. No accessibility requirement in v1 (moved to Non-Goals).
3. Reorder = Move up/down buttons (drag-and-drop out of scope).
4. PDF preview promoted to confirmed v1 capability.
5. Tech re-targeted from React Native for Windows to C#/WinUI 3.

A consequence note: because change #2 removes accessibility wholesale, none of the legacy accessibility-specific obligations (Narrator announcements, focus-management transitions G3, WCAG AA contrast, 44×44 hit targets, arrow-key list nav) are flagged — they fall under the deliberate Non-Goal. The gaps below are the residue *after* subtracting all five deliberate changes.

---

## Verdict

The re-platform is largely faithful at the FR level (legacy FR-1…FR-4 all map cleanly to new FR-1…FR-12). The gaps are concentrated in (a) **detailed behavioral contracts** that lived in the legacy UX-DR / microcopy layer and were summarized away, and (b) a few **NFR / success-metric specifics** that were softened or dropped. None is a whole-feature omission, but several are concrete enough that a developer building only from the new PRD would implement different behavior than the legacy plan intended.

---

## Gaps (ranked by impact on a developer building from the new PRD)

### HIGH

**H-1. Validation timeout value (30 s) dropped from any binding statement — actually still present.**
Re-checked: new FR-2 *does* retain "Validation that does not resolve within 30 seconds falls back to *error-corrupt*." NOT a gap. (Recorded here to show it was checked.)

**H-2. "Checking" item must be reorderable, not just selectable/removable.**
- Legacy: epics.md UX-DR13 and Story 2.3 ("a file in 'checking' state … can be the source or target of Move up/Move down like any other item (FR-2.1) — reordering is independent of validation status").
- New draft: FR-2 says a checking item "remains selectable and removable at any time regardless of Validation status" and FR-4 reorder consequences do not mention checking items. Reorderability-while-checking is dropped from the explicit list.
- Impact: A developer may gate Move up/Move down on `status == valid`, breaking the legacy contract that reorder is independent of validation. Easy to get wrong because the new FR-2 enumerates "selectable and removable" but conspicuously omits "reorderable."
- Severity: **High** (concrete behavior divergence, plausible misimplementation).

**H-3. Merge progress indicator must start indeterminate and switch to determinate if percentage is available.**
- Legacy: epics.md UX-DR20 and Story 4.2 ("starts indeterminate; switches to determinate if PROGRESS percent messages arrive"); the legacy NFR-1 + worker contract emitted `PROGRESS` percent messages.
- New draft: FR-8 / NFR-1 only say "a progress indicator is shown for any merge exceeding 2 seconds." No mention of determinate progress or percentage reporting.
- Impact: Developer will likely ship a purely indeterminate bar and never wire up per-file/page progress, a real reduction in the legacy UX. The legacy off-thread design explicitly surfaced progress percent.
- Severity: **High** (named capability silently reduced).

### MEDIUM

**M-1. Source-file-disappears-mid-merge is in the new draft but the pre-merge "valid → gone" path is under-specified vs. legacy worker guarantee.**
- Legacy: Story 4.1 worker contract — on any failure "any partial output is deleted before posting `ERROR`"; explicit DONE/ERROR/PROGRESS message protocol and the guarantee that no partial file exists if the op did not complete fully.
- New draft: FR-11 keeps "no partial output" and even adds "source file disappeared" as an example cause — good. The residual gap is only the explicitness of the *delete-partial-before-reporting* ordering, which the new draft states as an outcome ("no partial … remains") but not as a sequencing guarantee. Minor.
- Severity: **Medium-Low** (outcome preserved; mechanism less prescriptive — acceptable for a PRD, noted for architecture).

**M-2. Success banner auto-dismiss timing and manual-dismiss both specified in legacy; new draft keeps auto-dismiss but drops manual ×.**
- Legacy: epics.md UX-DR18 / Story 4.4 — success banner "auto-dismisses after 8 s; manually dismissable via ×."
- New draft: FR-9 says "auto-dismisses after ~8 seconds" but does not mention a manual dismiss (×) affordance.
- Impact: Developer may omit the manual close button on the success banner. Small but a real affordance loss.
- Severity: **Medium**.

**M-3. Error banner "manual dismiss only / no auto-dismiss" is preserved; the re-enable-buttons-on-dismiss behavior is dropped.**
- Legacy: Story 4.4 — on error-banner dismiss "the file list is preserved; Add/Merge buttons are re-enabled." UX-DR19 error banner is manual-dismiss only.
- New draft: FR-11 keeps "manually dismissed (no auto-dismiss)" and "file list preserved" — good. It does not state that the UI unlocks / buttons re-enable after a failed merge. (FR-6 only describes lock *during* merge.)
- Impact: The "merge failed → UI restored" transition (legacy UX-DR22 "Merge failed" state) is not explicit. A developer could leave the UI locked after failure. Likely inferred correctly, but not guaranteed by the text.
- Severity: **Medium**.

**M-4. Window-close-during-merge: worker termination on "Close anyway" dropped.**
- Legacy: Story 4.3 — "Close anyway" → "the merge is cancelled; the Web Worker is terminated; any partial output file is deleted from disk; the app exits cleanly." Default action = **Keep merging**.
- New draft: FR-12 keeps the dialog, **Keep merging** as default, and "Close anyway cancels the merge and leaves no partial output." It drops the explicit *cancellation of the in-flight background operation* (legacy "worker terminated"). Under C#/WinUI this maps to cancelling the `Task`/background thread.
- Impact: Without the explicit cancel-the-running-merge requirement, a developer might delete the partial file but let the background merge keep running (orphaned `Task`) — exactly the failure the legacy spec guarded against. The new addendum's `Task.Run`/CancellationToken mapping is implied but not tied to FR-12.
- Severity: **Medium**.

**M-5. File-list scroll-to-reveal on add dropped.**
- Legacy: epics.md UX-DR9 / Story 1.4 — "files append to bottom of list; list scrolls to reveal new items."
- New draft: FR-1 keeps append + duplicate-skip + cancel-noop, but drops "list scrolls to reveal the newly added items."
- Impact: After adding files to a long list, newly added items may be below the fold. Small UX regression.
- Severity: **Medium-Low**.

**M-6. Page-count extraction for valid files is in FR-2 — present. Per-item validation spinner / "checking…" visual is implied but the checking-state preview placeholder text is preserved.**
- Re-checked: new FR-5 keeps the "Checking…" placeholder in the preview pane, and FR-2 keeps page-count display. NOT a gap. (Recorded as checked.)

**M-7. Microcopy precision lost (acceptable at PRD level, flag for downstream UX).**
- Legacy: epics.md UX-DR24 enumerates MC-1…MC-19 verbatim strings (e.g., MC-3 "Password protected", MC-4 "Could not read file", MC-5/MC-6 disabled-merge tooltips, MC-8 overwrite body, MC-17/18 close-dialog copy). MC-20 "Checking…" also referenced.
- New draft: paraphrases most (success/error banner intent, "Select a file to preview it", "Add PDFs to get started", "Could not read file") but does not lock exact strings, and explicitly defers microcopy to the downstream fresh UX spec (PRD §0, §11).
- Impact: Intended (PRD says a fresh UX spec re-authors copy). Low risk *provided* the downstream UX run reuses the legacy MC table. Flag so the strings are not reinvented inconsistently.
- Severity: **Low** (deliberate deferral, but worth an explicit pointer to the legacy MC catalog).

### LOW

**L-1. Success metric SM-2 mapping note: crash-free ≥99% preserved; merge-success ≥98% preserved; downloads 1,000/30d preserved; rating ≥4.0 preserved.** All four legacy success metrics and both counter-metrics survive (new §7 SM-1…SM-4, SM-C1/C2). NOT a gap — recorded as checked.

**L-2. Disabled-Merge tooltip distinction (empty vs. all-errors) collapsed.**
- Legacy: MC-5 "Add at least one PDF to merge" (empty list) and MC-6 "Remove or replace files with errors" (all-errors) are two distinct tooltips (Story 2.4, UX-DR27).
- New draft: FR-10 says "a disabled Merge surfaces a tooltip/affordance explaining why it is unavailable" — single generic statement, distinction not required.
- Impact: Developer may ship one generic tooltip instead of context-specific copy. Minor.
- Severity: **Low**.

**L-3. Sidebar resize constraints (240–480 px; preview never < 160 px; resets to 320 on launch) partly dropped.**
- Legacy: epics.md UX-DR3 / UX-DR26 — sidebar default 320, min 240, max 480; preview min 160; resets to default on launch (no persistence).
- New draft: §11 keeps "Sidebar … resizable via a drag divider; resets on each launch (no persistence)" and §11 window constraints (min 640×480, default 900×640). It drops the specific 240/480/160/320 px clamps.
- Impact: Exact clamp values left to the downstream UX spec. The no-persistence intent and resizability survive; only the numeric bounds are gone from the PRD. Acceptable for a PRD that defers layout to UX, but the numbers are a concrete carried decision worth preserving.
- Severity: **Low**.

**L-4. Window min/default size preserved (640×480 / 900×640), maximizable preserved.** New §11 keeps these (legacy UX-DR4). NOT a gap.

**L-5. Duplicate detection semantics (case-insensitive absolute-path compare, silent skip) preserved** in new FR-1. NOT a gap.

**L-6. Flagged-file preview "attempt to render partial content + inline exclusion notice" preserved** in new FR-5 (legacy UX-DR12). NOT a gap.

**L-7. Selection-follows-moved-file and no-wraparound on reorder: selection-follows preserved; "no wraparound" implied but not stated.**
- Legacy: UX-DR8 — "No wraparound." New FR-4 keeps "selection follows the moved item" and disables Move up at top / Move down at bottom (which implies no wraparound). Effectively covered.
- Severity: **Low / non-issue**.

**L-8. Open-folder "folder no longer exists → inline notice" preserved** in new FR-9 (legacy MC-19 / UX-DR18). NOT a gap.

---

## Net assessment

- **No whole capability was silently dropped.** Every legacy FR (add / validate / remove / reorder / preview / merge / folder / overwrite / success / error / block-merge / merge-failure) has a home in new FR-1…FR-12, plus the window-close guard.
- **The real losses are behavioral specifics that migrated out of scope when the UX-DR/microcopy layer was summarized into PRD-level "Consequences (testable)":**
  - reorder-while-checking (H-2),
  - determinate/percentage progress (H-3),
  - explicit cancellation of the running merge on window-close (M-4),
  - success-banner manual dismiss (M-2),
  - UI-unlock-after-failure (M-3),
  - scroll-to-reveal on add (M-5),
  - the two distinct disabled-Merge tooltips (L-2),
  - the numeric sidebar clamps (L-3),
  - and the verbatim microcopy catalog (M-7).
- **Recommended action:** carry H-2, H-3, M-2, M-3, M-4 into the new PRD's "Consequences (testable)" lists (they are capability-level, not just UX styling), and add an explicit pointer in §0/§11 telling the downstream `bmad-ux` run to reuse the legacy MC-1…MC-20 strings and the UX-DR3/26 sidebar clamps rather than reinventing them.

---

*Generated by reconciliation pass on 2026-06-14.*
