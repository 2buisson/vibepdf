---
title: 'Add .gitignore for the pdfjunior project subfolder'
type: 'chore'
created: '2026-06-14'
status: 'done'
route: 'one-shot'
---

# Add .gitignore for the pdfjunior project subfolder

## Intent

**Problem:** The `pdfjunior/` project subfolder has no `.gitignore`, so generated build output (`bin/`, `obj/`), MSIX packaging artifacts, and user-specific Visual Studio files (`*.csproj.user`) would be committed. The repository-root `.gitignore` is a React Native template and does not cover this .NET 8 / WinUI 3 project.

**Approach:** Add a `.gitignore` scoped to `pdfjunior/` with the standard Visual Studio / .NET / WinUI 3 (MSIX) ignore set — build output, MSBuild intermediates, packaging output, signing artifacts, test results, and OS/editor cruft — while preserving all source, assets, manifests, and publish profiles.

## Suggested Review Order

- Build output exclusion — the core rule that keeps `bin/`/`obj/` out of source control.
  [`.gitignore:6`](../../pdfjunior/.gitignore#L6)

- MSIX packaging & signing artifacts — `.appx`/`.msix`/`.pfx`/`.cer` excluded so packaging output and certs never leak.
  [`.gitignore:28`](../../pdfjunior/.gitignore#L28)

- User-specific VS files — `*.csproj.user`/`*.user` excluded per-developer.
  [`.gitignore:14`](../../pdfjunior/.gitignore#L14)
