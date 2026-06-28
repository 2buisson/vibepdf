---
title: 'Localization (multi-language) with French translation'
type: 'feature'
created: '2026-06-28'
status: 'in-progress'
baseline_commit: '5064811427036654c316cae87c0ad80811614086'
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** All UI text is hard-coded English (const `UiStrings` facade + four inline XAML literals), so Vibe PDF cannot present in any other language. We want true multi-language support and a complete French translation.

**Approach:** Adopt the native WinUI 3 / MRT localization pattern. Move every user-facing string into per-locale `Resources.resw` files (`en-US` default + `fr-FR`), drive XAML via `x:Uid` and code-behind via `ResourceLoader`, and delete the `UiStrings` facade. The app follows the Windows display language automatically with English fallback.

## Boundaries & Constraints

**Always:** Keep English as the default/fallback locale (`DefaultLanguage = en-US`). Migrate **every** current `UiStrings` member to resources verbatim (English) and provide a French equivalent for each. Preserve all format placeholders exactly (`{0}`, `{1:0}`, `{1:0} %` for FR). Both `.resw` files must hold an identical key set. No behavior change beyond text source — merge gating, validation, preview, dialogs work unchanged.

**Ask First:** Adding any new in-app UI (e.g. a language switcher) — out of scope unless renegotiated. Dropping the 7 currently-unreferenced strings (see Design Notes) instead of carrying them over.

**Never:** No in-app language picker, no runtime culture switching, no persisted language override. No third-party localization library. Do not touch validation/merge/preview logic. Do not change brand name "Vibe PDF".

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| French OS | Windows display language = French | Entire UI in French: titlebar, buttons, tooltips, statuses, placeholders, dialogs | N/A |
| Other OS | display language = English / any non-FR | Entire UI in English (en-US fallback) | N/A |
| Format strings | page count / merge % / filename rendered | Placeholders filled; `Vibe PDF — 55 %` (FR) vs `— 55%` (EN); `3 pages` / `3 pages` | N/A |
| Missing key in a locale | `x:Uid` / `GetString` lookup | en-US fallback value used; never blank or crash | Key-set parity check across both `.resw` |

</frozen-after-approval>

## Code Map

- `vibepdf/MainWindow.xaml` -- all XAML UI; hosts the 4 `x:Uid` targets + custom titlebar TextBlock; declares `xmlns:strings`
- `vibepdf/MainWindow.xaml.cs` -- ~22 code-driven string sites (statuses, tooltips, dialog, titlebar format)
- `vibepdf/Strings/UiStrings.cs` -- current const facade → **delete** after migration
- `vibepdf/Strings/en-US/Resources.resw` -- **new** English (default/fallback) resources
- `vibepdf/Strings/fr-FR/Resources.resw` -- **new** French resources
- `vibepdf/vibepdf.csproj` -- add `DefaultLanguage` + ensure `.resw` compile to `resources.pri`
- `vibepdf/Package.appxmanifest` -- already `<Resource Language="x-generate"/>`; auto-registers locales from the PRI (no edit needed)

## Tasks & Acceptance

**Execution:**
- [x] `vibepdf/Strings/en-US/Resources.resw` -- author all keys with English values migrated verbatim from `UiStrings` (XAML keys dotted: `AddButton.Label`, `RemoveButton.Label`, `MergeButton.Content`, `EmptyListPlaceholder.Text`; code keys flat: `AppTitle`, `StatusChecking`, … per Design Notes)
- [x] `vibepdf/Strings/fr-FR/Resources.resw` -- identical key set, French values from the Design Notes table
- [x] `vibepdf/Strings/UiStrings.cs` -- delete; strings now live in `.resw`
- [x] `vibepdf/MainWindow.xaml` -- remove `xmlns:strings` and the Window `Title` attribute; add `x:Uid` to AddButton/RemoveButton/MergeButton/EmptyListPlaceholder and drop their inline `Label`/`Content`/`Text` literals & x:Binds; remove `TitleText` x:Bind (now code-driven)
- [x] `vibepdf/MainWindow.xaml.cs` -- swap `using vibepdf.Strings` → `using Microsoft.Windows.ApplicationModel.Resources`; add `private static readonly ResourceLoader Resources = new();`; replace every `UiStrings.X` with `Resources.GetString("X")`; in the ctor set `Title = Resources.GetString("AppTitle")` and call `UpdateTitle()` for the initial titlebar text
- [x] `vibepdf/vibepdf.csproj` -- add `<DefaultLanguage>en-US</DefaultLanguage>`; add explicit `<PRIResource Include="Strings\**\*.resw" />` only if the default glob does not already compile them (verify via build)

