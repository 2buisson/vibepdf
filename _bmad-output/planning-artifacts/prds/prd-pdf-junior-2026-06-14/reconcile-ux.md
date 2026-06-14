---
title: UX Reconciliation — Legacy UX vs. Fresh PRD
project: PDF Junior
created: 2026-06-14
sources:
  legacy_experience: ../../../../pdfjunior-legacy/_bmad-output/planning-artifacts/ux-designs/ux-pdf-junior-2026-06-01/EXPERIENCE.md
  legacy_design: ../../../../pdfjunior-legacy/_bmad-output/planning-artifacts/ux-designs/ux-pdf-junior-2026-06-01/DESIGN.md
  prd: ./prd.md
---

# UX Reconciliation

## Purpose & method

This reconciliation reads the freshly authored PRD (`prd.md`, 2026-06-14) against the
legacy UX experience set (`EXPERIENCE.md` / `DESIGN.md`, v1.3, 2026-06-10) it was derived
from. The goal is to surface **qualitative ideas (tone/voice/feel), behavioral edge cases,
and state-handling** that the legacy UX captured but the new FR-structured PRD has dropped
or weakened — and that would **not be recoverable** if lost before the downstream
`bmad-ux` spec is authored.

### Out of scope (not flagged)

The following are **deliberate, decided changes** and are correctly reflected in the PRD —
they are not gaps:

- Output filename now set in the native **Save dialog** (default `merged.pdf`); legacy
  `OutputNameField` and its in-app validation are intentionally gone.
- **No accessibility** in v1 (the entire legacy "Accessibility Floor" and per-control
  Narrator/focus behavior is deliberately descoped — PRD §5, §6.2).
- Reorder = **Move up / Move down** buttons (legacy already reflected this).
- **Preview confirmed** for v1 (legacy "deferrable spike" risk does not carry).
- Tech is **C# / WinUI** (legacy was React Native for Windows).

Pure **visual/layout styling** (color tokens, spacing grid, corner radii, typography
ramps, icon glyphs, exact pixel sizes, divider drag clamps, Mica/elevation) is also out of
scope — it belongs to the downstream UX spec and is not PRD-level material.

---

## Verdict

The PRD is a **faithful and largely complete** re-platform of the legacy plan. Product
scope, journeys, FR coverage, and the core tone are all carried. The gaps that remain are
mostly **qualitative tone nuance and a handful of resolved-state edge cases** — none are
scope-breaking, but several encode intent that a downstream UX author would otherwise have
to re-invent (and could get wrong), so they are worth landing in the PRD now.

---

## Gaps (ranked by impact)

### HIGH

#### H1 — Per-situation tone matrix is collapsed to one generic paragraph
- **Legacy:** `EXPERIENCE.md → Voice and Tone` table (lines 63–75) defines a distinct tone
  *per situation*: empty state = "warm invitation, single line"; error on file = "factual,
  non-blaming"; merge success = "brief, affirming, one optional action"; merge failure =
  "honest, specific reason if available, **no apology filler**"; overwrite warning =
  "neutral, factual, default is safe".
