using Windows.Storage;

namespace pdfjunior.Services;

public interface IOutputWriter
{
    Task WriteAsync(Stream source, StorageFile destination);
}
