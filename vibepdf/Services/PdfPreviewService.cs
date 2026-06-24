using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Data.Pdf;
using Windows.Storage;
using Windows.Storage.Streams;

namespace vibepdf.Services;

public class PdfPreviewService : IPdfPreviewService
{
    // Render at ~2x the 300px card so the image stays crisp on high-DPI displays.
    private const uint RenderWidth = 600;

    public async Task<BitmapImage?> RenderFirstPageAsync(string path, CancellationToken ct)
    {
        // Mirror PdfValidationService's Windows.Data.Pdf access pattern.
        var file = await StorageFile.GetFileFromPathAsync(path);
        ct.ThrowIfCancellationRequested();

        var doc = await PdfDocument.LoadFromFileAsync(file);
        if (doc.PageCount == 0)
            throw new InvalidOperationException("PDF has no pages.");

        ct.ThrowIfCancellationRequested();

        using var page = doc.GetPage(0);
        using var stream = new InMemoryRandomAccessStream();
        await page.RenderToStreamAsync(stream, new PdfPageRenderOptions { DestinationWidth = RenderWidth });
        stream.Seek(0);

        // BitmapImage is a XAML DependencyObject — it must be created on the UI thread.
        // The VM calls this on the UI thread, so the awaits above resume there; no Task.Run.
        var bitmap = new BitmapImage();
        await bitmap.SetSourceAsync(stream);
        return bitmap;
    }
}
