# PRD Quality Review — PDF Junior

## Overall verdict

This is a strong, disciplined chain-top PRD for what it is: a small single-purpose utility where restraint is the whole product thesis, and the PRD lives that thesis rather than just asserting it. Done-ness clarity is its standout strength — every FR carries testable consequences with real bounds (30 s validation timeout, 2 s progress threshold, ~8 s banner auto-dismiss), and the no-partial-output guarantee is threaded coherently across FR-8/11/12 and the UJs. The main risks are concentrated in two SMs that don't actually validate the thesis they're attached to (SM-1 rating attributed to "FR-1…FR-12 collectively"; SM-4 adoption as a product metric the app has little lever over), and one quietly load-bearing assumption — that the platform PDF API can cleanly distinguish *error-password* from *error-corrupt* — that the PRD treats as settled but is the riskiest single dependency in the build.

## Decision-readiness — strong

A decision-maker can act on this. The hard product calls are stated as decisions, not hedged: reorder is "Move up / Move down buttons (decided — carries the legacy 2026-06-10 reversal of an earlier drag-and-drop design)" (FR-4 Notes), output filename is set in the native Save dialog with the consequence that "the app performs no separate filename validation" (FR-7), and accessibility is explicitly cut for v1 with the trade-off named ("no committed keyboard-only guarantees, Narrator/screen-reader announcements, WCAG conformance," §5). These are choices with what-was-given-up attached, not neutral balancing.

The single Open Question (§12, PDF library selection) is genuinely open and correctly punted to architecture rather than rhetorically answered. The `[NOTE FOR PM]` on page-range selection (§6.2) sits at a real tension — the one legacy "future consideration" — not a safe checkpoint. The PRD resists the red-flag pattern of smoothing everything to neutral.

One soft spot: the §5 accessibility cut is decisive but its downstream cost is undersold. "Native WinUI controls retain Windows' baseline automation support" is true but glosses that keyboard reorder (Move up/down via keyboard) and focus management after Remove (FR-3 clears selection) are exactly the interactions a keyboard-only user would hit first. Not a reversal of the decision — a flag that the decision has more UX blast radius than the sentence implies.

### Findings
- **low** Accessibility cut understates downstream UX cost (§5; §2.3 UJ flows) — The decision to scope out accessibility is legitimate and clearly owned, but FR-3 (selection clears on Remove) and FR-4 (button-only reorder) are precisely where keyboard-only users break, and the PRD frames the gap as merely "no accessibility work scoped." *Fix:* add one sentence noting that button-based reorder + selection-clearing are the specific interactions a later accessibility pass would need to revisit, so the UX spec doesn't bake in a hard-to-undo focus model.

## Substance over theater — strong

Little furniture here. The Vision (§1) is product-specific, not swappable: it names the actual competitive gap ("PDF24 (~400 MB) or PDFgear (~200 MB)") and ties the differentiator to a concrete, defended threshold ("stays under 100 MB installed"). That same number reappears as a hard NFR (NFR-6, "a hard constraint and a core competitive differentiator, not a stretch goal") and is tracked in the addendum (§2, "track the Windows App SDK + chosen PDF library footprint against the 100 MB cap early"). The Vision earns its claims.

No persona theater: §2 deliberately collapses to "Anyone on Windows 11 who needs to combine PDFs" and resists inventing segments. The four UJs use named protagonists (Marcus, Sophie, David) but each drives distinct FR coverage — UJ-2 is the flagged-file path, UJ-4 is the failure-recovery + no-partial-output guarantee — so they're load-bearing, not decoration. No innovation-theater differentiation section.

NFRs avoid boilerplate: NFR-1 has a real threshold ("progress indicator… for any merge exceeding 2 seconds"), NFR-5 is bounded ("within 3 seconds on a mid-range Windows 11 PC"), NFR-6 is hard-capped. The one adjective worth flagging — "mid-range Windows 11 PC" (NFR-5) — is undefined, but for a free utility this is an acceptable looseness, not theater.

## Strategic coherence — strong

