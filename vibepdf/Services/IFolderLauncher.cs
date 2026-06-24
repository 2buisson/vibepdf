namespace vibepdf.Services;

public interface IFolderLauncher
{
    Task<bool> LaunchFolderAsync(string folderPath);
}
