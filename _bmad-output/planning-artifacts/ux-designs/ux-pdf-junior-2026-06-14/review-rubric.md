# Spine Pair Review — PDF Junior

## Overall verdict

The spine pair is **strong** — among the best-shaped for a utility app of this scope. EXPERIENCE.md is notably well-built: the Microcopy Inventory commits verbatim strings, the State Patterns table covers every lifecycle beat, and the Key Flows faithfully mirror the PRD's four user journeys with climax beats and failure paths. DESIGN.md correctly declares the "zero brand-layer" posture and lets WinUI theme resources do the work. The main structural gaps are a missing Inspiration & Anti-patterns section (triggered by competitive references in sources), two failure-path omissions on Key Flows 2 and 3, and three components with behavioral specs in EXPERIENCE.md that lack visual-spec rows in DESIGN.md. No critical severity findings; most issues are medium or low.

---

## 1. Flow coverage — strong

All four PRD user journeys (UJ-1 through UJ-4) map to Key Flows 1-4. Each has a named protagonist (Marcus, Sophie, David), numbered steps, and a climax beat. Flow 1 and Flow 4 include explicit `Failure:` sections.

### Findings

- **medium** Flow 2 (Sophie, error recovery) has no explicit failure path (EXPERIENCE.md, Flow 2 end). Sophie's flow assumes she correctly identifies and removes the flagged files. A plausible failure: she removes the wrong file, or a file that was checking resolves to flagged after she already started a merge attempt. *Fix:* Add a `Failure:` clause -- e.g., "Sophie removes a valid file by mistake; she re-adds it via Add PDF(s) and it re-validates."
- **medium** Flow 3 (Marcus, intentional overwrite) has no explicit failure path (EXPERIENCE.md, Flow 3 end). A plausible failure: the overwrite target is locked by another application. *Fix:* Add a `Failure:` clause -- e.g., "The file is open in another app; the merge fails with 'Access denied' (MC-16). Marcus closes the other app and retries."

---

## 2. Token completeness — strong

DESIGN.md declares 11 color tokens, 3 typography ramps, 2 corner-radius tokens, and 1 spacing token. All color tokens reference WinUI theme resources (no hex values), which is correct and intentional for a zero-override design. Component tokens in the YAML all resolve to declared color/rounded tokens.

### Findings

- **low** `list-view-item-selected` references `{SystemAccentColorLight2}` (DESIGN.md frontmatter, line 69), which is a raw WinUI resource not declared as a token in the YAML `colors` block. All other component tokens reference `{colors.*}` or `{rounded.*}` paths. *Fix:* Add `accent-light2: '{SystemAccentColorLight2}'` to the `colors` block and reference it as `{colors.accent-light2}` in the component, or note that the raw resource reference is intentional.
- **low** No contrast targets stated anywhere in DESIGN.md. The document delegates to WinUI's built-in AA compliance, but the `{colors.critical}` text on `{colors.surface}` combination (used for flagged-file captions) is load-bearing and its contrast ratio is not asserted. The Drift example similarly delegates to shadcn but states "brand overrides verified to maintain ratios" in the EXPERIENCE.md Accessibility Floor. *Fix:* Add a one-line note in Colors: "All WinUI semantic brushes meet AA contrast by design; no custom combinations introduced."
- **low** The `caution` token (`{SystemFillColorCautionBrush}`) is declared in the YAML frontmatter (line 23) but never referenced in the prose, in any component spec, or in EXPERIENCE.md. It appears to be an unused token. *Fix:* Remove `caution` from the frontmatter or add a note explaining its reservation.

---

## 3. Component coverage — adequate

DESIGN.md names 11 component roles in its Components table. EXPERIENCE.md names 13 components in its Component Patterns table. Most cross-reference correctly, with visual specs in DESIGN.md and behavioral specs in EXPERIENCE.md.

### Findings