The PRD has a real thesis and bets on it consistently: *do one thing, refuse to grow, and win on privacy + restraint + footprint rather than features.* This is not just stated in §1 — it's enforced structurally. The counter-metrics are the strongest evidence: SM-C1 ("Do not grow scope to chase ratings; simplicity is the differentiator") and SM-C2 ("Do not optimize for time-in-app") explicitly defend the thesis against the two most likely ways a successful utility gets ruined. The §10 "Hard anti-patterns" list (no splash, no onboarding, no upsell, no AI-feature prompts) and the "permanently free… ever" monetization stance (§9.3, NFR via §5 `[NON-GOAL for MVP and beyond]`) all serve the same arc. Feature prioritization follows the thesis, not ease.

MVP scope kind is coherent: this is a problem-solving MVP (one job done completely and safely), and the scope logic matches — error handling and the no-partial-output guarantee get a full feature group (§4.4) because *reliability is the product*, which is exactly where a problem-solving MVP should spend its complexity budget.

The coherence gap is in Success Metrics, covered in detail under Done-ness and Downstream, so only flagged here: SM-4 (adoption, 1,000 downloads/30 days) measures market pull, not the thesis, and is largely outside the product's control (Store discoverability, listing copy). It's a fine business goal but a weak *product* success metric, and it slightly dilutes an otherwise tight metric set.

### Findings
- **medium** SM-4 measures distribution, not the thesis (§7) — "1,000 Store downloads in the first 30 days. Validates the vision/positioning (§1)." Downloads validate marketing/Store placement more than the product's reliability-and-restraint thesis, and the team has weak levers on it. *Fix:* either reframe SM-4 as an explicitly-acknowledged business/distribution metric (not a product-quality signal), or replace it with something the product controls — e.g., merge-completion rate per session, or uninstall rate within 7 days as a retention/satisfaction proxy.

## Done-ness clarity — strong

This is the PRD's best dimension and the one downstream story creation leans on hardest. Every FR (FR-1 through FR-12) carries an explicit **Consequences (testable)** block, and the consequences are genuinely testable, with concrete bounds rather than adjectives:

- FR-2: "Validation that does not resolve within 30 seconds falls back to *error-corrupt*" — a real timeout, not "handles slow files gracefully."
- FR-8: "A progress indicator is shown for any merge exceeding 2 seconds" — measurable trigger.
- FR-9: "auto-dismisses after ~8 seconds"; "if the folder no longer exists, an inline 'Folder not found' message is shown instead of failing" — the edge case is specified, not waved.
- FR-11: enumerated error reasons with exact strings ("Not enough space on E:\.", "Access denied", "File not found: {name}") plus a generic fallback, and "No partial or zero-byte output file remains."
- FR-12: confirmation dialog with named buttons and defaults ("Keep merging (default) and Close anyway").

The no-partial-output guarantee is the model case: stated as a UJ-4 "Guarantee," then made testable in FR-11 and FR-12. State-machine edges are covered (selection clears on Remove, FR-3; Merge disable conditions enumerated in FR-6/FR-10; Move up/down disabled at list ends, FR-4). Empty/placeholder states are specified (FR-5: "Select a file to preview it"; "Add PDFs to get started").

Two small soft edges, both low-severity. First, NFR-5's "mid-range Windows 11 PC" is the one unbounded performance phrase — acceptable for a free utility but worth a footnote so QA isn't guessing the test hardware. Second, FR-5's "rendering whatever partial content it can" for a Flagged file is the one consequence that is hard to make pass/fail (how much partial content? is none acceptable?); it's the seam where the otherwise-tight done-ness loosens, and it depends on PDF-library behavior that isn't yet chosen.

### Findings
- **low** "Rendering whatever partial content it can" is not testable (§4.2 FR-5) — For a Flagged file, the preview should "render whatever partial content it can," which has no pass/fail bound and depends on the unchosen PDF library. *Fix:* make the inline exclusion notice the guaranteed behavior and explicitly mark partial-content rendering as best-effort/optional, so a story can be marked done when the notice shows even if zero pages render.
- **low** "Mid-range Windows 11 PC" is an unbounded test target (§8 NFR-5) — The 3 s startup bound is good but the reference hardware is an adjective. *Fix:* name a rough spec (e.g., a representative CPU/RAM baseline) or defer the exact bench to the architecture/test plan with a note.

