using Windows.Storage;

namespace pdfjunior.Services;

public interface IFilePickerService
{
    Task<IReadOnlyList<string>> PickFilesAsync();
    Task<StorageFile?> PickSaveFileAsync(string suggestedName);
}
