# Downstream-Readiness Review — PDF Junior PRD

**Reviewer role:** Adversarial downstream-readiness pass (feeds UX spec → architecture → epics/stories).
**Inputs reviewed:** `prd.md`, `addendum.md` (both dated 2026-06-14).
**Verdict:** Solid, internally coherent PRD with strong traceability — but it is **not yet clean for handoff**. There is one cross-document ID contradiction that will confuse the architect, several journey-implied behaviors missing from the FRs that a UX designer will have to invent (and therefore invent inconsistently), and a handful of untestable consequences. Recommend one tightening pass before `bmad-ux`.

**Settled decisions excluded from scope** (not re-litigated): native Save-dialog filename, no accessibility in v1, Move up/down reorder buttons, preview confirmed for v1.

---

## CRITICAL

### C-1. Open Question numbering contradicts between PRD and addendum
- **Location:** `prd.md` §12 (line 329, "Open Question … PDF library selection" — the only numbered item, i.e., OQ-1); `addendum.md` §1 line 12 ("**Open Question 2.**"), §1 line 13 PDF-validation row, §4 line 47 ("PRD Open Question 2"), and §4 item 1 ("PRD Open Question 2").
- **Problem:** The PRD has exactly **one** Open Question (the PDF library), which is item **1** in §12. The addendum repeatedly cites it as **"Open Question 2."** An architect reading the addendum will look for a non-existent OQ-2 in the PRD and may assume an OQ-1 is missing or that a question was dropped. The `.decision-log.md` (line 26) confirms intent: "Only remaining Open Question: PDF library selection." So the PRD §12 numbering (OQ-1) is correct and the addendum is stale (it still uses the pre-renumber OQ-2/OQ-3 scheme).
- **Fix:** In `addendum.md`, change every "Open Question 2" reference to "Open Question 1" (3 occurrences: §1 line 12, §4 line 47, and the §4 numbered list). Verify no orphan "Open Question 3" survives anywhere in the addendum (the reorder OQ was closed; it should not be referenced as open).

---

## HIGH

### H-1. Native Save dialog behavior is under-specified vs. what the journeys promise (UX designer will have to invent it)
- **Location:** `prd.md` FR-7 (lines 167–176) vs. UJ-1 climax (line 47), UJ-3 (lines 56–59), UJ-4 (line 63).
- **Problem:** The journeys lean heavily on Save-dialog behavior, but FR-7's consequences don't cover the full surface the journeys imply:
  - **When does the Save dialog appear relative to validation/merge state?** FR-7 says "On **Merge**, the … Save dialog opens." But the merge cannot start until a destination is chosen. So the *enabled* state (FR-6) gates opening the dialog, and the actual merge work (FR-8) starts only after the dialog returns OK. This sequencing — Merge click → Save dialog → (OK) → merge runs → (Cancel) → silent abort — is correct but only assembled by reading FR-6 + FR-7 + FR-8 together. State it explicitly in FR-7 or the §4.3 description so the UX flow and the architect's state machine match.
  - **UI lock timing.** FR-6 says "During a merge the UI is locked." But is the UI locked while the *Save dialog* is open (before the merge actually starts)? The native dialog is modal, so practically yes, but the FR doesn't say whether the app's own lock engages at click-time or at merge-start. This matters for the window-close guard (FR-12): if the user cancels the Save dialog, FR-12 must NOT fire.
- **Fix:** Add a consequence to FR-7 (or a sequencing note in §4.3): "Merge proceeds only after the Save dialog returns a confirmed destination; cancellation returns the app to its pre-Merge idle state with the UI unlocked and FR-12 not engaged." Clarify in FR-6 that the UI lock applies to the *merge execution* phase (post-dialog), not to the dialog itself.

