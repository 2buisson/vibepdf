using Windows.Storage;

namespace vibepdf.Services;

public interface IOutputWriter
{
    Task WriteAsync(Stream source, StorageFile destination);
}