- **medium** **List item** has a full behavioral spec in EXPERIENCE.md Component Patterns (line 109) — filename display, caption-styled status line, critical color for flagged captions, selectable/removable/reorderable at all times — but no dedicated visual-spec row in DESIGN.md Components. DESIGN.md describes it only as a sub-clause of the ListView row ("Items show filename + caption-sized status/page-count"). A downstream developer needs the list-item visual contract (spacing between filename and caption, critical-color application, selected vs. unselected states) in one place. *Fix:* Add a `List item` row to DESIGN.md Components specifying: filename in Body style, status in Caption style, flagged status uses `{colors.critical}`, selected highlight uses `{colors.accent}` via the existing `list-view-item-selected` component token.
- **medium** **Preview toolbar** has behavioral rules in EXPERIENCE.md (line 111) — right-aligned, grouped Move buttons with gap before Remove, all disabled when nothing selected — but no visual-spec row in DESIGN.md Components. DESIGN.md mentions it in Layout & Spacing (line 104) but not in the Components table. *Fix:* Add a `Preview toolbar` row to DESIGN.md Components: layout direction, button grouping, gap specification.
- **low** **Drag divider** has behavioral rules in EXPERIENCE.md (line 120) — double-click reset, cursor change, min/max widths — but no visual-spec row in DESIGN.md Components. DESIGN.md mentions it in Layout & Spacing (line 102). For a standard WinUI GridSplitter this is arguably unnecessary, but the behavioral spec in EXPERIENCE.md implies a component that should have a visual-spec counterpart. *Fix:* Add a minimal `Drag divider` row to DESIGN.md Components or note in Layout & Spacing that this uses a standard WinUI GridSplitter with no visual overrides.
- **low** **TextBlock** (state placeholders) has a visual-spec row in DESIGN.md Components (line 141) but no matching named row in EXPERIENCE.md Component Patterns. Its behavioral spec is distributed across State Patterns and Microcopy Inventory, which is functionally complete, but the component name doesn't appear in both tables. *Fix:* No action required; the coverage through State Patterns and Microcopy Inventory is adequate. Note for mechanical consistency only.

---

## 4. State coverage — strong

The State Patterns table in EXPERIENCE.md covers 16 named states across the full lifecycle: launch, checking, validated, selected (valid/flagged), removed, reordered, merge pressed, save cancelled, merge executing (<2s / >=2s), success, failure, folder-gone, close-during-merge, and validation timeout. For a single-window, no-network, no-account desktop utility, this is comprehensive.

### Findings

- **low** No explicit "all files removed" state listed. The "File removed" state says "If the list is now empty, sidebar shows MC-1. Merge-enabled recalculated." This implicitly covers the transition back to the app-launch state, but making this a named row (or a note on the File removed row) would help a developer handle the edge case where a user adds files, removes all of them, and the app returns to its initial state. *Fix:* Add a note to the File removed row: "If the last file is removed, the app returns to the App launch state (MC-1 in sidebar, MC-2 in preview, Merge disabled with MC-10 tooltip)."

---

## 5. Visual reference coverage — adequate (for scope)

No `mockups/`, `wireframes/`, or `imports/` directories exist. Neither spine references any visual asset files. For a zero-override WinUI app where the visual language is entirely defined by platform defaults, this is defensible — there is no brand layer to illustrate.

### Findings

- **low** A single wireframe showing the two-pane layout (sidebar, drag divider, preview toolbar, preview pane, progress indicator area, action bar) would help downstream consumers visualize the IA. The IA section describes the layout in prose and table, but a spatial reference would resolve ambiguity about relative positioning (e.g., where exactly does the progress indicator sit "above the Action bar"?). *Fix:* Optional. Add a simple wireframe or ASCII layout diagram to the IA section of EXPERIENCE.md, or create a `wireframes/` directory with a single layout reference. Not blocking.

---

## 6. Bloat & overspecification — strong

Both spines are lean for a utility app. DESIGN.md carries appropriate editorial voice in the "no posture" brand statement without overwriting. EXPERIENCE.md's Microcopy Inventory is the right kind of overspecification — it commits verbatim strings that directly translate to `UiStrings.cs`. No persona restatement, no scope recap, no decorative narrative.

### Findings

- **low** EXPERIENCE.md's IA section restates window dimensions (640x480, 900x640), sidebar widths (280px, min 200px, max 50%), and drag divider behavior that also appear in DESIGN.md Layout & Spacing. Both files carry the same pixel specs. *Fix:* In EXPERIENCE.md's IA section, add a brief note: "Visual dimensions per DESIGN.md Layout & Spacing." Then trim the duplicated pixel values, or explicitly acknowledge the duplication as intentional (behavioral IA context vs. visual spec).
- **low** The Component Patterns table in EXPERIENCE.md repeats "AccentButtonStyle" for the Merge button and the 2-second ProgressBar threshold, which are visual/implementation details that belong in DESIGN.md. *Fix:* These are borderline — they serve as behavioral context for downstream developers reading EXPERIENCE.md in isolation. No action required; note for awareness.

---

## 7. Inheritance discipline — strong

Source frontmatter in both files resolves to existing documents. UJ names and persona names (Marcus, Sophie, David) are verbatim from the PRD. Glossary terms (File list, List item, Flagged file, Valid file, Selected file, Preview pane, Preview toolbar, Action bar, Merge, File picker, Save dialog) are used consistently across both spines and the PRD. The `{colors.critical}` token reference in EXPERIENCE.md resolves to DESIGN.md's frontmatter.

### Findings

