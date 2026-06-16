using pdfjunior.Models;
using pdfjunior.Services;
using Xunit;

namespace pdfjunior.Tests.Services;

public class PdfValidationServiceTests
{
    private readonly PdfValidationService _service = new();

    private static string FixturePath(string filename) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", filename);

    [Fact]
    public async Task ValidateAsync_ValidPdf_ReturnsValidWithPageCount()
    {
        var (status, pageCount) = await _service.ValidateAsync(FixturePath("valid.pdf"), CancellationToken.None);

        Assert.Equal(ValidationStatus.Valid, status);
        Assert.NotNull(pageCount);
        Assert.True(pageCount > 0);
    }

    [Fact]
    public async Task ValidateAsync_EncryptedPdf_ReturnsErrorPassword()
    {
        var (status, pageCount) = await _service.ValidateAsync(FixturePath("encrypted.pdf"), CancellationToken.None);

        Assert.Equal(ValidationStatus.ErrorPassword, status);
        Assert.Null(pageCount);
    }

    [Fact]
    public async Task ValidateAsync_CorruptFile_ReturnsErrorCorrupt()
    {
        var (status, pageCount) = await _service.ValidateAsync(FixturePath("corrupt.pdf"), CancellationToken.None);

        Assert.Equal(ValidationStatus.ErrorCorrupt, status);
        Assert.Null(pageCount);
    }

    [Fact]
    public async Task ValidateAsync_NonExistentFile_ReturnsErrorCorrupt()
    {
        var (status, pageCount) = await _service.ValidateAsync(FixturePath("nonexistent.pdf"), CancellationToken.None);

        Assert.Equal(ValidationStatus.ErrorCorrupt, status);
        Assert.Null(pageCount);
    }

    [Fact]
    public async Task ValidateAsync_CancellationRequested_ThrowsOperationCanceled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            _service.ValidateAsync(FixturePath("valid.pdf"), cts.Token));
    }
}
