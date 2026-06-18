using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Data.Pdf;
using Windows.Storage;
using Windows.Storage.Streams;

namespace pdfjunior.Services;

public class PdfPreviewService : IPdfPreviewService
{
    public async Task<IReadOnlyList<BitmapImage>> RenderPagesAsync(string path, double width, CancellationToken ct)
    {
        var file = await StorageFile.GetFileFromPathAsync(path);
        ct.ThrowIfCancellationRequested();
        var doc = await PdfDocument.LoadFromFileAsync(file);

        var options = new PdfPageRenderOptions { DestinationWidth = (uint)Math.Max(1, width) };
        var pages = new List<BitmapImage>();

        for (uint i = 0; i < doc.PageCount; i++)
        {
            ct.ThrowIfCancellationRequested();

            using var page = doc.GetPage(i);
            using var stream = new InMemoryRandomAccessStream();
            await page.RenderToStreamAsync(stream, options);

            stream.Seek(0);
            var bmp = new BitmapImage();
            await bmp.SetSourceAsync(stream);
            pages.Add(bmp);
        }

        return pages;
    }
}
