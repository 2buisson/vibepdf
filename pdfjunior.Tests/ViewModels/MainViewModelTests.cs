using NSubstitute;
using pdfjunior.Models;
using pdfjunior.Services;
using pdfjunior.Strings;
using pdfjunior.ViewModels;
using Xunit;

namespace pdfjunior.Tests.ViewModels;

public class MainViewModelTests
{
    private readonly IFilePickerService _pickerService = Substitute.For<IFilePickerService>();
    private readonly IPdfValidationService _validationService = Substitute.For<IPdfValidationService>();

    private MainViewModel CreateViewModel() => new(_pickerService, _validationService);

    [Fact]
    public void Files_StartsEmpty()
    {
        var vm = CreateViewModel();
        Assert.Empty(vm.Files);
    }

    [Fact]
    public void HasFiles_DefaultFalse()
    {
        var vm = CreateViewModel();
        Assert.False(vm.HasFiles);
    }

    [Fact]
    public void SelectedFile_DefaultNull()
    {
        var vm = CreateViewModel();
        Assert.Null(vm.SelectedFile);
    }

    [Fact]
    public void CanMerge_DefaultFalse()
    {
        var vm = CreateViewModel();
        Assert.False(vm.CanMerge);
    }

    [Fact]
    public void MergeDisabledReason_NoFiles_ReturnsNoFilesMessage()
    {
        var vm = CreateViewModel();
        Assert.Equal(UiStrings.MergeDisabledNoFiles, vm.MergeDisabledReason);
    }

    [Fact]
    public async Task AddFiles_PickerReturnsFiles_FilesAppendedWithCheckingStatus()
    {
        var tcs = new TaskCompletionSource<(ValidationStatus, int?)>();
        _pickerService.PickFilesAsync().Returns(new List<string> { @"C:\test\a.pdf", @"C:\test\b.pdf" });
        _validationService.ValidateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(tcs.Task);

        var vm = CreateViewModel();
        await vm.AddFilesCommand.ExecuteAsync(null);

        Assert.Equal(2, vm.Files.Count);
        Assert.Equal("a.pdf", vm.Files[0].DisplayName);
        Assert.Equal("b.pdf", vm.Files[1].DisplayName);
        Assert.Equal(ValidationStatus.Checking, vm.Files[0].Status);
        Assert.Equal(ValidationStatus.Checking, vm.Files[1].Status);

        tcs.SetResult((ValidationStatus.Valid, 3));
    }

    [Fact]
    public async Task AddFiles_PickerCancelled_NoFilesAdded()
    {
        _pickerService.PickFilesAsync().Returns(new List<string>());

        var vm = CreateViewModel();
        await vm.AddFilesCommand.ExecuteAsync(null);

        Assert.Empty(vm.Files);
    }

    [Fact]
    public async Task AddFiles_DuplicatePath_SilentlySkipped()
    {
        _pickerService.PickFilesAsync().Returns(new List<string> { @"C:\test\a.pdf" });
        _validationService.ValidateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<(ValidationStatus, int?)>((ValidationStatus.Valid, 1)));

        var vm = CreateViewModel();
        await vm.AddFilesCommand.ExecuteAsync(null);
        await vm.AddFilesCommand.ExecuteAsync(null);

