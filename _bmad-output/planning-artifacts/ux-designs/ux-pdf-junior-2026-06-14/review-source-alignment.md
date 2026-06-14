# Source Alignment Review — PDF Junior

## Overall verdict

The UX spines (DESIGN.md and EXPERIENCE.md) are strongly aligned with their upstream sources. All 12 PRD functional requirements have complete behavioral and visual coverage. All HIGH and MEDIUM gaps from the reconcile-ux.md are explicitly addressed in the spines, with decision-log traceability. The one substantive finding is a string-prefix mismatch between the architecture's Error Taxonomy listing and the canonical EXPERIENCE.md microcopy for three error strings — the architecture omits the "Merge failed — " prefix that EXPERIENCE.md defines.

## 1. PRD FR Coverage — PASS

### Findings

- **low** FR-10 tooltip: the PRD requires distinguishing "no-valid-file case from the flagged-file-present case" (two cases). EXPERIENCE.md provides three distinct tooltips (MC-10, MC-11, MC-12), adding a third for the "still checking" state. This exceeds the PRD requirement and is a beneficial extension, not a contradiction. No fix needed.
- **low** NFR-5 startup time (<3 s): covered only implicitly via Key Flow 1 narrative ("The window opens in under three seconds"). The spines do not restate the NFR as a constraint, but startup time is not a UX-spec responsibility — it is an architecture/NFR concern. No fix needed.

All 12 FRs (FR-1 through FR-12) and all UX-relevant NFRs (NFR-1, NFR-2, NFR-4, NFR-5) have sufficient behavioral and visual information in the spines for a developer to implement them. No missing, incomplete, or contradictory treatments found.

## 2. Architecture Microcopy Alignment — NEEDS ATTENTION

### String cross-reference table

| MC-ID | EXPERIENCE.md string | Architecture expected | Match? |
|-------|----------------------|----------------------|--------|
| MC-15 | Merge failed — Not enough space on {drive}. | Not enough space on {drive}. | NO — prefix "Merge failed — " missing in architecture |
| MC-16 | Merge failed — Access denied | Access denied | NO — prefix "Merge failed — " missing in architecture |
| MC-17 | Merge failed — File not found: {name} | File not found: {name} | NO — prefix "Merge failed — " missing in architecture |
| MC-18 | Merge failed. Try again or check the files. | Merge failed. Try again or check the files. | YES |
| MC-19 | Folder not found | Folder not found | YES |

### Findings

- **high** MC-15, MC-16, MC-17: The architecture's Error Taxonomy (line 275-280) lists the error reason substrings without the "Merge failed — " prefix, but the canonical EXPERIENCE.md strings include it. The architecture itself says these must be the "exact EXPERIENCE.md strings." The architecture's UI String Patterns section (line 383) also cites the format string `Not enough space on {drive}.` without the prefix. *Fix:* Update the architecture's Error Taxonomy mapping and UI String Patterns example to include the full canonical strings from EXPERIENCE.md: `Merge failed — Not enough space on {drive}.`, `Merge failed — Access denied`, `Merge failed — File not found: {name}`. Alternatively, the architecture could document a two-part composition pattern (prefix + reason) if that is the intended implementation strategy, but it should state so explicitly and the composed result must match EXPERIENCE.md verbatim.
- **low** The architecture's Error Taxonomy enumerates only the five error/failure strings (MC-15 through MC-19). The remaining 18 MC strings (MC-1 through MC-14, MC-20 through MC-23) are covered by the general UI String Patterns directive ("all user-facing strings live in UiStrings.cs and must match EXPERIENCE.md verbatim") but are not individually listed in the architecture. This is acceptable — the taxonomy's scope is exception-to-microcopy mapping, not a full string inventory. No fix needed, but implementers should treat the EXPERIENCE.md Microcopy Inventory as the authoritative enumeration for UiStrings.cs.

## 3. Reconcile-UX Gap Coverage — PASS

### Gap resolution table

| Gap ID | Severity | Description | Addressed? | Where |
|--------|----------|-------------|------------|-------|
| H1 | High | Per-situation tone matrix collapsed | YES | EXPERIENCE.md Voice and Tone table (lines 49-62); Decision log #4 |
| H2 | High | Distinct disabled-Merge tooltips | YES (exceeded) | EXPERIENCE.md MC-10, MC-11, MC-12 — three tooltips for three states; Decision log #3 |
| H3 | High | Sub-2s merges show no progress affordance | YES | EXPERIENCE.md ProgressBar component (line 118), State Patterns (line 135); Decision log #5 |
| M1 | Medium | Validation timeout rationale | YES | EXPERIENCE.md State Patterns (line 141) — rationale stated: "prevents a permanently stuck, non-interactive item" |
| M2 | Medium | Preview unchanged through reorder | YES | EXPERIENCE.md Move up/down component (line 112), State Patterns (line 132); Decision log #6 |
| M3 | Medium | Stale banner dismissed on Merge press | YES | EXPERIENCE.md Merge button (line 115), State Patterns (line 133); Decision log #7 |
| M4 | Medium | Specific failure reason as expected default | YES | EXPERIENCE.md Voice and Tone general rules (line 70): "Specific failure reasons are the expected default" |
| M5 | Medium | Empty-state vs. silent-normal distinction | N/A (not a gap) | Reconcile-ux.md confirmed carried; EXPERIENCE.md general rules (line 66) |
| L1 | Low | Climax-beat emotional payoff | YES | Key Flows include emotional payoff language (Flows 1-4) |
| L2 | Low | Auto-scroll on file add | YES (reversed) | Decision log #9 — deliberately reversed to no auto-scroll; EXPERIENCE.md File list component (line 108) |
| L3 | Low | Flagged-file "notice alone is sufficient" | YES | EXPERIENCE.md Preview pane (line 110), MC-8/MC-9; Decision log #8 |
| L4 | Low | Duplicate-skip silent, picker-cancel no-op | N/A (not a gap) | Reconcile-ux.md confirmed carried; EXPERIENCE.md File list and Add button components |

### Findings

- All three HIGH gaps are fully resolved with explicit decision-log traceability.
- All five MEDIUM gaps are resolved. M5 was confirmed "not a gap" by the reconcile-ux.md itself and is faithfully carried.
- All four LOW gaps are addressed. L2 was deliberately reversed (auto-scroll removed); L4 was confirmed "not a gap."
- No reconcile-ux.md gap was left unaddressed.

## Known deltas (informational)

- Validation timeout: UX says 5s (EXPERIENCE.md line 141, Decision log #10), architecture says 30s (architecture line 242). This is a user decision made during this session — the architecture has not yet been updated. Not a bug.