### H-2. Progress indicator behavior is asserted but not fully testable, and its source of "progress" is undefined
- **Location:** `prd.md` FR-8 consequence (line 185, "A progress indicator is shown for any merge exceeding 2 seconds"); NFR-1 (line 285); IA §11 (line 322, "thin bar above the Action bar, visible only during a merge exceeding 2 s").
- **Problem:** "Shown for any merge exceeding 2 seconds" is **untestable as written** — you cannot know a merge will exceed 2 s until it already has. The intended behavior is almost certainly: *if the merge has not completed within 2 s, show the indicator until completion.* As written, an engineer cannot verify the requirement deterministically. Additionally: **is the indicator determinate (percentage/page progress) or indeterminate (`ProgressRing`/marquee `ProgressBar`)?** The addendum (line 19) lists both `ProgressBar` and `ProgressRing` as candidates and IA §11 calls it a "thin bar" (implying `ProgressBar`), but nothing says whether real progress can be reported. With "no file-size limit" (NFR-1), a determinate bar requires the merge engine to emit progress events — a real architectural constraint that is currently unstated.
- **Fix:** Restate FR-8 consequence as: "If the merge has not completed within 2 s of starting, a progress indicator is displayed until the merge completes, fails, or is cancelled." Add a consequence (or Open Question) on determinacy: e.g., "Progress is indeterminate in v1 unless the chosen PDF library exposes per-file/per-page progress" — and flag the determinate-vs-indeterminate choice as a decision the architecture must resolve, since it constrains the merge-engine API.

### H-3. "Off the UI thread" / responsiveness has no FR-level acceptance criterion — it's asserted, not testable
- **Location:** `prd.md` FR-8 (line 184, "merge runs off the UI thread; the app remains responsive throughout (NFR-1)"); NFR-1 (line 285); addendum line 15 (`Task.Run` / background thread).
- **Problem:** "Remains responsive" is not measurable. An engineer cannot write a passing/failing test against "responsive." For a WinUI app the concrete, testable guarantee is *the UI thread is never blocked* — e.g., the window continues to paint, the Preview pane stays scrollable (FR-6 line 165 already says this), and input is processed. NFR-1 is a product-level promise but gives no threshold or observable. This is the one re-platform concern the prompt flagged: there is currently **no FR-level guarantee** that pins down "off the UI thread."
- **Fix:** Add a testable NFR-1 consequence: "During a merge of arbitrarily large input, the UI thread is never blocked — the window repaints, the Preview pane remains scrollable, and the window-close guard (FR-12) remains responsive — verified by interacting with the app during a multi-second merge." This also gives QA a concrete script and the architect a non-negotiable threading constraint.

### H-4. Validation timeout (30 s) contradicts the "no limits / responsive" framing and is operationally ambiguous
- **Location:** `prd.md` FR-2 consequence (line 113, "Validation that does not resolve within 30 seconds falls back to *error-corrupt*"); NFR-1 (line 285, "No file-count or file-size limit").
- **Problem:** A legitimately huge but valid PDF could take longer than 30 s to validate/parse on a slow disk, and would be **falsely flagged as corrupt** — directly blocking a valid merge (FR-10) and undermining SM-3 ("≥98% successful merges on non-password-protected PDFs"). The 30 s number appears once, with no rationale, and conflicts with the explicit "no file-size limit" promise. It is also ambiguous whether the timeout is per-file or for the whole batch, and whether it is wall-clock or CPU.
- **Fix:** Either (a) raise/remove the hard timeout and define the fallback in terms of an actual parse failure rather than elapsed time, or (b) keep a timeout but tag it `[ASSUMPTION]`, state it is **per-file wall-clock**, and add it to the §13 Assumptions Index for confirmation. Also reconcile the resulting message: FR-2 says timeout → *error-corrupt*; UJ-2 (line 52) shows the *error-corrupt* user-facing string as "Could not read file" — confirm a timeout shows that same string (currently implied, not stated).

### H-5. Banner interaction model is incomplete — multiple banners, replacement, and manual-dismiss affordance unspecified
- **Location:** `prd.md` FR-9 (success banner, lines 187–194), FR-11 (error banner, lines 211–218); aesthetic §10; IA §11 line 324 lists "success banner, error banner" as transient surfaces.
- **Problem:** Several journey-implied behaviors are missing:
  - **Can a success banner and an error banner be visible at once?** If a user merges (success banner, auto-dismiss ~8 s), then immediately merges again and it fails before 8 s elapse, do both banners stack, or does the new one replace the old? Undefined.
  - **Manual dismiss UI.** FR-11 says the error banner "is manually dismissed (no auto-dismiss)" but never says *how* (close X on the `InfoBar`? click anywhere?). The success banner auto-dismisses at ~8 s — can the user also dismiss it manually before then? UJ-4 (line 63) says the error banner is "(manual dismiss)" but UJ-1 (line 47) doesn't mention dismissing the success banner.
  - **Does adding/removing files or starting a new merge clear an existing banner?** A stale "Merged successfully" banner sitting over a freshly-edited file list would be misleading.
