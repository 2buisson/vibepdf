using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using vibepdf.Models;
using vibepdf.Services;
using Xunit;

namespace vibepdf.Tests.Services;

public class PdfSharpMergeServiceTests : IDisposable
{
    private readonly PdfSharpMergeService _service = new();
    private readonly List<string> _tempFiles = [];

    // The Fixtures/valid.pdf used by the validation tests is a minimal hand-crafted PDF
    // that the WinRT renderer (Windows.Data.Pdf) accepts but PDFsharp's stricter parser
    // rejects ("Unexpected token 'endobj'"). Since the merge engine IS PDFsharp, we feed
    // it PDFsharp-produced inputs with known page counts — self-contained and deterministic.
    private string CreateTempPdf(int pageCount)
    {
        var path = Path.Combine(Path.GetTempPath(), $"vibepdf_merge_test_{Guid.NewGuid():N}.pdf");
        using (var doc = new PdfDocument())
        {
            for (var i = 0; i < pageCount; i++)
                doc.AddPage();
            doc.Save(path);
        }
        _tempFiles.Add(path);
        return path;
    }

    private static int PageCountOf(Stream stream)
    {
        stream.Position = 0;
        using var doc = PdfReader.Open(stream, PdfDocumentOpenMode.Import);
        return doc.PageCount;
    }

    // Collects IProgress callbacks synchronously (the service reports on the Task.Run
    // thread inside the delegate, so by the time the awaited task completes every
    // Report has run) — deterministic, unlike the async-dispatching Progress<T>.
    private sealed class ListProgress : IProgress<double>
    {
        public List<double> Values { get; } = [];
        public void Report(double value) => Values.Add(value);
    }

    [Fact]
    public async Task MergeAsync_SingleValidFile_ProducesCopyWithSamePageCount()
    {
        // AC #3: a single Valid file merges to a valid single-file PDF.
        var source = CreateTempPdf(3);
        using var output = new MemoryStream();

        var outcome = await _service.MergeAsync([source], output, null, CancellationToken.None);

        Assert.IsType<MergeOutcome.Success>(outcome);
        Assert.Equal(3, PageCountOf(output));
    }

    [Fact]
    public async Task MergeAsync_TwoFiles_ConcatenatesPageCounts()
    {
        // AC #2: files are combined in order — count proves concatenation.
        var first = CreateTempPdf(2);
        var second = CreateTempPdf(3);
        using var output = new MemoryStream();

        var outcome = await _service.MergeAsync([first, second], output, null, CancellationToken.None);

        Assert.IsType<MergeOutcome.Success>(outcome);
        Assert.Equal(5, PageCountOf(output));
    }

    [Fact]
    public async Task MergeAsync_ReportsProgress_OncePerFileEndingAt100()
    {
        // AC #7: determinate progress by file count.
        var first = CreateTempPdf(1);
        var second = CreateTempPdf(1);
        using var output = new MemoryStream();
        var progress = new ListProgress();

        await _service.MergeAsync([first, second], output, progress, CancellationToken.None);

        Assert.Equal(2, progress.Values.Count);
        Assert.Equal(100.0, progress.Values[^1]);
    }

    [Fact]
    public async Task MergeAsync_CancelledToken_Throws()
    {
        // FR-12 plumbing: an already-cancelled token short-circuits the merge.
        var source = CreateTempPdf(1);
        using var output = new MemoryStream();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _service.MergeAsync([source], output, null, cts.Token));
    }

    public void Dispose()
    {
        foreach (var path in _tempFiles)
        {
            try { File.Delete(path); } catch { /* best-effort cleanup */ }
        }
    }
}
