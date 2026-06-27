using Windows.Storage;
using Windows.System;

namespace vibepdf.Services;

public class FolderLauncher : IFolderLauncher
{
    public async Task<bool> LaunchFolderAsync(string folderPath)
    {
        if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
            return false; // VM shows MC-19 "Folder not found"
        try
        {
            var folder = await StorageFolder.GetFolderFromPathAsync(folderPath);
            await Launcher.LaunchFolderAsync(folder);
            return true;
        }
        catch
        {
            // Folder vanished after the Exists check (TOCTOU), access-denied, or an
            // unreachable UNC path: degrade to the inline "Folder not found" path.
            return false;
        }
    }
}
