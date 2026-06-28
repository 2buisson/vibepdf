---
id: SPEC-remove-mvvm
companions:
  - brownfield.md
sources: []
---

> **Canonical contract.** This SPEC and the files in `companions:` are the complete, preservation-validated contract for what to build, test, and validate. Source documents listed in frontmatter are for traceability only — consult them only if you need narrative rationale or prose color this contract intentionally omits.

# Remove CommunityToolkit.Mvvm — collapse to code-behind

## Why

A vision-to-realize refactor: the maintainer wants the `CommunityToolkit.Mvvm` dependency *and* the MVVM pattern it underpins gone — no View/ViewModel split, no source-generated `[ObservableProperty]`/`[RelayCommand]`, no binding-driven view indirection — replaced by a single `MainWindow` that owns its state and drives controls imperatively through event handlers. This is not a behavior change: vibepdf must build, run, and behave identically afterward. The whole of vibepdf's UI logic lives in one place today (`MainViewModel` + its `x:Bind`-only view), so the blast radius is contained but the async/concurrency correctness in that class is subtle and easy to lose in a rewrite — see `brownfield.md`.

## Capabilities

- id: CAP-1
  intent: Remove the CommunityToolkit.Mvvm dependency from the project entirely.
  success: No `CommunityToolkit.Mvvm` `PackageReference` and no `using CommunityToolkit.Mvvm.*` remain anywhere; `dotnet build vibepdf/vibepdf.csproj -p:Platform=x64 -r win-x64` succeeds.

- id: CAP-2
  intent: Collapse the View/ViewModel split into MainWindow code-behind that owns state and updates controls imperatively.
  success: `MainViewModel` and the `ViewModels/` folder are deleted; `MainWindow.xaml` carries no `{x:Bind ViewModel.*}` or `Command=` bindings; every interaction runs through a code-behind `Click`/`SelectionChanged`/event handler rather than an `ICommand` or an `INotifyPropertyChanged`-driven binding.

- id: CAP-3
  intent: Preserve every existing user-facing behavior of the app through the refactor.
  success: Each item in `brownfield.md` §"Behavior-parity checklist" demonstrably holds in an F5 walkthrough, and the service-layer test suite (`PdfValidationServiceTests`, `PdfSharpMergeServiceTests`) stays green under `dotnet test … -p:Platform=x64 -r win-x64`.

## Constraints

- No `CommunityToolkit.Mvvm` — package reference and all usings removed.
- No MVVM architecture: no ViewModel class, no `ICommand`/`RelayCommand` command objects, no `INotifyPropertyChanged`-driven view, no `x:Bind` to a view model. This rules out the "hand-roll INotifyPropertyChanged + ICommand and keep the VM" path — the pattern goes, not just the package.
- Behavior and UX parity is the bar: no feature, layout, styling, or copy (`UiStrings`) changes. All behaviors and edge cases in `brownfield.md` are preserved exactly.
- The subtle async/cancellation logic currently in `MainViewModel` must survive intact — single in-flight preview render with staleness guard, 3-permit validation semaphore, 5s validation timeout, late-completion drop on remove, per-merge cancellation, UI-thread marshalling. Enumerated in `brownfield.md` §"Concurrency behaviors to preserve".
- Scope is MVVM removal only: keep `Microsoft.Extensions.DependencyInjection`, the `Services/*` interface layer, and the `Models/*` types (`ValidationStatus`, `PreviewState`, `MergeOutcome`, `StatusBarState`).
- List rows refresh imperatively: `PdfFileItem` is a plain class with no `INotifyPropertyChanged`. On validation completion the code-behind locates the row container (`FileListView.ContainerFromItem(item)`) and updates its `TextBlock`s directly; the DataTemplate's status `x:Bind` drops to `OneTime`. No data binding on the model.
- `MainViewModelTests` is deleted, not ported. The post-refactor regression net is the service-layer tests plus the manual F5 parity walkthrough in `brownfield.md`; no headless coverage of the merge-gating, selection-shift, or validation-timeout logic is retained.
- Build/test entry points are unchanged: `dotnet build … -p:Platform=x64 -r win-x64` and `dotnet test … -p:Platform=x64 -r win-x64`.

## Non-goals

- No change to app features, window layout, visual styling, theming, or user-visible strings.
- No change to PDF validation, preview, or merge service logic, or to PDFsharp usage.
- Not removing `Microsoft.Extensions.DependencyInjection` or the `Services/*` abstractions — DI of services is not MVVM and stays.
- No new UI-automation harness beyond what the chosen test disposition requires (see Open Questions).

## Success signal

vibepdf builds and runs with zero `CommunityToolkit.Mvvm` references, no `MainViewModel`, and a code-behind-driven `MainWindow`; a full manual walkthrough — add → validate → preview → reorder → remove → merge → progress-in-title → result dialog → open folder — behaves identically to today, and the service-layer test suite stays green.

## Assumptions

- "Identical behavior" is judged against the current `_bmad-output/planning-artifacts/ux-designs/ux-pdf-junior-2026-06-14/EXPERIENCE.md` and the behaviors encoded in today's `MainViewModelTests`, since there is no separate written acceptance-criteria doc.
- `ObservableCollection<PdfFileItem>` may remain as the `ListView` backing store (it is BCL `System.Collections.ObjectModel`, not part of MVVM) to keep live add/remove/reorder, consistent with the chosen approach.
- `Converters/BoolToVisibilityConverter.cs` is already unreferenced dead code (the XAML uses the static `MainWindow.BoolToVisibility` helpers) and may be deleted as part of the cleanup.
