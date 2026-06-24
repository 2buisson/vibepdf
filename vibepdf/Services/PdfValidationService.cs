using vibepdf.Models;
using Windows.Data.Pdf;
using Windows.Storage;

namespace vibepdf.Services;

public class PdfValidationService : IPdfValidationService
{
    public async Task<(ValidationStatus Status, int? PageCount)> ValidateAsync(string path, CancellationToken ct)
    {
        try
        {
            var file = await StorageFile.GetFileFromPathAsync(path);
            ct.ThrowIfCancellationRequested();
            var doc = await PdfDocument.LoadFromFileAsync(file);
            return (ValidationStatus.Valid, (int)doc.PageCount);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (IsPasswordError(ex))
        {
            return (ValidationStatus.ErrorPassword, null);
        }
        catch
        {
            return (ValidationStatus.ErrorCorrupt, null);
        }
    }

    private static bool IsPasswordError(Exception ex)
    {
        // Windows.Data.Pdf throws with specific HRESULTs for password-protected PDFs
        // 0x8007052B (ERROR_WRONG_PASSWORD) or message containing "password"
        return ex.HResult == unchecked((int)0x8007052B)
            || ex.Message.Contains("password", StringComparison.OrdinalIgnoreCase);
    }
}