- **low** DESIGN.md sources list `../../architecture.md`, but the architecture document lists DESIGN.md and EXPERIENCE.md as its own input documents (architecture.md frontmatter, lines 9-10). This is a circular source reference. In practice, the architecture was likely authored concurrently or after the UX spines. *Fix:* Remove `../../architecture.md` from DESIGN.md's `sources` frontmatter (it was not a source *for* DESIGN.md; DESIGN.md was a source *for* it), or add a note explaining the bidirectional relationship.
- **low** EXPERIENCE.md uses "Sidebar" as a surface name in the IA table (line 25), while the PRD glossary does not define "Sidebar" as a term — it uses "left sidebar" descriptively. The term is clear in context and unlikely to cause confusion. *Fix:* None required; note for completeness.

---

## 8. Shape fit — adequate

**DESIGN.md** follows the canonical section order exactly: Brand & Style, Colors, Typography, Layout & Spacing, Elevation & Depth, Shapes, Components, Do's and Don'ts. All sections present.

**EXPERIENCE.md** has: Foundation, Information Architecture, Voice and Tone, Microcopy Inventory (invented), Component Patterns, State Patterns, Interaction Primitives, Accessibility Floor, Key Flows. All required defaults present. Responsive & Platform is omitted (single fixed desktop surface, no breakpoints — defensible, follows the Quill example precedent).

### Findings

- **medium** **Inspiration & Anti-patterns section is missing** from EXPERIENCE.md. Per shape-fit rules, this section is required-when-applicable and is triggered when sources or the decision log show reference products or rejects. The PRD's Vision (Section 1) explicitly names competitive products and their failings: Adobe Acrobat (subscription-gated), web tools (privacy concern), PDF24/PDFgear (heavyweight). The decision log records rejected design alternatives (drag-to-reorder, auto-scroll, 30s timeout). The PRD's Non-Goals and Aesthetic/Tone sections contain anti-patterns (splash screens, onboarding, upsell, AI prompts). These lifts and rejects are currently scattered across source documents; collecting them in an Inspiration section would prevent a downstream developer from independently re-deriving (and potentially re-introducing) rejected patterns. *Fix:* Add an Inspiration & Anti-patterns section to EXPERIENCE.md. Candidate entries: "Lifted from Windows Calculator/Settings: the zero-brand posture — look like a native OS feature, not a third-party app." "Rejected: drag-to-reorder (decided — Move up/down buttons; see decision log #13)." "Rejected: splash screen, onboarding, what's-new modal (PRD Section 10 hard anti-patterns)." "Rejected: rating prompts, upsell, gamification." "Rejected: auto-scroll-to-reveal on file add (decision log #9)."
- **low** **Microcopy Inventory** is an invented section not in the canonical list. It earns its place: the 23 canonical strings (MC-1 through MC-23) are directly consumed by `UiStrings.cs` in the architecture, and their verbatim wording is a load-bearing contract. This is a well-justified addition for a utility app where microcopy IS the voice.

---

## Mechanical notes

**Frontmatter completeness:**
- DESIGN.md: `status: draft` — should this be `final` if the architecture has already consumed it? The architecture.md lists it as an input document and is marked `status: complete`.
- EXPERIENCE.md: `status: draft` — same question.
- Both spines list `updated: 2026-06-14`, consistent with the decision log dates.

**Cross-references:**
- EXPERIENCE.md references "`DESIGN.md.Components`" in the Component Patterns header — this resolves correctly.
- EXPERIENCE.md references "`DESIGN.md`" in Voice and Tone and Accessibility Floor — resolves correctly.
- The decision log entries cross-reference reconcile-ux.md headings (H1, H2, H3, M2, M3, L3) and PRD sections (Section 9.1, FR-5, FR-10, FR-12) — all resolve.

**Name inconsistencies:**
- DESIGN.md Components table uses WinUI control names (`ListView`, `Button`, `InfoBar`, `ProgressBar`, `ContentDialog`, `ScrollViewer`, `TextBlock`). EXPERIENCE.md Component Patterns uses product-domain names (`File list`, `Merge button`, `Preview pane`). Both approaches are valid within their respective spines (visual spec vs. behavioral spec), and the mapping is clear from parenthetical annotations. No fix needed.
- EXPERIENCE.md MC-11 says "Remove files with errors before merging" while PRD FR-10 describes the condition as "flagged-file-present." The EXPERIENCE.md string is the canonical user-facing copy; the PRD term is the internal glossary term. Consistent.

**Mermaid syntax:** No Mermaid diagrams in either spine. Not required.

**Decision log alignment:** All 15 decision log entries have corresponding treatments in the spines. Entries 3 (three tooltips), 4 (per-situation tone), 5 (sub-2s silence), 6 (preview unchanged on reorder), 7 (banner dismissed on merge), 8 (exclusion notice only), 9 (no auto-scroll), 10 (5s timeout) are all implemented in EXPERIENCE.md's State Patterns and Component Patterns. Good traceability.