## Scope honesty — strong

Omissions are explicit and do real work. §5 Non-Goals is a substantive list, not a formality — it pre-empts the obvious scope-creep requests (editor, page-level ops, compression, password unlock, cloud, multi-language, mobile/web, monetization, accessibility) and tags the permanence distinctions correctly: monetization is `[NON-GOAL for MVP and beyond]` while multi-language is "v1 only." §2.2 Non-Users reinforces this from the user-segment angle. §6.2 separates "deferred to a possible v1.1" (page-range, with a `[NOTE FOR PM]`) from "not planned" — honest gradation rather than a flat dump.

Open-items density is appropriately low for a green-light-to-build PRD: exactly one Open Question (§12), one `[ASSUMPTION]` (FR-7), and one `[NOTE FOR PM]` (§6.2 page-range). For the stakes, that's correctly calibrated — the PRD isn't hiding unresolved decisions behind tags, and §13 even notes which earlier assumptions were *closed* by user decisions on 2026-06-14 ("output-filename handling, reorder control, and accessibility… no longer open"), which is exactly the kind of resolution honesty the rubric wants.

The one place scope honesty is slightly thin is not in what's tagged but in what *isn't*: the *error-password* vs *error-corrupt* distinction (FR-2, Glossary) is presented as a settled v1 capability, but the addendum (§1 PDF-validation row; §4 deferred decision 2) shows the *mechanism* to reliably distinguish them is unresolved and library-dependent. That's an inference the reader could silently assume is risk-free. It deserves an `[ASSUMPTION]` or `[NOTE FOR PM]` in the PRD body, not just a quiet entry in the addendum's deferred list — because if the chosen library can't cleanly separate the two, FR-2's consequences and UJ-2's whole narrative shift.

### Findings
- **high** The password-vs-corrupt distinction is treated as settled in the PRD but is unresolved in the addendum (§4.2 FR-2; Glossary "Validation status"; addendum §1, §4 item 2) — FR-2 asserts as testable that files resolve specifically to *error-password* vs *error-corrupt*, and UJ-2 dramatizes exactly that ("One file resolves to *Password protected*, one to *Could not read file*"). But addendum §4 lists "distinguish *error-password* from *error-corrupt*" as a still-open library-dependent decision. The PRD body carries no flag that this is the build's riskiest dependency. *Fix:* add a `[NOTE FOR PM]` or `[ASSUMPTION]` on FR-2 stating that reliable separation of encrypted vs corrupt depends on the PDF library/platform API chosen in architecture, and define the fallback contract (e.g., "if the library cannot distinguish, both resolve to *error-corrupt* and the password UJ degrades to a generic 'Could not read file'").

## Downstream usability — strong

This PRD is built to feed UX → architecture → stories, and it does so cleanly. A real Glossary (§3) defines every domain noun (File list, List item, Validation status, Flagged file, Valid file, Selected file, Preview pane, Preview toolbar, Action bar, Save dialog, Output filename, Merge) and the body uses them verbatim — capitalization is consistent (e.g., "File list," "Valid files," "Flagged file" hold their forms across FR-6, FR-8, FR-10). The instruction "Downstream workflows must use these terms verbatim. No synonyms elsewhere" is honored in practice.

IDs are contiguous and unique: FR-1…FR-12 (no gaps), UJ-1…UJ-4, SM-1…SM-4 + SM-C1/SM-C2, NFR-1…NFR-7. Cross-references resolve: SMs name the FRs/NFRs they validate, FRs name the UJs they realize, FR-6 points to FR-10, FR-8 points to NFR-1, §4.4 points back to §4.2. Each section is reasonably self-contained — features carry their own Description + Consequences, and the IA (§11) is independently legible. UJs each have a named protagonist carrying inline context; no floating UJs.

