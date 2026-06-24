using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using vibepdf.Models;

namespace vibepdf.Services;

public class PdfSharpMergeService : IPdfMergeService
{
    public Task<MergeOutcome> MergeAsync(
        IReadOnlyList<string> paths, Stream output, IProgress<double>? progress, CancellationToken ct)
        => Task.Run<MergeOutcome>(() =>
        {
            try
            {
                using var merged = new PdfDocument();
                for (var i = 0; i < paths.Count; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    using var input = PdfReader.Open(paths[i], PdfDocumentOpenMode.Import);
                    for (var p = 0; p < input.PageCount; p++)
                        merged.AddPage(input.Pages[p]);
                    progress?.Report(100.0 * (i + 1) / paths.Count); // determinate by file count
                }
                merged.Save(output, closeStream: false); // keep the MemoryStream open for the VM to rewind
                return new MergeOutcome.Success(string.Empty);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { return new MergeOutcome.Failure(ex.Message); }
        }, ct);
}
