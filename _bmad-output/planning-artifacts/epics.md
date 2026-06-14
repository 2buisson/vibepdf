---
stepsCompleted: [1, 2, 3, 4]
inputDocuments:
  - _bmad-output/planning-artifacts/prds/prd-pdf-junior-2026-06-14/prd.md
  - _bmad-output/planning-artifacts/architecture.md
---

# pdf-junior - Epic Breakdown

## Overview

This document provides the complete epic and story breakdown for pdf-junior, decomposing the requirements from the PRD and Architecture into implementable stories.

## Requirements Inventory

### Functional Requirements

FR-1: The user can add one or more PDFs via the File picker; multiple selection in a single invocation is supported. Selected files are appended to the end of the File list. Duplicate files (same absolute path, case-insensitive) are silently skipped. Cancelling the picker is a silent no-op.

FR-2: Each added List item is validated asynchronously to determine its Validation status (checking → valid | error-password | error-corrupt). A valid item displays its page count. A per-file wall-clock guard (default 5 s) bounds an unresponsive parse. Items remain selectable, removable, and reorderable at any status.

FR-3: The user can remove any List item by selecting it and using Remove in the Preview toolbar. After removal, selection clears and the Preview pane returns to its empty state. Remove is enabled only when a file is selected.

FR-4: The user can change the position of the Selected file using Move up and Move down buttons in the Preview toolbar. Selection follows the moved item. Move up disabled at first position; Move down disabled at last; both disabled when nothing is selected.

FR-5: The user can view a read-only preview of the Selected file in the Preview pane. Valid files render fit-to-width with vertical scroll. Checking files show a placeholder. Flagged files show an inline exclusion notice. Empty selection shows "Select a file to preview it"; empty list shows "Add PDFs to get started."

FR-6: The user initiates the merge via the Merge button in the Action bar. Merge is enabled only when ≥1 valid file, 0 flagged files, 0 checking files. During merge execution the File list is read-only and controls are disabled (Preview pane stays scrollable).

FR-7: On Merge, the native Windows Save dialog opens with filename pre-filled as "merged.pdf". The user selects the destination folder and filename. The OS enforces filename validity and overwrite confirmation. Cancelling the dialog aborts silently.

FR-8: All Valid files in the File list are combined in display order into a single PDF written to the chosen location. Merge runs off the UI thread. A progress indicator appears if the merge exceeds 2 seconds; shorter merges show no progress. Progress is determinate by file count.

FR-9: On success, an inline success banner names the output file and auto-dismisses after ~8 seconds (manually dismissible). Open folder opens File Explorer to the output folder. The File list is preserved after merge.

FR-10: Merge is enabled only when the File list contains ≥1 Valid file, 0 Flagged files, and 0 files still checking. A disabled Merge explains why via a hover tooltip distinguishing the cause.

FR-11: On failure (disk full, write denied, source vanished), the user receives a descriptive error banner (manually dismissed, no auto-dismiss). A partial output file may remain at the destination. The File list is preserved for retry.

FR-12: If the user attempts to close the window while a merge is running, a confirmation dialog offers Keep merging (default) and Close anyway. Close anyway cancels the in-progress merge cooperatively; a partial output file may remain.

### NonFunctional Requirements

NFR-1: Performance / non-blocking — No file-count or file-size limit. Merge runs off the UI thread; during a merge the UI thread is never blocked. A progress indicator appears once a merge has been running 2 seconds.

NFR-2: Privacy / local-only — No backend; all processing is local. No network requests in normal operation; no telemetry, analytics, or crash data leaves the device.

NFR-3: Microsoft Store compliance — Packaged as MSIX; capability declarations minimal (file access only, no broadFileSystemAccess); must pass Store certification. Minimum OS: Windows 11.

NFR-4: No account — No sign-in, registration, or onboarding flow of any kind.

NFR-5: Startup time — Reaches interactive state within 3 seconds on a mid-range Windows 11 PC.

NFR-6: Install size — Installed footprint stays under 100 MB (hard constraint and core competitive differentiator).

NFR-7: Privacy policy — A publicly accessible privacy policy page is produced and linked in the Store listing before submission.

### Additional Requirements

