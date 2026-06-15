using Microsoft.UI.Xaml.Media.Imaging;

namespace pdfjunior.Services;

public interface IPdfPreviewService
{
    Task<IReadOnlyList<BitmapImage>> RenderPagesAsync(string path, double width, CancellationToken ct);
}