**Acceptance Criteria:**
- Given the migration is complete, when `dotnet build` runs, then it succeeds with zero remaining `UiStrings`/`vibepdf.Strings` references and both `.resw` compiled into `resources.pri`.
- Given both locale files, when their keys are compared, then en-US and fr-FR contain an identical key set (no missing/extra key).
- Given a merge at 55%, when the titlebar updates under each locale, then it reads `Vibe PDF — 55%` (EN) / `Vibe PDF — 55 %` (FR) with the value formatted correctly.
- Given the existing test suite, when `dotnet test` runs, then all tests still pass (no string coupling introduced).

## Spec Change Log

## Design Notes

**.resw shape** (ResX schema) and call-site swap — golden example:
```xml
<data name="MergeButton.Content" xml:space="preserve"><value>Fusionner</value></data>
<data name="AppTitle" xml:space="preserve"><value>Vibe PDF</value></data>
```
```xml
<!-- XAML --> <Button x:Uid="MergeButton" x:Name="MergeButton" .../>
```
```csharp
// code-behind: UiStrings.AppTitle  ->  Resources.GetString("AppTitle")
```
`new ResourceLoader()` (Windows App SDK MRT) resolves the default `Resources` map against the current OS language with en-US fallback — no extra wiring.

**Carried-over unused keys:** `MergeErrorDiskFull`, `MergeErrorAccessDenied`, `MergeErrorFileMissing`, `CloseGuardTitle`, `CloseGuardBody`, `CloseGuardKeepMerging`, `CloseGuardCloseAnyway` have no current consumer (planned story 2.3) but are migrated + translated for parity. Drop only on request.

**French translation table** (key → FR; English value = current `UiStrings`):
| Key | French |
|-----|--------|
| AppTitle | Vibe PDF |
| AppTitleMergeProgress | {0} — {1:0} % |
| EmptyListPlaceholder.Text (EmptyFileListPlaceholder) | Ajoutez des PDF pour commencer |
| EmptyPreviewPlaceholder | Sélectionnez un fichier pour l'aperçu |
| StatusChecking / PreviewChecking | Vérification… |
| StatusValidSingular | {0} page |
| StatusValidPlural | {0} pages |
| StatusErrorPassword | Protégé par mot de passe |
| StatusErrorCorrupt | Lecture du fichier impossible |
| StatusErrorTimeout | Lecture du fichier impossible (délai dépassé) |
| PreviewPasswordExclusion | Ce fichier est protégé par mot de passe et sera exclu de la fusion. |
| PreviewCorruptExclusion | Ce fichier n'a pas pu être lu et sera exclu de la fusion. |
| MergeDisabledNoFiles | Ajoutez au moins un PDF à fusionner |
| MergeDisabledFlaggedFiles | Supprimez les fichiers en erreur avant de fusionner |
| MergeDisabledStillChecking | En attente de la vérification des fichiers |
| DefaultMergeFileName | fusion.pdf |
| MergeSuccess | Fusion réussie — {0} |
| MergeSuccessOpenFolder | Ouvrir le dossier |
| MergeErrorDiskFull | Échec de la fusion — Espace insuffisant sur {0}. |
| MergeErrorAccessDenied | Échec de la fusion — Accès refusé |
| MergeErrorFileMissing | Échec de la fusion — Fichier introuvable : {0} |
| MergeErrorGeneric | Échec de la fusion. Réessayez ou vérifiez les fichiers. |
| FolderNotFound | Dossier introuvable |
| DialogClose | Fermer |
| AddButton.Label | Ajouter des PDF |
| RemoveButton.Label | Supprimer |
| MergeButton.Content | Fusionner |
| CloseGuardTitle | Fusion en cours |
| CloseGuardBody | Une fusion est toujours en cours. Fermer maintenant peut laisser un fichier incomplet à destination. |
| CloseGuardKeepMerging | Continuer la fusion |
| CloseGuardCloseAnyway | Fermer quand même |

