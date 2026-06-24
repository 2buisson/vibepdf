using Windows.Storage;

namespace vibepdf.Services;

public interface IFilePickerService
{
    Task<IReadOnlyList<string>> PickFilesAsync();
    Task<StorageFile?> PickSaveFileAsync(string suggestedName);
}
