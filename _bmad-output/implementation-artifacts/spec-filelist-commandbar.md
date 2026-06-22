---
title: 'File-list CommandBar (relocate Add / Remove)'
type: 'refactor'
created: '2026-06-22'
status: 'in-progress'
context: []
baseline_commit: '1c49d18b868070977385242c398b4796596f00e2'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The two file-management actions are scattered — **Add PDF(s)** sits in the bottom action bar and **Remove** is a glyph-only button floating above the preview pane. Neither lives next to the file list it acts on, so the relationship between the buttons and the list is not obvious.

**Approach:** Add a `CommandBar` at the top of the file-list sidebar holding two primary `AppBarButton`s — **Add PDF(s)** (add icon) and **Remove** (trash icon), labels below the icons. Delete the old bottom-bar Add button and the preview-toolbar Remove button. This is a UI relocation only; both buttons reuse their existing view-model commands, so command gating (disabled during merge; Remove needs a selection) carries over unchanged.

## Boundaries & Constraints

**Always:**
- The `CommandBar` is always visible — including when the file list is empty — so the user can always add the first file.
- Both buttons bind to the existing `AddFilesCommand` / `RemoveCommand` (reuse, do not re-implement). Disabled/enabled state must come from the commands' `CanExecute`, not new code.
- The **Merge** button stays where it is (bottom-right action bar).
- Labels render below the icons (`DefaultLabelPosition="Bottom"`).

**Ask First:**
- Touching the `MainViewModel` command logic, or adding/renaming any command.
- Changing Merge's position or the merge UI-lock behavior.

**Never:**
- No new commands, services, or strings beyond what relocation needs.
- Do not rewrite the locked UX docs (`EXPERIENCE.md`/`DESIGN.md`) — the divergence is intentional and out of scope here.
- Do not add a `…` overflow menu or secondary commands.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| App launch (empty list) | `Files` empty, nothing selected | CommandBar visible at top of sidebar; **Add PDF(s)** enabled; **Remove** disabled | N/A |
| File selected | `SelectedFile != null`, not merging | **Remove** enabled | N/A |
| No selection | `SelectedFile == null` | **Remove** disabled | N/A |
| During merge | `IsMerging == true` | **Add PDF(s)** and **Remove** both disabled (Merge also disabled via `CanMerge`) | N/A |
| After Remove | item removed, selection cleared | **Remove** returns to disabled; list/preview update as before | N/A |

</frozen-after-approval>

## Code Map

- `pdfjunior/MainWindow.xaml` — the only file changed. Sidebar (`Grid.Column="0"`), preview toolbar `StackPanel` (rows 87–98), and bottom action bar (rows 156–176) all live here.
- `pdfjunior/ViewModels/MainViewModel.cs` — reference only. `AddFilesCommand` (`CanAddFiles` = `!IsMerging`) and `RemoveCommand` (`CanRemove` = `SelectedFile != null && !IsMerging`) already exist; no edits.

## Tasks & Acceptance

**Execution:**
- [x] `pdfjunior/MainWindow.xaml` — In the sidebar grid (`Grid.Column="0"`), wrap the existing ListView + empty-placeholder in a 2-row grid: row 0 (`Height="Auto"`) holds a new `CommandBar` (`DefaultLabelPosition="Bottom"`, `OverflowButtonVisibility="Collapsed"`, `IsOpen="False"`, `Background="Transparent"`) with two `AppBarButton`s: `Label="Add PDF(s)"` + `<SymbolIcon Symbol="Add"/>` bound to `AddFilesCommand`, and `Label="Remove"` + `<SymbolIcon Symbol="Delete"/>` bound to `RemoveCommand` (Mode=OneTime). Row 1 holds the existing ListView/placeholder grid unchanged. — relocate the two actions next to the file list.
- [x] `pdfjunior/MainWindow.xaml` — Delete the preview-toolbar `StackPanel` (the **Remove** glyph button) and its `RowDefinition Height="Auto"`; the preview `Grid` collapses to a single content row. — remove the now-relocated Remove control.
- [x] `pdfjunior/MainWindow.xaml` — Delete the **Add PDF(s)** `Button` from the bottom action bar, leaving only the **Merge** button (and its tooltip `Border` wrapper) right-aligned. — remove the now-relocated Add control.

**Acceptance Criteria:**
- Given the app is launched with an empty list, when the window renders, then a CommandBar sits at the top of the sidebar with **Add PDF(s)** and **Remove** buttons (label under icon), the bottom bar shows only **Merge**, and the preview pane has no Remove button.
- Given no file is selected, when the user looks at the CommandBar, then **Remove** is disabled and **Add PDF(s)** is enabled.
- Given a merge is running (`IsMerging`), when the CommandBar is shown, then both **Add PDF(s)** and **Remove** are disabled, and they re-enable when the merge finishes.
- Given a valid file is selected, when **Remove** is clicked, then the file is removed and selection clears (existing behavior, now driven from the CommandBar).

## Spec Change Log

- **2026-06-22 — CommandBar → StackPanel of standalone AppBarButtons.** *Finding (live user test):* "the label is not visible." A closed `CommandBar` (`IsOpen="False"`) with `DefaultLabelPosition="Bottom"` hides primary-command labels — Bottom labels render only when the bar is open. *Amendment:* replaced the `CommandBar` wrapper with a horizontal `StackPanel` holding the two standalone `AppBarButton`s, which render the label beneath the icon and keep it always visible. *Known-bad avoided:* a header toolbar whose Add/Remove labels stay invisible. *KEEP:* the two `AppBarButton`s, their `Symbol="Add"`/`Symbol="Delete"` icons, command bindings, always-visible placement above the list, and gating via `CanExecute`.

## Design Notes

`AppBarButton` derives from `ButtonBase`, so the existing `Command`/`CanExecute` bindings work as-is — no `IsEnabled` plumbing. Standalone (outside a `CommandBar`) it renders the icon with the label centered beneath it, always visible. `Symbol="Delete"` is the trash glyph (E74D, same as today's button); `Symbol="Add"` is the plus (E710).

## Verification

**Commands:**
- `dotnet build pdfjunior/pdfjunior.csproj -c Debug -p:Platform=x64 -r win-x64` — expected: build succeeds with no XAML compile (XLS) errors.

**Manual checks:**
- Launch the app: CommandBar appears at the top of the sidebar with **Add PDF(s)** + **Remove** (labels beneath icons); bottom bar shows only **Merge**; preview pane has no Remove button.
- **Remove** is disabled until a file is selected; **Add PDF(s)** opens the picker; during a merge both are disabled and re-enable after completion.
