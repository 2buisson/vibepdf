using Windows.Storage;
using Windows.System;

namespace pdfjunior.Services;

public class FolderLauncher : IFolderLauncher
{
    public async Task<bool> LaunchFolderAsync(string folderPath)
    {
        if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
            return false; // VM shows MC-19 "Folder not found"
        var folder = await StorageFolder.GetFolderFromPathAsync(folderPath);
        await Launcher.LaunchFolderAsync(folder);
        return true;
    }
}