## Verification

**Commands:**
- `dotnet build vibepdf/vibepdf.csproj -p:Platform=x64 -r win-x64` -- expected: succeeds; no errors from removed `UiStrings`; `resources.pri` regenerated.
- `dotnet test vibepdf.Tests/vibepdf.Tests.csproj -p:Platform=x64 -r win-x64` -- expected: all existing tests pass.

**Manual checks:**
- Run on an English Windows session → UI in English. Switch Windows display language to French and relaunch → all surfaces French: Add/Remove/Merge labels, empty placeholders, validation statuses (valid/password/corrupt/timeout), disabled-Merge tooltip, merge-success & error dialog, titlebar merge progress.
- Inspect both `.resw` → identical `<data name>` key sets.

## Review Findings

_Code review 2026-06-28 (Blind Hunter + Edge Case Hunter + Acceptance Auditor). Auditor verified the localization payload itself is spec-compliant: all 29 `UiStrings` migrated verbatim to en-US, FR values match the Design Notes table, 32/32 key-set parity, EN `{1:0}%` vs FR `{1:0} %` correct, 7 carried-over unused keys present, `DefaultLanguage=en-US` added, zero remaining `UiStrings`/`vibepdf.Strings` references. Edge Hunter confirmed via `dotnet build` + `makepri dump` that both locales compile into `resources.pri` (AC #1 ✓). Remaining gate: `dotnet test` for AC #4._

**Decision-needed (resolved 2026-06-28):**
- [x] [Review][Decision] Scope creep — `GenerateTemporaryStoreCertificate` bundled in csproj → **RESOLVED: split out** — exclude from the localization commit; commit separately as `chore: publishing`. [vibepdf/vibepdf.csproj]
- [x] [Review][Decision] Scope creep — `.gitignore` adds `intents/` → **RESOLVED: split out** — exclude from the localization commit; commit separately as `chore: publishing`. [.gitignore]
- [x] [Review][Decision] French `{1:0} %` ordinary space vs NBSP → **RESOLVED: keep as spec'd** — matches the frozen approved value; acceptance met as-is, no change. [vibepdf/Strings/fr-FR/Resources.resw AppTitleMergeProgress]

**Patch:** none — no unambiguous code defect in the localization payload.

**Deferred (tracked in deferred-work.md):**
- [x] [Review][Defer] `ResourceLoader.GetString` returns empty string (never throws) for a key missing from all locales — no guard; latent until story 2.3 wires the `MergeError*`/`CloseGuard*` keys [MainWindow.xaml.cs] — deferred
- [x] [Review][Defer] No automated guard enforcing key-set + format-token parity across the two `.resw` (parity holds today) — consider a small unit test [vibepdf/Strings/**] — deferred
- [x] [Review][Defer] Unguarded `string.Format` at `UpdateTitle`/`FormatStatus` — a malformed localized format string would throw `FormatException`; first-party strings make this low-risk [MainWindow.xaml.cs:461,580] — deferred, pre-existing
- [x] [Review][Defer] French 0-count renders plural ("0 pages"); French treats 0 as singular — `pageCount == 1` selection rule not localized; likely unreachable for a valid PDF [MainWindow.xaml.cs:580] — deferred, pre-existing
- [x] [Review][Defer] Number formatting uses `CurrentCulture` (region) while the string is chosen by display language — possible EN-string/FR-number mismatch; negligible for these integer/percent values [MainWindow.xaml.cs:463,580] — deferred
- [x] [Review][Defer] `static readonly ResourceLoader` first-touch unguarded — `TypeInitializationException` if `resources.pri` cannot load; mitigated by MSIX packaging [MainWindow.xaml.cs:31] — deferred
