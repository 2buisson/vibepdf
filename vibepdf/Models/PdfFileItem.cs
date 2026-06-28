namespace vibepdf.Models;

public class PdfFileItem
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public string Path { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    // Plain mutable state — the list row is refreshed imperatively when these change
    // (no INotifyPropertyChanged; see MainWindow.RefreshRow).
    public ValidationStatus Status { get; set; } = ValidationStatus.Checking;

    public int? PageCount { get; set; }

    public PdfFileItem(string path)
    {
        Path = path;
        DisplayName = System.IO.Path.GetFileName(path);
    }
}
