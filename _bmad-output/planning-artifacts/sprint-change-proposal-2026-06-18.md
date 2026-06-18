---
title: "Sprint Change Proposal — FR-4 Reorder: Move up/down buttons → drag-and-drop"
date: 2026-06-18
author: Antoine (via bmad-correct-course)
trigger_commit: 859232b0f834a8a16161f52bfead7492b3198302
scope_classification: Moderate (backlog/spec reconciliation; no replan)
status: applied
---

# Sprint Change Proposal — Drag-and-Drop Reorder

## 1. Issue Summary

**Problem statement.** Commit `859232b` ("feat: reorder files by drag and drop") replaced the
**Move up / Move down** toolbar buttons with **native `ListView` drag-and-drop reorder**
(`CanReorderItems` / `AllowDrop` / `CanDragItems`). The drag mutates the bound `Files`
collection directly, so Merge still consumes display order. The `MoveUp`/`MoveDown`
`[RelayCommand]`s, their `CanMoveUp`/`CanMoveDown` gates, the two
`[NotifyCanExecuteChangedFor]` attributes on `SelectedFile`, and the two toolbar buttons
were removed. The **Remove** button stays.

**How it was discovered.** The change was made deliberately at the user's request and the
commit body itself flags the divergence: *"this reverses the PRD FR-4 / UX decision
(2026-06-14)… Planning artifacts (story 1.3, PRD, EXPERIENCE.md) are not yet updated."*
This proposal performs that reconciliation.

**Evidence.** Diff of `859232b` touches:
- `pdfjunior/MainWindow.xaml` — removed Move up/down `Button`s; added `CanDragItems`/`AllowDrop`/`CanReorderItems` to the file `ListView`.
- `pdfjunior/ViewModels/MainViewModel.cs` — removed `MoveUp`/`MoveDown` commands + gates.
- `pdfjunior.Tests/ViewModels/MainViewModelTests.cs` — `MoveUp`/`MoveDown`/`CanMove*` tests replaced by `Reorder_PreservesSelectionAndOrder` and `Remove_NoSelection_Disabled`.

## 2. Impact Analysis

- **Epic impact:** Epic 1 (File Management & Preview) — FR-4 only. No epic added/removed/resequenced.
- **Story impact:**
  - **Story 1.3 (Remove & Reorder, `review`)** — directly implements the change. ACs and Change Log updated.
  - **Story 1.1 (App Shell, `done`)** — canonical shell AC in `epics.md` updated (toolbar now Remove-only). The `done` implementation artifact `1-1-…md` is left as a historical record.
  - **Story 1.4 (Preview, `backlog`)** — **no change required.** The "preview does not reload on reorder" guarantee still holds: drag uses `ObservableCollection.Move`, preserving the selected instance. Re-verify during 1.4.
  - **Story 2.2 (Execute Merge, `backlog`)** — **future change required at draft time:** the merge UI-lock must disable drag-reorder (`CanReorderItems`/`CanDragItems`/`AllowDrop` = `False`, or list read-only) instead of disabling Move up/down buttons. `epics.md` Story 2.2 AC already updated; flagged in `sprint-status.yaml`.
  - **Stories 2.1, 2.3 (`backlog`)** — no reorder dependency; no change.
- **Artifact conflicts (resolved):** PRD, epics, architecture, UX `EXPERIENCE.md`, UX `DESIGN.md`, story `1-3` file.
- **Technical impact:** Net code reduction (commands/gates/buttons removed). Reorder now needs no view-model command; threading model unchanged (collection still mutated on the UI thread). The only forward-looking technical task is the merge-lock for drag-reorder in Story 2.2.

## 3. Recommended Approach

**Direct Adjustment.** The product scope is unchanged — FR-4 still delivers "reorder files";
only the *mechanism* changed (buttons → drag). No rollback, no MVP re-scope. Effort is
limited to spec reconciliation (done here) plus one forward-looking AC for Story 2.2 (already
captured). Risk is low: the change simplifies the code, and the selection/preview-stability
guarantees are preserved by `ObservableCollection.Move`.