- **Fix:** Add consequences: (1) a new merge result replaces any visible banner (only one banner shown at a time); (2) both banners expose the standard `InfoBar` close affordance, and the success banner may be dismissed manually before the ~8 s timeout; (3) starting a new Merge clears any visible banner. (`InfoBar` is named in addendum line 19 — these map cleanly to its API.)

### H-6. Window-resize / sidebar-divider behavior is in the IA but has no FR and no testable consequences
- **Location:** `prd.md` IA §11 (line 319, "Sidebar (left) … Resizable via a drag divider; resets on each launch (no persistence)"; line 325, window constraints 640×480 min / 900×640 default).
- **Problem:** The drag-divider sidebar resize is a real interactive behavior (with min/max widths, drag affordance, and a no-persistence guarantee) that appears **only** in the IA — there is no FR, so it will not flow into epics/stories and may be silently dropped, or implemented inconsistently by whoever picks it up. The §9.1 privacy note ("no remembered window/sidebar geometry") and §10 ("resets on each launch") make non-persistence a *requirement*, but it's stated as prose, not a testable consequence. Min/max sidebar width is undefined, and behavior at the 640px minimum window width (does the sidebar have a floor before the preview pane is unusable?) is unspecified.
- **Fix:** Either add a short FR (e.g., "FR-13: Resize the layout") with consequences (sidebar resizable within a min/max range; window has the stated min/default sizes; **neither sidebar width nor window geometry persists across launches**), or explicitly mark sidebar resize as a UX-spec-owned detail with the non-persistence guarantee elevated into §9.1 as testable. Don't leave an interactive control orphaned in the IA.

---

## MEDIUM

### M-1. Glossary defines *Validation status* values that don't match the user-facing strings, with no mapping
- **Location:** `prd.md` §3 Glossary (line 74, statuses "*checking*, *valid*, *error-password*, or *error-corrupt*"); vs. UJ-2 (line 52, user sees "*Password protected*" and "*Could not read file*"); FR-2 (line 113, timeout → "Could not read file").
- **Problem:** The glossary uses internal tokens (`error-password`, `error-corrupt`) but the journeys show user-facing labels ("Password protected", "Could not read file"). There is no explicit mapping table, so a UX writer and a developer could diverge on the displayed strings (e.g., is the corrupt label "Could not read file" or "Corrupt"?). The glossary preamble (line 69) insists "Downstream workflows must use these terms verbatim. No synonyms elsewhere." — yet the journeys themselves already use synonyms for the status values.
- **Fix:** Add a small mapping in §3 or FR-2: `error-password → "Password protected"`, `error-corrupt → "Could not read file"`. Clarify that the internal tokens are state names and the quoted strings are the canonical microcopy (subject to UX-spec wording).

### M-2. FR-10 wording is redundant/self-overlapping and its tooltip consequence may contradict §10 "silent by default"
- **Location:** `prd.md` FR-10 (line 204, "disabled whenever the File list is empty, contains only Flagged files, **or contains any Flagged file**"); consequence line 209 ("A disabled **Merge** surfaces a tooltip/affordance explaining why it is unavailable").
- **Problem:** (1) "contains only Flagged files, or contains any Flagged file" — the second clause subsumes the first; the list of conditions is redundant and slightly confusing (the real rule is just: enabled iff ≥1 Valid file AND zero Flagged files, which is exactly FR-6 line 164). (2) The required "tooltip/affordance explaining why" sits in mild tension with §10's "**Silent by default.** No instructional copy unless the File list is empty" — a disabled-Merge explanation is instructional copy that appears when the list is non-empty. Not a hard contradiction (tooltips are arguably not "instructional copy"), but a UX designer needs the call made.
- **Fix:** Simplify FR-10 to "Merge is disabled unless the File list contains at least one Valid file and zero Flagged files" (single rule, aligned with FR-6). Explicitly reconcile the tooltip with §10: state whether the explanation is a hover tooltip (acceptable under "silent by default") or inline text (which would violate it).

