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

    public async Task<StorageFile?> PickSaveFileAsync(string suggestedName)
    {
        var picker = new FileSavePicker();
        InitializeWithWindow.Initialize(picker, Hwnd);
        picker.SuggestedFileName = suggestedName;                 // "merged.pdf"
        picker.DefaultFileExtension = ".pdf";
        picker.FileTypeChoices.Add("PDF document", new List<string> { ".pdf" });
        return await picker.PickSaveFileAsync();                  // null when the user cancels
    }
}
