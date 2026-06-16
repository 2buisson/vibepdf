using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace pdfjunior.Services;

public class FilePickerService : IFilePickerService
{
    public nint Hwnd { get; set; }

    public async Task<IReadOnlyList<string>> PickFilesAsync()
    {
        var picker = new FileOpenPicker();
        InitializeWithWindow.Initialize(picker, Hwnd);
        picker.ViewMode = PickerViewMode.List;
        picker.FileTypeFilter.Add(".pdf");

        var files = await picker.PickMultipleFilesAsync();
        if (files is null || files.Count == 0)
            return [];

        return files.Select(f => f.Path).ToList();
    }

    public Task<StorageFile?> PickSaveFileAsync(string suggestedName)
    {
        return Task.FromResult<StorageFile?>(null);
    }
}