- Starter template alignment: TargetFramework → net10.0-windows10.0.22621.0, TargetPlatformMinVersion → 10.0.22000.0, add CommunityToolkit.Mvvm 8.4.2.
- PDF merge engine: PDFsharp 6.2.4 (MIT, pure-managed) for whole-file concatenation in File-list order.
- PDF validation + preview engine: Windows.Data.Pdf.PdfDocument (in-box) for password/corrupt classification, page count, and page-to-bitmap preview rendering.
- MVVM via CommunityToolkit.Mvvm 8.4.2 with ObservableObject, [ObservableProperty], [RelayCommand].
- DI via Microsoft.Extensions.DependencyInjection 10.0.x; single composition root in App; constructor injection only; services as singletons.
- App structure: Views/ · ViewModels/ · Services/ (interface + impl) · Models/ · Strings/ · Converters/. Single project, single window.
- Service interfaces: IPdfValidationService, IPdfPreviewService, IPdfMergeService, IFilePickerService, IFolderLauncher, IOutputWriter, IErrorMapper.
- Concurrency: bounded validation concurrency via SemaphoreSlim; per-file wall-clock guard (5 s default); merge on Task.Run with CancellationToken; progress via IProgress<double> marshaled to UI; progress bar appears only after 2 s.
- Output safety: direct write to destination StorageFile (no temp-file staging, no rollback); partial output permitted on failure/cancellation per FR-11.
- Error taxonomy: services return MergeOutcome result type (Success | Failure); single ErrorMapper maps exceptions to canonical microcopy strings.
- UI strings: all user-facing copy in a single Strings/UiStrings.cs static class; no inline string literals.
- Test stack: xunit.v3 3.2.2 in a sibling pdfjunior.Tests project; unit tests for ViewModels + Services behind interfaces; PDF fixtures (valid, encrypted, corrupt).
- Trimming verification: PublishTrimmed=True on Release; verify packaged app runs with WinUI/PDFsharp (add TrimmerRootAssembly if needed).
- Budget measurement: NFR-5 (<3 s startup) and NFR-6 (<100 MB installed) must be measured early.
- All UI/collection mutations on the UI thread via DispatcherQueue.TryEnqueue.
- Compiled x:Bind (not classic Binding) with explicit mode.
- Only WinUI theme resources for colors/brushes — no hardcoded hex, no custom radii.
- No telemetry, analytics, or network sinks; debug-only logging via System.Diagnostics.Debug.

### UX Design Requirements

N/A — UX Design document was excluded from this analysis per user direction.

### FR Coverage Map

FR-1: Epic 1 — Add PDF files via file picker
FR-2: Epic 1 — Validate added files (async, status + page count)
FR-3: Epic 1 — Remove a file
FR-4: Epic 1 — Reorder files (Move up/down)
FR-5: Epic 1 — Preview the selected file
FR-6: Epic 2 — Trigger the merge
FR-7: Epic 2 — Choose output destination (Save dialog)
FR-8: Epic 2 — Execute the merge (off-thread, progress)
FR-9: Epic 2 — Report success (banner + Open folder)
FR-10: Epic 2 — Block merge on invalid state (gating + tooltip)
FR-11: Epic 2 — Handle merge failure (descriptive error)
FR-12: Epic 2 — Guard window close during merge

## Epic List

### Epic 1: File Management & Preview
Users can add PDF files, see them validated (with page counts or error flags), remove unwanted files, reorder the list, and preview any file — all in a clean single-window WinUI 3 interface. Includes project foundation (starter alignment, DI wiring, models, test project scaffolding).
**FRs covered:** FR-1, FR-2, FR-3, FR-4, FR-5

### Epic 2: Merge, Output & Safety
Users can merge their organized files into a single PDF, choose the destination and filename via the native Save dialog, see progress and a success banner, and are protected by merge-gating, descriptive error messages, and a window-close guard.
**FRs covered:** FR-6, FR-7, FR-8, FR-9, FR-10, FR-11, FR-12

---

## Epic 1: File Management & Preview

Users can add PDF files, see them validated (with page counts or error flags), remove unwanted files, reorder the list, and preview any file — all in a clean single-window WinUI 3 interface.

### Story 1.1: Project Setup & App Shell Layout

As a user,
I want the app to launch quickly into a clean two-pane layout with clear empty-state guidance,
So that I immediately understand how to start merging PDFs.

**Acceptance Criteria:**

**Given** the scaffolded project exists
**When** the developer aligns the project (TargetFramework → net10.0-windows10.0.22621.0, TargetPlatformMinVersion → 10.0.22000.0, adds CommunityToolkit.Mvvm 8.4.2 and Microsoft.Extensions.DependencyInjection)
**Then** the project builds and targets .NET 10 / Windows 11

**Given** the project structure is created
**When** the developer inspects the solution
**Then** Models/ (PdfFileItem, ValidationStatus, MergeOutcome), ViewModels/ (MainViewModel), Services/ (interfaces), Strings/ (UiStrings), and Converters/ folders exist, DI composition root is wired in App.xaml.cs, and MainViewModel is resolved from the container

**Given** the app is launched
**When** the main window appears
**Then** the two-pane layout is visible: a left sidebar (File list area), a right Preview pane, a Preview toolbar (Move up, Move down, Remove — all disabled), and a bottom Action bar with Add PDF(s) and Merge (Merge disabled)

