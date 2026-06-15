using pdfjunior.Models;

namespace pdfjunior.Services;

public interface IPdfValidationService
{
    Task<(ValidationStatus Status, int? PageCount)> ValidateAsync(string path, CancellationToken ct);
}