        Assert.Single(vm.Files);
    }

    [Fact]
    public async Task AddFiles_DuplicatePathCaseInsensitive_SilentlySkipped()
    {
        _pickerService.PickFilesAsync()
            .Returns(
                new List<string> { @"C:\Test\A.pdf" },
                new List<string> { @"c:\test\a.pdf" });
        _validationService.ValidateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<(ValidationStatus, int?)>((ValidationStatus.Valid, 1)));

        var vm = CreateViewModel();
        await vm.AddFilesCommand.ExecuteAsync(null);
        await vm.AddFilesCommand.ExecuteAsync(null);

        Assert.Single(vm.Files);
    }

    [Fact]
    public async Task Validation_ValidPdf_StatusAndPageCountUpdated()
    {
        _pickerService.PickFilesAsync().Returns(new List<string> { @"C:\test\doc.pdf" });
        _validationService.ValidateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<(ValidationStatus, int?)>((ValidationStatus.Valid, 5)));

        var vm = CreateViewModel();
        await vm.AddFilesCommand.ExecuteAsync(null);

        await WaitForValidation();

        Assert.Equal(ValidationStatus.Valid, vm.Files[0].Status);
        Assert.Equal(5, vm.Files[0].PageCount);
    }

    [Fact]
    public async Task Validation_PasswordProtected_StatusErrorPassword()
    {
        _pickerService.PickFilesAsync().Returns(new List<string> { @"C:\test\locked.pdf" });
        _validationService.ValidateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<(ValidationStatus, int?)>((ValidationStatus.ErrorPassword, null)));

        var vm = CreateViewModel();
        await vm.AddFilesCommand.ExecuteAsync(null);

        await WaitForValidation();

        Assert.Equal(ValidationStatus.ErrorPassword, vm.Files[0].Status);
        Assert.Null(vm.Files[0].PageCount);
    }

    [Fact]
    public async Task Validation_CorruptFile_StatusErrorCorrupt()
    {
        _pickerService.PickFilesAsync().Returns(new List<string> { @"C:\test\bad.pdf" });
        _validationService.ValidateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<(ValidationStatus, int?)>((ValidationStatus.ErrorCorrupt, null)));

        var vm = CreateViewModel();
        await vm.AddFilesCommand.ExecuteAsync(null);

        await WaitForValidation();

        Assert.Equal(ValidationStatus.ErrorCorrupt, vm.Files[0].Status);
        Assert.Null(vm.Files[0].PageCount);
    }

    [Fact]
    public async Task Validation_Timeout_CancellationException_StatusErrorTimeout()
    {
        _pickerService.PickFilesAsync().Returns(new List<string> { @"C:\test\slow.pdf" });
        _validationService.ValidateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<(ValidationStatus, int?)>(callInfo =>
            {
                throw new OperationCanceledException();
            });

        var vm = CreateViewModel();
        await vm.AddFilesCommand.ExecuteAsync(null);

        await WaitForValidation();

        Assert.Equal(ValidationStatus.ErrorTimeout, vm.Files[0].Status);
    }

    [Fact]
    public async Task Validation_Timeout_HangingValidation_StatusErrorTimeout()
    {
        var hangForever = new TaskCompletionSource<(ValidationStatus, int?)>();
        _pickerService.PickFilesAsync().Returns(new List<string> { @"C:\test\huge.pdf" });
        _validationService.ValidateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(hangForever.Task);

        var vm = CreateViewModel();
        await vm.AddFilesCommand.ExecuteAsync(null);

        Assert.Equal(ValidationStatus.Checking, vm.Files[0].Status);

        await Task.Delay(TimeSpan.FromSeconds(6), TestContext.Current.CancellationToken);

        Assert.Equal(ValidationStatus.ErrorTimeout, vm.Files[0].Status);

        hangForever.SetCanceled(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CanMerge_AllValid_ReturnsTrue()
    {
        _pickerService.PickFilesAsync().Returns(new List<string> { @"C:\test\a.pdf" });
        _validationService.ValidateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<(ValidationStatus, int?)>((ValidationStatus.Valid, 3)));

        var vm = CreateViewModel();
        await vm.AddFilesCommand.ExecuteAsync(null);

        await WaitForValidation();

        Assert.True(vm.CanMerge);
    }

    [Fact]
    public async Task CanMerge_HasFlaggedFile_ReturnsFalse()
    {
        _pickerService.PickFilesAsync().Returns(new List<string> { @"C:\test\a.pdf", @"C:\test\b.pdf" });
        _validationService.ValidateAsync(@"C:\test\a.pdf", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<(ValidationStatus, int?)>((ValidationStatus.Valid, 3)));
        _validationService.ValidateAsync(@"C:\test\b.pdf", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<(ValidationStatus, int?)>((ValidationStatus.ErrorCorrupt, null)));

        var vm = CreateViewModel();
        await vm.AddFilesCommand.ExecuteAsync(null);

        await WaitForValidation();

        Assert.False(vm.CanMerge);
    }

    [Fact]
    public async Task MergeDisabledReason_FlaggedFiles_ReturnsFlaggedMessage()
    {
        _pickerService.PickFilesAsync().Returns(new List<string> { @"C:\test\a.pdf", @"C:\test\b.pdf" });
        _validationService.ValidateAsync(@"C:\test\a.pdf", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<(ValidationStatus, int?)>((ValidationStatus.Valid, 3)));
        _validationService.ValidateAsync(@"C:\test\b.pdf", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<(ValidationStatus, int?)>((ValidationStatus.ErrorPassword, null)));

        var vm = CreateViewModel();
        await vm.AddFilesCommand.ExecuteAsync(null);

        await WaitForValidation();

        Assert.Equal(UiStrings.MergeDisabledFlaggedFiles, vm.MergeDisabledReason);
    }

    [Fact]
    public async Task MergeDisabledReason_StillChecking_ReturnsCheckingMessage()
    {
        var tcs = new TaskCompletionSource<(ValidationStatus, int?)>();
        _pickerService.PickFilesAsync().Returns(new List<string> { @"C:\test\a.pdf" });
        _validationService.ValidateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(tcs.Task);

        var vm = CreateViewModel();
        await vm.AddFilesCommand.ExecuteAsync(null);

        Assert.Equal(UiStrings.MergeDisabledStillChecking, vm.MergeDisabledReason);

        tcs.SetResult((ValidationStatus.Valid, 1));
    }

    [Fact]
    public async Task MergeDisabledReason_AllValid_ReturnsNull()
    {
        _pickerService.PickFilesAsync().Returns(new List<string> { @"C:\test\a.pdf" });
        _validationService.ValidateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<(ValidationStatus, int?)>((ValidationStatus.Valid, 2)));

        var vm = CreateViewModel();
        await vm.AddFilesCommand.ExecuteAsync(null);

        await WaitForValidation();

        Assert.Null(vm.MergeDisabledReason);
    }

    [Fact]
    public void HasFiles_AfterAddingFile_ReturnsTrue()
    {
        var vm = CreateViewModel();
        vm.Files.Add(new PdfFileItem(@"C:\test\a.pdf"));
        Assert.True(vm.HasFiles);
    }

    // --- Remove (reorder is drag-and-drop in the ListView; no view-model command) ---

    [Fact]
    public void Remove_SelectedFile_RemovedAndSelectionCleared()
    {
        var vm = CreateViewModel();
        var a = new PdfFileItem(@"C:\test\a.pdf");
        var b = new PdfFileItem(@"C:\test\b.pdf");
        vm.Files.Add(a);
        vm.Files.Add(b);
        vm.SelectedFile = a;

        vm.RemoveCommand.Execute(null);

        Assert.DoesNotContain(a, vm.Files);
        Assert.Single(vm.Files);
        Assert.Null(vm.SelectedFile);
    }

    [Fact]
    public void Remove_LastFile_HasFilesBecomesFalse()
    {
        var vm = CreateViewModel();
        var a = new PdfFileItem(@"C:\test\a.pdf");
        vm.Files.Add(a);
        vm.SelectedFile = a;

        vm.RemoveCommand.Execute(null);

        Assert.Empty(vm.Files);
        Assert.False(vm.HasFiles);
    }

    [Theory]
    [InlineData(ValidationStatus.ErrorPassword)]
    [InlineData(ValidationStatus.ErrorCorrupt)]
    [InlineData(ValidationStatus.ErrorTimeout)]
    public void Remove_FlaggedFile_RemovedLikeAnyOther(ValidationStatus status)
    {
        var vm = CreateViewModel();
        var valid = new PdfFileItem(@"C:\test\good.pdf") { Status = ValidationStatus.Valid };
        var flagged = new PdfFileItem(@"C:\test\bad.pdf") { Status = status };
        vm.Files.Add(valid);
        vm.Files.Add(flagged);
        vm.SelectedFile = flagged;

        vm.RemoveCommand.Execute(null);

        Assert.DoesNotContain(flagged, vm.Files);
        Assert.Single(vm.Files);
        Assert.Null(vm.SelectedFile);
    }

    [Fact]
    public void Remove_OnlyFlaggedFile_CanMergeBecomesTrue()
    {
        var vm = CreateViewModel();
        var valid = new PdfFileItem(@"C:\test\good.pdf") { Status = ValidationStatus.Valid };
        var flagged = new PdfFileItem(@"C:\test\bad.pdf") { Status = ValidationStatus.ErrorCorrupt };
        vm.Files.Add(valid);
        vm.Files.Add(flagged);

        Assert.False(vm.CanMerge);

        vm.SelectedFile = flagged;
        vm.RemoveCommand.Execute(null);

        Assert.True(vm.CanMerge);
    }

    [Fact]
    public void Remove_NoSelection_Disabled()
    {
        var vm = CreateViewModel();
        vm.Files.Add(new PdfFileItem(@"C:\test\a.pdf"));

        Assert.False(vm.RemoveCommand.CanExecute(null));
    }

    [Fact]
    public void Reorder_PreservesSelectionAndOrder()
    {
        // Drag-reorder is performed by the ListView mutating the bound Files
        // collection directly; this mirrors that mutation to document that the
        // selected reference and resulting merge order survive a reorder.
        var vm = CreateViewModel();
        var a = new PdfFileItem(@"C:\test\a.pdf");
        var b = new PdfFileItem(@"C:\test\b.pdf");
        var c = new PdfFileItem(@"C:\test\c.pdf");
        vm.Files.Add(a);
        vm.Files.Add(b);
        vm.Files.Add(c);
        vm.SelectedFile = b;

        vm.Files.Move(1, 0); // user drags b above a

        Assert.Equal(new[] { b, a, c }, vm.Files);
        Assert.Same(b, vm.SelectedFile);
    }

    [Fact]
    public async Task Remove_DuringValidation_LateCompletionDoesNotMutateOrFlipMerge()
    {
        var hang = new TaskCompletionSource<(ValidationStatus, int?)>();
        _pickerService.PickFilesAsync().Returns(new List<string> { @"C:\test\hang.pdf" });
        _validationService.ValidateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(hang.Task);

        var vm = CreateViewModel();
        await vm.AddFilesCommand.ExecuteAsync(null);

        var item = vm.Files[0];
        Assert.Equal(ValidationStatus.Checking, item.Status);

        vm.SelectedFile = item;
        vm.RemoveCommand.Execute(null);

        Assert.Empty(vm.Files);

        // Late-completing validation arrives after the item was removed.
        hang.SetResult((ValidationStatus.Valid, 5));
        await WaitForValidation();

        Assert.Empty(vm.Files);
        Assert.False(vm.CanMerge);
    }

    private static async Task WaitForValidation()
    {
        await Task.Delay(200);
    }
}