**Given** the app is launched with no files added
**When** the user views the sidebar
**Then** it shows "Add PDFs to get started"
**And** the Preview pane shows "Select a file to preview it"

**Given** the pdfjunior.Tests project is created
**When** `dotnet test` is run
**Then** the xUnit.v3 test project compiles and a placeholder test passes

### Story 1.2: Add & Validate PDF Files

As a user,
I want to add PDF files and see each one validated automatically with a status and page count,
So that I know which files are ready to merge and which have problems.

**Acceptance Criteria:**

**Given** the app is running with an empty File list
**When** the user clicks Add PDF(s)
**Then** the native Windows file picker opens, filtered to .pdf files, with multi-select enabled

**Given** the file picker is open
**When** the user selects multiple PDF files and confirms
**Then** all selected files are appended to the bottom of the File list in picker order, each initially showing checking status

**Given** a file has been added and is being validated
**When** validation completes for a valid PDF
**Then** the list item shows valid status with its page count (e.g. "3 pages")

**Given** a password-protected PDF has been added
**When** validation completes
**Then** the list item shows "Password protected" status

**Given** a corrupt or unreadable file (e.g. an image renamed to .pdf) has been added
**When** validation completes
**Then** the list item shows "Could not read file" status

**Given** a file's validation does not complete
**When** 5 seconds have elapsed (per-file wall-clock guard)
**Then** validation resolves the item to "Could not read file" (error-corrupt)

**Given** a file is already in the File list
**When** the user adds the same file again (same absolute path, case-insensitive)
**Then** the duplicate is silently skipped — not added twice

**Given** the file picker is open
**When** the user cancels the picker
**Then** no files are added and no error is shown (silent no-op)

**Given** files are being validated (still in checking status)
**When** the user interacts with the list
**Then** checking items remain selectable, removable, and reorderable

### Story 1.3: Remove & Reorder Files

As a user,
I want to remove unwanted files and reorder the remaining ones,
So that I control exactly which files are merged and in what order.

**Acceptance Criteria:**

**Given** a file is selected in the File list
**When** the user clicks Remove in the Preview toolbar
**Then** the file is removed from the list, selection clears, and the Preview pane returns to "Select a file to preview it"

**Given** no file is selected
**When** the user views the Preview toolbar
**Then** Remove is disabled

**Given** a flagged file (error-password or error-corrupt) is selected
**When** the user clicks Remove
**Then** it is removed exactly like any other file

**Given** a file is selected that is not the first in the list
**When** the user clicks Move up
**Then** the file moves up one position and remains selected
**And** the Preview pane continues to show the same file without reloading

**Given** a file is selected that is not the last in the list
**When** the user clicks Move down
**Then** the file moves down one position and remains selected
**And** the Preview pane continues to show the same file without reloading

**Given** the first file in the list is selected
**When** the user views the Preview toolbar
**Then** Move up is disabled and Move down is enabled

**Given** the last file in the list is selected
**When** the user views the Preview toolbar
**Then** Move down is disabled and Move up is enabled

**Given** no file is selected
**When** the user views the Preview toolbar
**Then** Move up, Move down, and Remove are all disabled

### Story 1.4: Preview Selected File

As a user,
I want to see a read-only preview of the selected PDF,
So that I can verify the correct file is in the right position before merging.

**Acceptance Criteria:**

**Given** a valid PDF file is in the File list
**When** the user clicks it to select it
**Then** the Preview pane shows a read-only render of the file, fit-to-width, with vertical scroll only
**And** no editing, annotation, or page controls are present

**Given** a valid multi-page PDF is selected
**When** the user scrolls the Preview pane
**Then** all pages are viewable via continuous vertical scroll

**Given** a file in checking status is selected
**When** the Preview pane is viewed
**Then** it shows a "Checking…" placeholder instead of a preview

**Given** a flagged file with error-password status is selected
**When** the Preview pane is viewed
**Then** it shows an inline exclusion notice: "Password protected"

**Given** a flagged file with error-corrupt status is selected
**When** the Preview pane is viewed
**Then** it shows an inline exclusion notice: "Could not read file"

**Given** no file is selected (but the list is not empty)
**When** the Preview pane is viewed
**Then** it shows "Select a file to preview it"

**Given** the File list is empty
**When** the sidebar is viewed
**Then** it shows "Add PDFs to get started"

---

## Epic 2: Merge, Output & Safety

Users can merge their organized files into a single PDF, choose the destination and filename via the native Save dialog, see progress and a success banner, and are protected by merge-gating, descriptive error messages, and a window-close guard.

### Story 2.1: Merge Gating & Save Dialog

