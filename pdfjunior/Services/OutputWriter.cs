using Windows.Storage;
using Windows.Storage.Provider;

namespace pdfjunior.Services;

public class OutputWriter : IOutputWriter
{
    public async Task WriteAsync(Stream source, StorageFile destination)
    {
        CachedFileManager.DeferUpdates(destination);
        using (var outStream = await destination.OpenStreamForWriteAsync())
        {
            outStream.SetLength(0); // truncate when overwriting a larger existing file
            await source.CopyToAsync(outStream);
        }
        await CachedFileManager.CompleteUpdatesAsync(destination);
    }
}