- **PRD:** §10 keeps the umbrella tone ("calm, capable, never shouts", "plain, direct,
  never alarmist", "silent by default") but **drops the per-situation differentiation**.
  The load-bearing intents — *non-blaming* on file errors, *no apology filler* on failure,
  *affirming* (not just neutral) on success — are gone.
- **Why it matters / recoverability:** These are microcopy *intent* decisions, not visual
  styling. Once the downstream UX author writes copy, "honest with no apology filler" vs. a
  generic apologetic error string is exactly the kind of thing that silently drifts. If it
  isn't asserted at PRD level, it is not recoverable — it just won't be re-derived.
- **Recommendation:** Add the situation→tone intents to §10 (a compact bullet list is
  enough; the visual table can stay in the UX spec).

#### H2 — Microcopy *intent* for key states is no longer anchored
- **Legacy:** `EXPERIENCE.md → Microcopy inventory` (lines 76–100) pins the *intent* behind
  the strings users actually see: empty list "Add PDFs to get started"; empty preview
  "Select a file to preview it"; password error "Password protected"; corrupt error "Could
  not read file"; the two **distinct disabled-Merge tooltips** ("Add at least one PDF to
  merge" for empty vs. "Remove or replace files with errors" for all-error); success
  "Merged successfully — {filename}"; failure-with-reason vs. failure-without-reason
  ("Merge failed. Try again or check the files.").
- **PRD:** Many of these survive *embedded in journeys and FR consequences* (good), but the
  PRD never states they are the canonical, locked phrasings, and a few intents are softened:
  FR-10 reduces the **two semantically different disabled tooltips** to a single generic
  "tooltip/affordance explaining why it is unavailable" (line 209) — losing the distinction
  between "list is empty" and "you have files but they all have errors", which are different
  user problems with different remedies.
- **Why it matters / recoverability:** The empty-vs-all-error tooltip split is a real
  behavioral/microcopy decision, not styling. If the PRD only says "explain why", the UX
  author may write one tooltip and the empty-list user and the all-errors user get the same
  unhelpful string.
- **Recommendation:** In FR-10, explicitly require **two distinct** disabled-Merge
  explanations (empty list vs. files-present-but-all-flagged). Note in §10 that the legacy
  microcopy intents are the starting point for the UX spec.

#### H3 — The 2-second progress-indicator threshold is documented, but the "may not appear at all" *feel* and the >2 s feedback contract are weakened
- **Legacy:** `EXPERIENCE.md` Flow 1 (line 328) and merge action (line 280) make the *feel*
  explicit: for small merges the indicator "may not appear at all"; and after 2 s the
  indicator is the user's reassurance that work is happening. Legacy also ties **focus to
  the ProgressIndicator region** (descoped — accessibility) and dismisses any prior banner
  the instant Merge is pressed (see M3).
- **PRD:** FR-8 keeps ">2 seconds" (line 185) — so the threshold itself is **not** a gap.
  What is weakened is the deliberate *no-chrome-for-fast-merges* feel: the PRD states the
  bar appears over 2 s but does not assert the intent that a fast merge shows **nothing**
  (no flash, no spinner) — i.e., absence of feedback is the intended design, not a defect.
- **Why it matters:** Without the stated intent, a UX/dev pass may add a brief spinner or
  "merging…" flash for *all* merges "to be safe", which directly contradicts the
  silent-by-default principle. Borderline HIGH/MEDIUM; placed HIGH because it is a likely
  well-intentioned regression.
- **Recommendation:** Note in §10 or FR-8 that sub-2 s merges intentionally show no progress
  affordance at all.

### MEDIUM

#### M1 — "Checking…" preview placeholder and the 30 s validation-timeout *rationale*
- **Legacy:** `EXPERIENCE.md → ProgressRing` (line 159) and File item states (line 193)
  state the 30 s timeout exists **so the item "does not remain permanently
  non-interactive"** — a stated reliability rationale — and that on resolution the item
  "announces its new state" (the announce part is accessibility, descoped).
- **PRD:** FR-2 keeps the 30 s → *error-corrupt* fallback and the "Checking…" placeholder
  (lines 113, 149) — so the behavior itself is **carried**. What is lost is the explicit
  *why* (prevent a permanently stuck, non-interactive item). Minor, but the rationale guards
  against a future "just keep spinning" implementation choice.
- **Recommendation:** Optional one-line rationale on FR-2's timeout consequence.

#### M2 — Selected-file behavior *through a reorder* (selection + preview stay put)
- **Legacy:** `EXPERIENCE.md → PreviewToolbar` (line 128) and Reorder primitive (line 248):
  after Move up/down, **selection follows the moved file and the preview pane content is
  unchanged** (same file stays selected at its new position).
- **PRD:** FR-4 says "selection follows the moved item" (line 130) — the selection half is
  carried. The **preview-stays-loaded** half (no preview reload/flicker on reorder) is not
  stated.
- **Why it matters:** A naive implementation could clear/reload the preview on list
  re-render. Stating "preview content is unchanged on reorder" preserves the intended
  no-flicker feel.
- **Recommendation:** Add to FR-4 consequences: reordering does not reload or clear the
  Preview pane.

#### M3 — Prior banner is dismissed *the instant* Merge is pressed
- **Legacy:** `EXPERIENCE.md` merge action step 1 (line 271) and lifecycle (line 212): "Any
  visible success or error banner is dismissed immediately before the folder picker opens."
  Also captured in the merge-in-progress state row ("Any prior banner dismissed", line 185).
- **PRD:** Not stated. FR-9 covers showing the success banner; FR-11 the error banner; but
  nothing says a **stale** banner from a previous merge is cleared at the start of the next
  Merge.
- **Why it matters:** Edge case for the retry/second-output flows (UJ-3, UJ-4): a user who
  merges, sees the success/error banner, then merges again should not see the old banner
  lingering over the new operation. Recoverable-ish but easy to miss.
- **Recommendation:** Add to FR-6 consequences: pressing **Merge** immediately dismisses any
  visible success/error banner before the Save dialog opens.

#### M4 — Open-folder "Folder not found" recovery is present; the *other* failure-reason specificity is slightly thinner
- **Legacy:** `EXPERIENCE.md → ErrorBanner` (lines 293–295) and merge action step 6 (lines
  281–283) enumerate the *specific* reasons to surface when available — "Disk full", "Access
  denied", "file not found: {filename}" — with the generic MC-12 only as fallback.
- **PRD:** FR-11 carries this well (line 216) including the same examples and the
  manual-dismiss rule. **Largely not a gap.** The one soft spot: the legacy frames specific
  reasons as the *expected default* ("specific reason if available"); the PRD frames generic
  fallback and specific reason more even-handedly. Low-risk; noting for completeness.
- **Recommendation:** None required; optionally reaffirm "prefer the specific reason" in §10.

#### M5 — Empty-state vs. silent-normal-state distinction
- **Legacy:** `EXPERIENCE.md → Voice and Tone` (lines 71–72): the **only** time instructional
  copy appears is the empty file list ("warm invitation, single line") and empty preview;
  every other state is **silent**.
- **PRD:** §10 says "silent by default. No instructional copy unless the File list is empty"
  (line 308) — this **is carried**. The empty-preview "Select a file to preview it" copy is
  also in FR-5 (line 151). Not a true gap; included to confirm the principle landed.
- **Recommendation:** None.

### LOW

#### L1 — Climax-beat / emotional payoff framing of the journeys
- **Legacy:** Each Key Flow ends with a "Climax beat" describing the *emotional payoff*
  ("Four PDFs, one click, done. He emails the file before the bell rings."; "Sophie
  exhales…"; "the app held his work through the failure", lines 331, 343, 381).
- **PRD:** §2.3 journeys (UJ-1…UJ-4) keep the persona, path, climax, and resolution shape
  but in a flatter, more functional register — the explicit emotional payoff language is
  trimmed.
- **Why it matters:** Low — the downstream UX spec re-authors the detailed flows and can
  re-introduce emotional framing. The *intent* (calm, reassuring, "held your work") is
  preserved in §10's "holds the user's work" bullet, so it is recoverable.
- **Recommendation:** None required; the §10 principle covers it.

#### L2 — File-add affordances: append-to-bottom + auto-scroll-to-reveal
- **Legacy:** `EXPERIENCE.md → File addition` (line 261): newly added files append to the
  **bottom** and "the list scrolls to reveal newly added items."
- **PRD:** FR-1 carries "appended to the end of the File list" / "to the bottom" (lines 95,
  99). The **auto-scroll-to-reveal** micro-behavior is not stated.
- **Why it matters:** Low. It is a small affordance, plausibly re-derivable by the UX spec,
  but stating it avoids a "new files added off-screen with no feedback" miss when many files
  are added.
- **Recommendation:** Optional one-liner on FR-1.

#### L3 — Flagged-file preview: partial render + inline exclusion notice wording intent
- **Legacy:** `EXPERIENCE.md → PreviewPane` (line 152): a flagged file's preview "attempts to
  render what is available" and shows an inline notice "This file has an issue — it will be
  excluded from the merge"; if nothing can render (fully encrypted) "the notice alone is
  sufficient feedback."
- **PRD:** FR-5 carries the behavior ("inline exclusion notice explaining why it is
  excluded (rendering whatever partial content it can)", line 150). Behavior is carried; the
  specific *reassuring* phrasing intent ("the notice alone is sufficient feedback" for a
  blank render) is the only soft spot.
- **Recommendation:** None required; behavior is captured, exact copy belongs to UX spec.

#### L4 — Duplicate-skip is *silent*; picker-cancel is a *no-op* (both carried)
- **Legacy:** `EXPERIENCE.md → File addition` (lines 258–259): duplicate same-path
  (case-insensitive) silently skipped; picker dismissed = no-op, no error/banner.
- **PRD:** FR-1 carries both faithfully (lines 100–101). **Not a gap** — confirmed landed.
- **Recommendation:** None.

---

## Summary table

| ID | Severity | Gap | Legacy location |
|----|----------|-----|-----------------|
| H1 | High | Per-situation tone matrix collapsed to one generic paragraph (non-blaming / no-apology / affirming intents lost) | EXPERIENCE.md L63–75 |
| H2 | High | Two distinct disabled-Merge tooltips (empty vs. all-error) reduced to one generic "explain why" | EXPERIENCE.md L84–85, MC-5/MC-6; PRD FR-10 L209 |
| H3 | High | Sub-2 s merges intentionally show *no* progress affordance — "may not appear at all" feel not asserted | EXPERIENCE.md L280, L328 |
| M1 | Medium | 30 s validation-timeout *rationale* (avoid permanently stuck item) dropped | EXPERIENCE.md L159, L193 |
| M2 | Medium | Preview content stays loaded/unchanged through a reorder (no flicker) not stated | EXPERIENCE.md L128, L248 |
| M3 | Medium | Stale success/error banner dismissed the instant Merge is pressed | EXPERIENCE.md L271, L185 |
| M4 | Medium | Specific failure reason framed as the expected default (vs. even-handed with generic) | EXPERIENCE.md L281–295 |
| L1 | Low | "Climax beat" emotional-payoff framing of journeys trimmed | EXPERIENCE.md L331/343/381 |
| L2 | Low | Auto-scroll-to-reveal newly added files not stated | EXPERIENCE.md L261 |
| L3 | Low | Flagged-file "notice alone is sufficient" reassurance intent (behavior carried) | EXPERIENCE.md L152 |

*M5 and L4 reviewed and confirmed **carried** (not gaps); listed in detail above for traceability.*
