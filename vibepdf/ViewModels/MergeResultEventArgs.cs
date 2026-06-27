namespace vibepdf.ViewModels;

public enum MergeResultSeverity
{
    Success,
    Error,
}

// Carries a finished-merge result (success or error) from the view model to the
// View, which presents it in a modal ContentDialog.
public sealed class MergeResultEventArgs(MergeResultSeverity severity, string message) : EventArgs
{
    public MergeResultSeverity Severity { get; } = severity;

    public string Message { get; } = message;
}
