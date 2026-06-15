using pdfjunior.Models;

namespace pdfjunior.Services;

public interface IPdfMergeService
{
    Task<MergeOutcome> MergeAsync(IReadOnlyList<string> paths, Stream output, IProgress<double>? progress, CancellationToken ct);
}
