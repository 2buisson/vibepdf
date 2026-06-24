---
title: 'Rename project from pdfjunior to Vibe PDF'
type: 'chore'
created: '2026-06-24'
status: 'done'
baseline_commit: 'a49ed1b7180ed13931744f2390634ad11d648230'
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The product is being rebranded to "Vibe PDF" but the codebase still carries the old `pdfjunior` name in its namespace, build files, app display name, and folder structure.

**Approach:** Rename in two ordered phases so the build can be verified between them: (A) rename all in-file content — the `pdfjunior` namespace/token becomes `vibepdf`, and the app's user-facing `DisplayName` becomes "Vibe PDF" — then verify the build; (B) rename the physical folders, project files, and solution file (`pdfjunior*` → `vibepdf*`), fix the path references that point at them, and verify the build again.

## Boundaries & Constraints

**Always:**
- Identifier form is lowercase `vibepdf` everywhere the lowercase token `pdfjunior` appeared (namespace, `using`, XAML `using:`/`x:Class`, `RootNamespace`, file/folder names, `.slnx` paths, debug-profile labels).
- The human-facing app name `DisplayName`/`Description` in `Package.appxmanifest` becomes the spaced brand "Vibe PDF" (not `vibepdf`).
- Use `git mv` for folder/file renames so history is preserved; carry the untracked `pdfjunior/Models/StatusBarState.cs` along with the folder.
- After each phase, `dotnet build pdfjunior/...` (Phase A) / `vibepdf/...` (Phase B) must succeed before continuing.

**Ask First:**
- Touching anything under `_bmad-output/` (historical PRDs, story specs) or `_bmad/` tooling config (`project_name: pdf-junior`).
- Committing or creating a branch (currently on `master`).

**Never:**
- Change the MSIX `Identity Name` GUID or `PhoneProductId` in `Package.appxmanifest` — that is the Store-assigned package identity.
- Rename the `Assets/*.png` files (their names do not contain the project name).
- Rewrite the `_bmad-output/` planning/implementation history docs.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Lowercase token | `pdfjunior` in `.cs`/`.xaml`/`.csproj`/`.slnx`/`.gitignore`/`.manifest`/`launchSettings`/`.csproj.user` | replaced with `vibepdf` | N/A |
| App display name | `DisplayName`/`Description="pdfjunior"` in manifest | becomes `Vibe PDF` | N/A |
| Store identity | `Identity Name` GUID, `PhoneProductId` | left unchanged | N/A |
| History docs | files under `_bmad-output/` | left unchanged | N/A |

</frozen-after-approval>

## Code Map

- `pdfjunior/pdfjunior.csproj` -- `RootNamespace` (A); file renamed to `vibepdf.csproj` (B)
- `pdfjunior.Tests/pdfjunior.Tests.csproj` -- renamed `vibepdf.Tests.csproj`; `ProjectReference` path (B)
- `pdfjunior.slnx` -- two `<Project Path>` entries; file renamed `vibepdf.slnx` (B)
- `pdfjunior/Package.appxmanifest` -- 3 `pdfjunior` strings → "Vibe PDF"; GUID untouched (A)
- `pdfjunior/App.xaml`, `MainWindow.xaml` -- `x:Class`, `using:` namespaces (A)
- `pdfjunior/**/*.cs`, `pdfjunior.Tests/**/*.cs` -- `namespace`/`using pdfjunior*` (A)
- `pdfjunior/app.manifest` -- `assemblyIdentity name="pdfjunior.app"` (A)
- `pdfjunior/Properties/launchSettings.json`, `pdfjunior/pdfjunior.csproj.user` -- debug-profile labels (A)
- `pdfjunior/.gitignore`, `.gitignore` -- header comments (A)
- `CLAUDE.md` -- build/test command paths (B)

## Tasks & Acceptance

