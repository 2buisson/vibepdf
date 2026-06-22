namespace pdfjunior.Strings;

public static class UiStrings
{
    // App / window title (also shown in the custom title bar)
    public const string AppTitle = "PDF Junior";

    // MC-1: Empty file list placeholder
    public const string EmptyFileListPlaceholder = "Add PDFs to get started";

    // MC-2: Empty preview placeholder
    public const string EmptyPreviewPlaceholder = "Select a file to preview it";

    // MC-3: List item — checking status
    public const string StatusChecking = "Checking…";

    // MC-4: List item — valid status (format with page count)
    public const string StatusValidSingular = "{0} page";
    public const string StatusValidPlural = "{0} pages";

    // MC-5: List item — error-password
    public const string StatusErrorPassword = "Password protected";

    // MC-6: List item — error-corrupt
    public const string StatusErrorCorrupt = "Could not read file";

    // MC-24: List item — error-timeout
    public const string StatusErrorTimeout = "Could not read file (timeout)";

    // MC-7: Preview — checking placeholder
    public const string PreviewChecking = "Checking…";

    // MC-8: Preview — password exclusion
    public const string PreviewPasswordExclusion = "This file is password protected and will be excluded from the merge.";

    // MC-9: Preview — corrupt exclusion
    public const string PreviewCorruptExclusion = "This file could not be read and will be excluded from the merge.";

    // MC-10: Merge disabled tooltip — no valid files
    public const string MergeDisabledNoFiles = "Add at least one PDF to merge";

    // MC-11: Merge disabled tooltip — flagged files present
    public const string MergeDisabledFlaggedFiles = "Remove files with errors before merging";

    // MC-12: Merge disabled tooltip — still checking
    public const string MergeDisabledStillChecking = "Waiting for files to finish checking";

    // Default filename pre-filled in the Save dialog (FR-7)
    public const string DefaultMergeFileName = "merged.pdf";

    // MC-13: Success banner (format with filename)
    public const string MergeSuccess = "Merged successfully — {0}";

    // MC-14: Success banner action
    public const string MergeSuccessOpenFolder = "Open folder";

    // MC-15: Error — disk full (format with drive)
    public const string MergeErrorDiskFull = "Merge failed — Not enough space on {0}.";

    // MC-16: Error — access denied
    public const string MergeErrorAccessDenied = "Merge failed — Access denied";

    // MC-17: Error — source file missing (format with filename)
    public const string MergeErrorFileMissing = "Merge failed — File not found: {0}";

    // MC-18: Error — generic fallback
    public const string MergeErrorGeneric = "Merge failed. Try again or check the files.";

    // MC-19: Open folder — folder gone
    public const string FolderNotFound = "Folder not found";

    // MC-20: Close guard — dialog title
    public const string CloseGuardTitle = "Merge in progress";

    // MC-21: Close guard — dialog body
    public const string CloseGuardBody = "A merge is still running. Closing now may leave an incomplete file at the destination.";

    // MC-22: Close guard — primary button (default)
    public const string CloseGuardKeepMerging = "Keep merging";

    // MC-23: Close guard — secondary button
    public const string CloseGuardCloseAnyway = "Close anyway";
}
