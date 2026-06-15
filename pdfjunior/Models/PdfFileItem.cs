using CommunityToolkit.Mvvm.ComponentModel;

namespace pdfjunior.Models;

public partial class PdfFileItem : ObservableObject
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public string Path { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    [ObservableProperty]
    public partial ValidationStatus Status { get; set; } = ValidationStatus.Checking;

    [ObservableProperty]
    public partial int? PageCount { get; set; }

    public PdfFileItem(string path)
    {
        Path = path;
        DisplayName = System.IO.Path.GetFileName(path);
    }
}
