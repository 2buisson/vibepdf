using vibepdf.Models;

namespace vibepdf.Services;

public interface IPdfValidationService
{
    Task<(ValidationStatus Status, int? PageCount)> ValidateAsync(string path, CancellationToken ct);
}