The architecture handoff is unusually well-staged: the addendum is explicitly scoped as "input for `bmad-create-architecture`, not committed architecture," maps every legacy concern to a native target, and isolates three deferred decisions cleanly. The one downstream snag is the cross-document one already raised under Scope honesty — Open Question §12 references "addendum.md" and an "Open Question 2," but the PRD body has only one numbered Open Question (§12); "Open Question 2" exists only in the addendum's framing. See Mechanical notes.

### Findings
- **low** "Open Question 2" referenced but not numbered in the PRD body (§12; addendum §1 PDF-merge row, §4 item 1) — The addendum refers to "PRD Open Question 2," and FR-7's surrounding text and §12 speak of the library question, but §12 contains a single numbered item. A downstream reader resolving "Open Question 2" finds no such ID in the PRD. *Fix:* either renumber/align so the addendum cites "Open Question 1," or add the second open item explicitly to §12.

## Shape fit — strong

The shape matches the product. This is a consumer-facing single-screen utility with meaningful UX, and the PRD correctly treats UJs with named protagonists as load-bearing (UJ-1…UJ-4 each map to FR coverage) without over-formalizing — four UJs, no more, for a four-feature-group app is proportionate, not padded. It is also correctly identified and built as chain-top: the §0 Document Purpose names the downstream consumers ("downstream UX, architecture, and epics workflows"), IA is captured at PRD level "to feed" the fresh UX spec, and implementation specifics are deliberately pushed to the addendum so the PRD body stays capability-focused. That is exactly the chain-top discipline the rubric asks for.

Rigor is calibrated to the small surface area: there's no forced market-sizing, no competitive matrix beyond the one comparison the Vision actually needs, no persona sprawl. Nothing is over-formalized (no UJ density inappropriate for the scale) and nothing consumer-critical is under-formalized (preview, error/safety, and reorder all have explicit UX-shaping consequences). The brownfield-adjacent dimension (a re-platform from a legacy plan) is handled honestly: §3 of the addendum traces legacy decision provenance, and the PRD distinguishes carried-forward decisions from new ones, so the "faithful re-platform, not a re-scope" claim is auditable rather than asserted.

## Mechanical notes

- **Glossary drift:** None material. Domain nouns hold their casing and form across the document (File list, List item, Validation status, Flagged file, Valid file, Selected file, Merge). The Validation status values (*checking* / *valid* / *error-password* / *error-corrupt*) are used consistently, though the user-facing strings drift slightly from the internal state names — Glossary/FR-2 use *error-password*/*error-corrupt* while UJ-2 and FR-2 surface "Password protected" / "Could not read file." This is intentional (internal state vs. microcopy) and harmless, but worth a one-line Glossary note so the UX spec knows the user-facing strings are the canonical copy.
- **ID continuity:** Clean. FR-1…FR-12, UJ-1…UJ-4, NFR-1…NFR-7, SM-1…SM-4 + SM-C1/SM-C2 are all contiguous and unique with no gaps or duplicates.
- **Cross-reference integrity:** Internal refs resolve (SM→FR/NFR, FR→UJ, FR-6→FR-10, FR-8→NFR-1). The one mismatch is "Open Question 2" (addendum) vs. the single numbered item in PRD §12 — see Downstream usability finding.
- **Assumptions Index roundtrip:** Clean. The one inline `[ASSUMPTION]` (FR-7, last-output-location) is indexed in §13, and §13 carries no orphan entries. §13 also helpfully records which prior assumptions were resolved and closed.
- **`[NOTE FOR PM]` / `[NON-GOAL]` placement:** Appropriate and sparse — `[NOTE FOR PM]` on page-range (§6.2) sits at a genuine deferral; `[NON-GOAL for MVP and beyond]` on monetization (§5) correctly marks permanence.
- **Required sections:** All present and proportionate for a chain-top consumer-utility PRD (Vision, Target User + JTBD + Non-Users + UJs, Glossary, Features w/ testable consequences, Non-Goals, MVP Scope, Success Metrics w/ counter-metrics, NFRs, Constraints, Aesthetic/Tone, IA, Open Questions, Assumptions Index, plus the architecture-input addendum).
