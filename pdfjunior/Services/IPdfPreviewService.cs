using Microsoft.UI.Xaml.Media.Imaging;

namespace pdfjunior.Services;

public interface IPdfPreviewService
{
    // Renders the first page of the PDF at path to a BitmapImage (UI-thread bound).
    // Returns null only from a test substitute; the real implementation either
    // returns a non-null bitmap or throws (a throw maps to the corrupt notice).
    Task<BitmapImage?> RenderFirstPageAsync(string path, CancellationToken ct);
}