As a user,
I want the Merge button to be enabled only when my file list is ready, and to choose where to save the output,
So that I never accidentally merge with invalid files and I control the output location and filename.

**Acceptance Criteria:**

**Given** the File list contains at least one valid file, zero flagged files, and zero files still checking
**When** the user views the Action bar
**Then** the Merge button is enabled

**Given** the File list contains one or more flagged files
**When** the user views the Action bar
**Then** Merge is disabled
**And** a hover tooltip explains: flagged files must be removed before merging

**Given** the File list contains zero valid files (empty or all-flagged)
**When** the user views the Action bar
**Then** Merge is disabled
**And** a hover tooltip explains: no valid files to merge

**Given** any file in the list is still in checking status
**When** the user views the Action bar
**Then** Merge is disabled
**And** a hover tooltip explains: files are still being checked

**Given** Merge is enabled and the user clicks it
**When** the native Save dialog opens
**Then** the filename is pre-filled as "merged.pdf", the file type is set to .pdf, and the user can choose the destination folder and edit the filename

**Given** the Save dialog is open
**When** the user confirms a destination
**Then** the merge execution begins (Story 2.2)

**Given** the Save dialog is open
**When** the user cancels the dialog
**Then** no merge occurs, no error is shown, and the app returns to its idle pre-merge state with the UI unlocked

### Story 2.2: Execute Merge & Report Success

As a user,
I want my files merged into a single PDF with progress feedback and a clear success message,
So that I know the merge is working and can quickly find my output file.

**Acceptance Criteria:**

**Given** the merge service is being implemented
**When** the developer adds the PDFsharp 6.2.x NuGet package and creates PdfSharpMergeService (implementing IPdfMergeService)
**Then** the project builds and the installed size remains under 100 MB (NFR-6)

**Given** the user has confirmed a destination in the Save dialog
**When** the merge begins
**Then** all valid files in the File list are combined in display order into a single PDF written to the chosen location
**And** the merge runs off the UI thread — the app remains responsive throughout

**Given** a merge is in progress
**When** the File list and toolbar are viewed
**Then** the File list is read-only, Add PDF(s), Merge, Move up, Move down, and Remove are all disabled
**And** the Preview pane remains scrollable

**Given** a merge has been running for less than 2 seconds
**When** the user views the app
**Then** no progress indicator is shown

**Given** a merge has been running for 2 seconds or more
**When** the user views the app
**Then** a determinate progress bar (by file count) is visible above the Action bar

**Given** the merge completes successfully
**When** the success banner appears
**Then** it names the output file, auto-dismisses after approximately 8 seconds, and can be manually dismissed before then

**Given** the success banner is visible
**When** the user clicks Open folder
**Then** File Explorer opens to the folder containing the output file
**And** if the folder no longer exists, an inline "Folder not found" message is shown instead

**Given** a merge has completed successfully
**When** the user views the File list
**Then** it is preserved — no files are removed, so a second merge can be produced without re-adding files

**Given** a single valid file is in the File list
**When** the user merges
**Then** a valid single-file merge is produced (a copy at the chosen destination)

**Given** a previous success or error banner is visible
**When** a new merge is started
**Then** the previous banner is cleared

### Story 2.3: Error Handling & Close Guard

As a user,
I want clear error messages when a merge fails and a safety prompt if I try to close the app mid-merge,
So that I understand what went wrong, can retry without losing my file list, and never accidentally cancel a merge.

**Acceptance Criteria:**

**Given** the merge fails due to insufficient disk space
**When** the error banner appears
**Then** it reads "Not enough space on {drive}." (with the actual drive letter)

**Given** the merge fails due to write permission denied
**When** the error banner appears
**Then** it reads "Access denied"

**Given** the merge fails because a source file disappeared
**When** the error banner appears
**Then** it reads "File not found: {filename}" (with the actual filename)

**Given** the merge fails for an unexpected reason
**When** the error banner appears
**Then** it reads "Merge failed. Try again or check the files."

**Given** an error banner is shown
**When** the user views it
**Then** it must be dismissed manually (no auto-dismiss)
**And** the UI is unlocked: File list, Preview toolbar, Add PDF(s), and Merge are re-enabled

**Given** a merge has failed
**When** the user views the File list
**Then** it is preserved for retry — no files are removed

**Given** a merge is in progress
**When** the user attempts to close the window
**Then** a confirmation dialog appears with "Keep merging" (default) and "Close anyway"

**Given** the close-guard dialog is shown
**When** the user selects "Keep merging"
**Then** the dialog closes and the merge continues

**Given** the close-guard dialog is shown
**When** the user selects "Close anyway"
**Then** the in-progress merge is cancelled cooperatively and the window closes
**And** a partial or incomplete output file may remain at the destination
