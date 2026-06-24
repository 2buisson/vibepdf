using vibepdf.Models;

namespace vibepdf.Services;

public interface IPdfMergeService
{
    Task<MergeOutcome> MergeAsync(IReadOnlyList<string> paths, Stream output, IProgress<double>? progress, CancellationToken ct);
}