**Posture note (accepted trade-off).** This softens the EXPERIENCE.md "buttons and clicks,
nothing to learn" / "Banned: drag-to-reorder" restraint principle. Drag-to-reorder is a
learned gesture; the user accepts it as the more direct interaction for a small list.

## 4. Detailed Change Proposals (applied)

### PRD (`prds/prd-pdf-junior-2026-06-14/prd.md`)
- **FR-4 (§4.1)** rewritten: drag-and-drop consequences; selection/preview stable through a drop; draggable at any Validation status; drag disabled during merge; reversal note.
- **Glossary** "Preview toolbar" → Remove-only; reorder is drag in the File list.
- **§2.3 UJ-1** narrative: "clicks Move up" → "drags a misordered file up one position."
- **§6.1 MVP scope**, **§10 Safe destructive actions**, **§11 IA toolbar**, **§13 assumptions index** updated.
- Frontmatter `updated: 2026-06-18`.

### Epics (`epics.md`)
- FR-4 inventory line and FR Coverage Map → drag-and-drop.
- Story 1.1 shell AC → Preview toolbar (Remove — disabled).
- Story 1.3 ACs → drag-and-drop (reorder, selection/preview stability, any-status draggable).
- Story 2.2 merge-lock AC → "File list read-only (drag-reorder disabled)… Add PDF(s), Merge, and Remove disabled."

### Architecture (`architecture.md`)
- FR-1–FR-4 context → "reorder via native ListView drag-and-drop."
- Requirements → Structure Mapping: FR-4 → `ListView` drag-and-drop mutating bound `Files`, **no view-model command**.

### UX `EXPERIENCE.md`
- IA table, Component Patterns (Preview toolbar → Remove-only; new **Drag-reorder** row), State Patterns ("File reordered"), Interaction Primitives + **Banned** list (drag-to-reorder no longer banned), Flow 1 narrative. Frontmatter `updated: 2026-06-18`. Added a `Decision 2026-06-18` callout.

### UX `DESIGN.md`
- `ListView` row notes built-in drag-reorder; Move up/down `Button` row removed; Remove is the sole toolbar control. Frontmatter `updated: 2026-06-18`.

### Story file (`implementation-artifacts/1-3-remove-reorder-files.md`)
- Status banner + ACs 4–6 rewritten for drag-and-drop; Change Log entry (2026-06-18) documenting the code/test rework. Original button-based Tasks/Dev Notes retained as history.

### Decision logs & provenance
- **PRD `.decision-log.md`** — dated reversal entry (mirrors the FR-11 reversal pattern), with full ripple list.
- **UX `.decision-log.md`** — row #16.
- **`addendum.md`** — legacy-provenance line updated to note the 2026-06-18 reversal.

### Sprint status (`sprint-status.yaml`)
- `last_updated: 2026-06-18`; change note with future-story impact (esp. 2.2 merge-lock).

### Deliberately NOT changed (historical, point-in-time)
- `review-rubric.md`, `review-readiness.md`, `reconcile-ux.md`, `reconcile-requirements.md`,
  `review-source-alignment.md`, `implementation-readiness-report-2026-06-14.md`, and the
  `done` story files `1-1-…md` / `1-2-…md`. These are snapshots of what was reviewed/built at
  the time; the reversal is recorded in the decision logs instead of rewriting them.

## 5. Implementation Handoff

- **Scope classification:** **Moderate** — spec/backlog reconciliation, no replan. The code change itself is already merged (`859232b`).
- **Recipients & responsibilities:**
  - **Reviewer of Story 1.3** — confirm the implementation matches the updated ACs 1–6 (drag-and-drop; Remove gate) before moving 1.3 `review → done`. Manual VS F5 visual pass still pending per the story.
  - **Developer of Story 2.2 (when drafted)** — implement the merge-lock by disabling drag-reorder on the file `ListView`, not by disabling Move up/down buttons.
- **Success criteria:** all planning artifacts describe drag-and-drop reorder consistently (verified — no stray Move up/down instructions remain in living specs); Story 2.2's future merge-lock requirement is captured; no FR renumbering; product scope unchanged.