**Execution — Phase A (content rename, no moves):**
- [x] All `.cs` in `pdfjunior/` and `pdfjunior.Tests/` -- replace token `pdfjunior` → `vibepdf` (namespace + using) -- rebrand
- [x] `pdfjunior/App.xaml`, `pdfjunior/MainWindow.xaml` -- replace `pdfjunior` → `vibepdf` in `x:Class`/`using:` -- rebrand
- [x] `pdfjunior/pdfjunior.csproj` -- `RootNamespace` → `vibepdf` -- rebrand
- [x] `pdfjunior/Package.appxmanifest` -- 3 display strings → `Vibe PDF`; leave Identity GUID -- user-facing brand
- [x] `pdfjunior/app.manifest` -- `name="pdfjunior.app"` → `vibepdf.app` -- rebrand
- [x] `pdfjunior/Properties/launchSettings.json`, `pdfjunior/pdfjunior.csproj.user` -- `pdfjunior (Package/Unpackaged)` → `vibepdf (...)` -- keep profile + ActiveDebugProfile in sync
- [x] `pdfjunior/.gitignore`, `.gitignore` -- update header comment -- consistency
- [x] **Verify build (Phase A checkpoint)**

**Execution — Phase B (structural rename):**
- [x] `git mv pdfjunior/pdfjunior.csproj` → `vibepdf.csproj`; `pdfjunior.Tests/pdfjunior.Tests.csproj` → `vibepdf.Tests.csproj` -- rename project files
- [x] `git mv pdfjunior/` → `vibepdf/`; `pdfjunior.Tests/` → `vibepdf.Tests/` (carry untracked `StatusBarState.cs`) -- rename folders
- [x] `git mv pdfjunior.slnx` → `vibepdf.slnx`; update both `<Project Path>` entries -- rename solution + paths
- [x] `vibepdf.Tests/vibepdf.Tests.csproj` -- `ProjectReference` → `..\vibepdf\vibepdf.csproj` -- fix reference
- [x] `CLAUDE.md` -- update build/test command paths -- keep docs runnable
- [x] **Verify build + tests (Phase B checkpoint)**

**Acceptance Criteria:**
- Given a clean checkout, when `dotnet build vibepdf/vibepdf.csproj -p:Platform=x64 -r win-x64` runs, then it succeeds with zero references to `pdfjunior` remaining in built code.
- Given the test project, when `dotnet test vibepdf.Tests/vibepdf.Tests.csproj -p:Platform=x64 -r win-x64` runs, then all tests pass.
- Given a grep for `pdfjunior` outside `_bmad-output/`, when run after Phase B, then there are zero matches.
- Given the rebuilt app, when launched, then its title-bar/Store name reads "Vibe PDF".

## Verification

**Commands:**
- `dotnet build pdfjunior/pdfjunior.csproj -p:Platform=x64 -r win-x64` -- expected: build succeeds (Phase A, old paths)
- `dotnet build vibepdf/vibepdf.csproj -p:Platform=x64 -r win-x64` -- expected: build succeeds (Phase B)
- `dotnet test vibepdf.Tests/vibepdf.Tests.csproj -p:Platform=x64 -r win-x64` -- expected: all tests pass
- `git grep -n pdfjunior -- ':!_bmad-output'` -- expected: no matches

## Suggested Review Order

**Branding (user-facing name)**

- Start here: the app's display/Store name — the visible result of the rebrand.
  [`Package.appxmanifest:19`](../../vibepdf/Package.appxmanifest#L19)

- Window title shown in the running app (hard-coded, not bound to AppTitle).
  [`MainWindow.xaml:12`](../../vibepdf/MainWindow.xaml#L12)

- The shared title string constant.
  [`UiStrings.cs:6`](../../vibepdf/Strings/UiStrings.cs#L6)

**Project identity & wiring (verifies the build holds together)**

- Root namespace of the assembly; assembly name now follows the renamed csproj → `vibepdf.dll`.
  [`vibepdf.csproj:6`](../../vibepdf/vibepdf.csproj#L6)

- Solution now points at the renamed folders/projects.
  [`vibepdf.slnx:7`](../../vibepdf.slnx#L7)

- Test project's reference to the renamed main project.
  [`vibepdf.Tests.csproj:30`](../../vibepdf.Tests/vibepdf.Tests.csproj#L30)

**Peripherals**

- Native assembly identity in the app manifest.
  [`app.manifest:3`](../../vibepdf/app.manifest#L3)

- Build/test commands updated to the new paths.
  [`CLAUDE.md:5`](../../CLAUDE.md#L5)
