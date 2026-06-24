namespace vibepdf.Models;

public abstract record MergeOutcome
{
    public sealed record Success(string Path) : MergeOutcome;
    public sealed record Failure(string Reason) : MergeOutcome;
}