### M-3. FR-2 / FR-6 race: Merge can be enabled while files are still *checking*
- **Location:** `prd.md` FR-2 (asynchronous validation, line 106; "remains selectable and removable … while *checking*", line 114); FR-6 (line 164, enabled when "≥1 Valid file and no Flagged files remain"); FR-10 (line 204).
- **Problem:** Validation is asynchronous. Suppose the list has one *valid* file and one still *checking*. By FR-6/FR-10's literal text (≥1 Valid, no Flagged *present*), **Merge is enabled** — the *checking* file is neither Valid nor Flagged, so it doesn't block. The user could click Merge, and the *checking* file would be silently excluded (it's not Valid) — or worse, it resolves to *error-password* a moment later. UJ-1 (line 46) shows the user waiting for resolution, but no FR forbids merging while any file is *checking*. This is an edge case that will bite QA and produce surprising "where did my file go?" results.
- **Fix:** Decide and state: either (a) Merge is also disabled while **any** file is in *checking* status (safest, matches the "never silently produce a wrong result" promise in §4.4), or (b) a *checking* file is explicitly excluded from merge with a stated rationale. Add the chosen rule to FR-6/FR-10 consequences.

### M-4. "Renders whatever partial content it can" for Flagged files is untestable and may be impossible for the two flag types
- **Location:** `prd.md` FR-5 consequence (line 150, Flagged file "shows an inline exclusion notice … (rendering whatever partial content it can)"); §4.2 description; addendum line 33 ("must render partial content for damaged files with an inline exclusion notice").
- **Problem:** "Whatever partial content it can" is not testable (how much is "can"? what's the pass condition?). More concretely: an *error-password* file generally **cannot** be rendered at all by `Windows.Data.Pdf.PdfDocument` (it throws on encrypted docs — addendum line 13), and a fully *error-corrupt* file may render nothing. So "rendering whatever partial content it can" will, for the common cases, render *nothing* — making the consequence vacuous or misleading. UJ-2 (line 52) only promises "an inline exclusion notice," not partial rendering.
- **Fix:** Drop the "rendering whatever partial content it can" clause (or demote to a non-guaranteed best-effort note) and make the testable consequence simply: "the Preview pane shows an inline exclusion notice stating why the file is excluded; no preview content is required." This also aligns the PRD with the addendum's own statement that the password case throws.

### M-5. "Source file disappeared" / mid-merge file mutation is mentioned in failures but not as input validation timing
- **Location:** `prd.md` FR-11 (line 213, "source file disappeared"; line 216, "File not found: {name}"); FR-8 (execute the merge).
- **Problem:** Files are validated asynchronously at add-time (FR-2), but a file could be moved/deleted/locked between validation and merge (a real scenario the failure message anticipates). The PRD treats this only as a *failure mode* of FR-8, which is fine, but the **no-partial-output guarantee** (FR-11, UJ-4 line 65) interacts with multi-file merge: if file 3 of 5 disappears mid-write, the engine must roll back the whole output. This is an architecturally significant atomicity requirement (write-to-temp-then-rename, or buffer-then-write) that is implied but never stated as a requirement.
- **Fix:** Add an FR-11 consequence making atomicity explicit: "The output file is produced atomically — no destination file (partial or zero-byte) exists unless the entire merge succeeded (e.g., write to a temporary file and move into place on success)." Gives the architect the constraint and QA a verifiable assertion.

### M-6. SM cross-references are loose / one is mis-scoped
- **Location:** `prd.md` §7 SM-2 (line 273, "Validates NFR-1, FR-8, FR-11"); SM-3 (line 276, "Validates FR-2, FR-8, FR-11").
- **Problem:** SM-2 (crash-free sessions) is attributed to NFR-1/FR-8/FR-11, but crash-freeness spans the whole app (validation FR-2, preview FR-5 rendering — a common crash source — etc.), not just merge. SM-3 (merge success rate) cites FR-2 but the metric is explicitly scoped to "non-password-protected PDFs," i.e., it deliberately *excludes* the FR-2 password path; the FR-2 link is therefore only partly relevant. These are minor but the cross-refs are advertised (§7 header line 269) as precise.
- **Fix:** Broaden SM-2's validated set (or state it validates the product holistically like SM-1). For SM-3, clarify it validates FR-8/FR-11 plus the *valid*-detection half of FR-2 only.

### M-7. "Merge" glossary definition vs. FR-8: empty/zero-valid edge already handled, but single-file "merge" semantics undefined
- **Location:** `prd.md` §3 "Merge" (line 83); FR-6 (enabled at "≥1 Valid file", line 164); FR-8 (line 180).
- **Problem:** Merge is enabled with as few as **one** Valid file (FR-6). "Merging" a single file is a degenerate case — is it allowed (produces a copy with a chosen name) or should it be blocked/relabeled? The product allows it per FR-6, which is probably fine (a user may want to "save as" via the merge flow), but it's never acknowledged, and the success banner copy "Merged successfully — merged.pdf" is slightly odd for a single file. Worth a one-line confirmation so epics don't treat single-file as an error.
- **Fix:** Add a one-line note to FR-8: "A single Valid file is a valid merge (produces a copy at the chosen destination)." Confirm the success copy is acceptable for n=1.

---

## LOW

### L-1. "~8 seconds" and "~2 seconds" use tildes — confirm tolerance for QA
- **Location:** `prd.md` FR-9 (line 192, "~8 seconds"); FR-8/NFR-1/IA (2 seconds).
- **Problem:** The "~" on 8 s implies a tolerance but none is given; the 2 s threshold is exact in some places and the IA. QA needs to know if 8 s is a hard assertion or "roughly." Minor.
- **Fix:** State "8 s (± UX-spec tolerance)" or pick an exact value. Keep the 2 s threshold consistent (it already is).

### L-2. Glossary preamble "no synonyms elsewhere" is violated by the PRD's own prose
- **Location:** `prd.md` §3 (line 69); e.g., "file list" capitalization varies, and "native Windows file picker" (FR-1) vs. glossary which defines "Save dialog" but not the *open*/input picker as a glossary term.
- **Problem:** The input file picker (`FileOpenPicker`) is used throughout ("native Windows file picker", FR-1 line 95; "native file picker", UJ-1 line 46) but, unlike "Save dialog," it is **not a glossary term**. Asymmetric: the output picker is defined, the input picker is not. Minor inconsistency given the strict "verbatim terms" rule.
- **Fix:** Add a glossary entry "File picker — the native Windows open-file dialog invoked by **Add PDF(s)**, filtered to `.pdf`, multi-select." Use it verbatim thereafter.

### L-3. FR-12 "Close anyway cancels the merge" — cancellation mechanism vs. NFR-1 background thread is implied only
- **Location:** `prd.md` FR-12 (lines 220–226); addendum line 15 (`Task.Run`).
- **Problem:** "Close anyway cancels the merge and leaves no partial output" requires cooperative cancellation (`CancellationToken`) threaded through the merge engine — an architectural requirement that the PRD implies but doesn't state. If the chosen PDF library can't be cancelled mid-operation, "Close anyway" can't honor the no-partial-output guarantee without killing the process and cleaning up the temp file. Worth flagging for architecture.
- **Fix:** Add a note (or fold into M-5's atomicity consequence): cancellation must be cooperative and must trigger temp-file cleanup; flag library cancellation support as an architecture concern alongside Open Question 1.

### L-4. UJ-3 "reorders files" but is a single-Valid-file-capable flow — minor narrative-vs-FR slack
- **Location:** `prd.md` UJ-3 (line 57, "Marcus reorders files"); FR-4 Realizes "UJ-1, UJ-3" (line 127).
- **Problem:** UJ-3's whole point is overwrite confirmation; the "reorders files" clause is incidental and adds nothing to the journey's purpose. Harmless, but FR-4 cites UJ-3 as a primary reorder journey when UJ-1 already covers reorder more directly. Trivial traceability noise.
- **Fix:** Optional — drop the reorder mention from UJ-3 or keep FR-4's UJ-3 ref only if reorder is genuinely load-bearing there. No action required.

### L-5. Title/working-title still unconfirmed
- **Location:** `prd.md` line 9 ("*Working title — confirm.*").
- **Problem:** The product name "PDF Junior" is used as a defined glossary term (line 71) and throughout, but the title line flags it as unconfirmed. If the name changes, the glossary and Store-listing NFRs (NFR-3, NFR-7) and privacy-policy copy all need updating. Low risk but a dangling confirmation.
- **Fix:** Resolve the working title before UX/Store-asset work begins, or add it to §12 Open Questions so it isn't lost.

---

## Summary of recommended pre-handoff actions
1. **(Critical)** Fix the Open Question 1 vs. 2 numbering across the addendum.
2. **(High)** Pin down Save-dialog sequencing & UI-lock timing (FR-6/FR-7), make the progress-indicator and "off-UI-thread" requirements testable (FR-8/NFR-1), reconcile the 30 s validation timeout with "no size limit," complete the banner interaction model, and give the sidebar-resize behavior an FR or explicit UX-owned home.
3. **(Medium)** Add the status-token → microcopy mapping, resolve the *checking*-while-Merge-enabled race, make output atomicity explicit, and de-vacuum the "partial content" preview consequence.
4. **(Low)** Tidy tolerances, glossary symmetry, cancellation note, and the working title.

**File:** `C:\dev\pdfjunior\_bmad-output\planning-artifacts\prds\prd-pdf-junior-2026-06-14\review-readiness.md`
