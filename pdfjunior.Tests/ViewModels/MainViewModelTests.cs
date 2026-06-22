using Microsoft.UI.Xaml.Media.Imaging;
using NSubstitute;
using pdfjunior.Models;
using pdfjunior.Services;
using pdfjunior.Strings;
using pdfjunior.ViewModels;
using Windows.Storage;
using Xunit;

namespace pdfjunior.Tests.ViewModels;

public class MainViewModelTests
{
    private readonly IFilePickerService _pickerService = Substitute.For<IFilePickerService>();
    private readonly IPdfValidationService _validationService = Substitute.For<IPdfValidationService>();
    private readonly IPdfMergeService _mergeService = Substitute.For<IPdfMergeService>();
    private readonly IOutputWriter _outputWriter = Substitute.For<IOutputWriter>();
    private readonly IFolderLauncher _folderLauncher = Substitute.For<IFolderLauncher>();
    private readonly IPdfPreviewService _previewService = Substitute.For<IPdfPreviewService>();

    private MainViewModel CreateViewModel() =>
        new(_pickerService, _validationService, _mergeService, _outputWriter, _folderLauncher, _previewService);

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
    public void MergeDisabledReason_AllFlagged_ReturnsNoFilesMessage()
    {
        // AC #3: zero valid files because every item is flagged → MC-10 ("add a PDF"),
        // NOT MC-11 — removing the flagged files would leave nothing to merge.
        var vm = CreateViewModel();
        vm.Files.Add(new PdfFileItem(@"C:\test\locked.pdf") { Status = ValidationStatus.ErrorPassword });
        vm.Files.Add(new PdfFileItem(@"C:\test\bad.pdf") { Status = ValidationStatus.ErrorCorrupt });

        Assert.Equal(UiStrings.MergeDisabledNoFiles, vm.MergeDisabledReason);
    }

    [Fact]
    public void MergeDisabledReason_FlaggedAndChecking_ReturnsCheckingMessage()
    {
        // AC #4: checking outranks flagged — a list with both shows MC-12 until all resolve.
        var vm = CreateViewModel();
        vm.Files.Add(new PdfFileItem(@"C:\test\bad.pdf") { Status = ValidationStatus.ErrorCorrupt });
        vm.Files.Add(new PdfFileItem(@"C:\test\checking.pdf") { Status = ValidationStatus.Checking });

        Assert.Equal(UiStrings.MergeDisabledStillChecking, vm.MergeDisabledReason);
    }

    // --- Save dialog (story 2.1) ---

    [Fact]
    public async Task Merge_MergeableList_OpensSaveDialogWithDefaultName()
    {
        // AC #6: clicking Merge opens the Save dialog pre-filled with "merged.pdf".
        var vm = CreateViewModel();
        vm.Files.Add(new PdfFileItem(@"C:\test\a.pdf") { Status = ValidationStatus.Valid });
        Assert.True(vm.CanMerge);

        await vm.MergeCommand.ExecuteAsync(null);

        await _pickerService.Received(1).PickSaveFileAsync(UiStrings.DefaultMergeFileName);
    }

    [Fact]
    public async Task Merge_SaveDialogCancelled_SilentNoOp()
    {
        // AC #8: cancelling the Save dialog (picker returns null) writes nothing,
        // throws nothing, and leaves the app state unchanged.
        var vm = CreateViewModel();
        vm.Files.Add(new PdfFileItem(@"C:\test\a.pdf") { Status = ValidationStatus.Valid });
        _pickerService.PickSaveFileAsync(Arg.Any<string>()).Returns((StorageFile?)null);

        await vm.MergeCommand.ExecuteAsync(null);

        Assert.Single(vm.Files);
        Assert.True(vm.CanMerge);
    }

    // --- Merge UI-lock & banners (story 2.2) ---

    [Fact]
    public void IsMerging_True_CanMergeFalse()
    {
        // AC #4: while a merge is running, Merge itself is disabled.
        var vm = CreateViewModel();
        vm.Files.Add(new PdfFileItem(@"C:\test\a.pdf") { Status = ValidationStatus.Valid });
        Assert.True(vm.CanMerge);

        vm.IsMerging = true;

        Assert.False(vm.CanMerge);
    }

    [Fact]
    public void IsMerging_True_DisablesAddRemoveReorder()
    {
        // AC #4: Add / Remove disabled and drag-reorder off while merging.
        var vm = CreateViewModel();
        var a = new PdfFileItem(@"C:\test\a.pdf") { Status = ValidationStatus.Valid };
        vm.Files.Add(a);
        vm.SelectedFile = a;

        Assert.True(vm.AddFilesCommand.CanExecute(null));
        Assert.True(vm.RemoveCommand.CanExecute(null));
        Assert.True(vm.CanReorderFiles);

        vm.IsMerging = true;

        Assert.False(vm.AddFilesCommand.CanExecute(null));
        Assert.False(vm.RemoveCommand.CanExecute(null));
        Assert.False(vm.CanReorderFiles);
    }

    [Fact]
    public async Task MergePressed_ClearsSuccessBanner()
    {
        // AC #11: pressing Merge clears any visible banner before the Save dialog opens.
        // Picker cancels (returns null) so no StorageFile is needed and no lock engages.
        var vm = CreateViewModel();
        vm.Files.Add(new PdfFileItem(@"C:\test\a.pdf") { Status = ValidationStatus.Valid });
        vm.IsSuccessBannerOpen = true;
        _pickerService.PickSaveFileAsync(Arg.Any<string>()).Returns((StorageFile?)null);

        await vm.MergeCommand.ExecuteAsync(null);

        Assert.False(vm.IsSuccessBannerOpen);
        Assert.Single(vm.Files);     // list preserved
        Assert.False(vm.IsMerging);  // no lock engaged on the cancel path
    }

    [Fact]
    public async Task MergePressed_ClearsErrorBanner()
    {
        // AC #11: same as above for the error banner.
        var vm = CreateViewModel();
        vm.Files.Add(new PdfFileItem(@"C:\test\a.pdf") { Status = ValidationStatus.Valid });
        vm.IsErrorBannerOpen = true;
        _pickerService.PickSaveFileAsync(Arg.Any<string>()).Returns((StorageFile?)null);

        await vm.MergeCommand.ExecuteAsync(null);

        Assert.False(vm.IsErrorBannerOpen);
        Assert.Single(vm.Files);
        Assert.False(vm.IsMerging);
    }

    [Fact]
    public async Task OpenFolder_NoPriorMerge_SafeNoOp()
    {
        // The happy path (folder gone → MC-19) needs a real StorageFile destination and is
        // F5-verified. The null-guard path is unit-testable: with no captured output folder,
        // Open folder is a no-op that never touches the launcher and never throws.
        var vm = CreateViewModel();

        await vm.OpenFolderCommand.ExecuteAsync(null);

        await _folderLauncher.DidNotReceive().LaunchFolderAsync(Arg.Any<string>());
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

    // --- Preview (story 1.4) ---

    [Fact]
    public void Preview_SelectNull_ShowsEmptyPlaceholder()
    {
        var vm = CreateViewModel();

        vm.SelectedFile = null;

        Assert.Equal(PreviewState.None, vm.Preview);
        Assert.Null(vm.PreviewImage);
        Assert.True(vm.ShowPreviewPlaceholder);
        Assert.Equal(UiStrings.EmptyPreviewPlaceholder, vm.PreviewPlaceholderText);
    }

    [Fact]
    public async Task Preview_SelectChecking_ShowsCheckingPlaceholder_NoRender()
    {
        var vm = CreateViewModel();
        var item = new PdfFileItem(@"C:\test\a.pdf") { Status = ValidationStatus.Checking };
        vm.Files.Add(item);

        vm.SelectedFile = item;
        await WaitForValidation();

        Assert.Equal(PreviewState.Checking, vm.Preview);
        Assert.Equal(UiStrings.PreviewChecking, vm.PreviewPlaceholderText);
        Assert.Null(vm.PreviewImage);
        await _previewService.DidNotReceive().RenderFirstPageAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Preview_SelectErrorPassword_ShowsPasswordExclusion()
    {
        var vm = CreateViewModel();
        var item = new PdfFileItem(@"C:\test\a.pdf") { Status = ValidationStatus.ErrorPassword };
        vm.Files.Add(item);

        vm.SelectedFile = item;
        await WaitForValidation();

        Assert.Equal(PreviewState.ExcludedPassword, vm.Preview);
        Assert.Equal(UiStrings.PreviewPasswordExclusion, vm.PreviewPlaceholderText);
    }

    [Theory]
    [InlineData(ValidationStatus.ErrorCorrupt)]
    [InlineData(ValidationStatus.ErrorTimeout)]
    public async Task Preview_SelectCorruptOrTimeout_ShowsCorruptExclusion(ValidationStatus status)
    {
        var vm = CreateViewModel();
        var item = new PdfFileItem(@"C:\test\a.pdf") { Status = status };
        vm.Files.Add(item);

        vm.SelectedFile = item;
        await WaitForValidation();

        Assert.Equal(PreviewState.ExcludedCorrupt, vm.Preview);
        Assert.Equal(UiStrings.PreviewCorruptExclusion, vm.PreviewPlaceholderText);
    }

    [Fact]
    public async Task Preview_SelectValid_RendersFirstPage()
    {
        var vm = CreateViewModel();
        var item = new PdfFileItem(@"C:\test\a.pdf") { Status = ValidationStatus.Valid };
        vm.Files.Add(item);

        vm.SelectedFile = item;
        await WaitForValidation();

        Assert.Equal(PreviewState.Ready, vm.Preview);
        Assert.True(vm.ShowPreviewImage);
        await _previewService.Received(1).RenderFirstPageAsync(item.Path, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Preview_CheckingResolvesToValid_RendersAutomatically()
    {
        var vm = CreateViewModel();
        var item = new PdfFileItem(@"C:\test\a.pdf") { Status = ValidationStatus.Checking };
        vm.Files.Add(item);

        vm.SelectedFile = item;
        await WaitForValidation();
        Assert.Equal(PreviewState.Checking, vm.Preview);

        item.Status = ValidationStatus.Valid;
        await WaitForValidation();

        Assert.Equal(PreviewState.Ready, vm.Preview);
        await _previewService.Received(1).RenderFirstPageAsync(item.Path, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Preview_ReorderSelectedFile_DoesNotReload()
    {
        var vm = CreateViewModel();
        var a = new PdfFileItem(@"C:\test\a.pdf") { Status = ValidationStatus.Valid };
        var b = new PdfFileItem(@"C:\test\b.pdf") { Status = ValidationStatus.Valid };
        var c = new PdfFileItem(@"C:\test\c.pdf") { Status = ValidationStatus.Valid };
        vm.Files.Add(a);
        vm.Files.Add(b);
        vm.Files.Add(c);

        vm.SelectedFile = b;
        await WaitForValidation();

        vm.Files.Move(1, 0); // user drags b above a
        await WaitForValidation();

        // AC #10: reorder is a Files.Move (no SelectedFile change) → no re-render.
        await _previewService.Received(1).RenderFirstPageAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        Assert.Equal(PreviewState.Ready, vm.Preview);
        Assert.Same(b, vm.SelectedFile);
    }

    [Fact]
    public async Task Preview_RemoveSelectedFile_ClearsPreview()
    {
        var vm = CreateViewModel();
        var item = new PdfFileItem(@"C:\test\a.pdf") { Status = ValidationStatus.Valid };
        vm.Files.Add(item);

        vm.SelectedFile = item;
        await WaitForValidation();
        Assert.Equal(PreviewState.Ready, vm.Preview);

        vm.RemoveCommand.Execute(null);

        Assert.Equal(PreviewState.None, vm.Preview);
        Assert.Null(vm.PreviewImage);
        Assert.Null(vm.SelectedFile);
    }

    [Fact]
    public async Task Preview_SelectionSupersededMidRender_DropsStaleResult()
    {
        // A's first-page render is in flight when the user selects B; A's late
        // completion must be dropped by the staleness guard, not applied.
        var renderA = new TaskCompletionSource<BitmapImage?>();
        var a = new PdfFileItem(@"C:\test\a.pdf") { Status = ValidationStatus.Valid };
        var b = new PdfFileItem(@"C:\test\b.pdf") { Status = ValidationStatus.Checking };
        _previewService.RenderFirstPageAsync(a.Path, Arg.Any<CancellationToken>()).Returns(renderA.Task);

        var vm = CreateViewModel();
        vm.Files.Add(a);
        vm.Files.Add(b);

        vm.SelectedFile = a; // starts the (pending) render of A
        vm.SelectedFile = b; // supersedes: B is Checking, no render
        Assert.Equal(PreviewState.Checking, vm.Preview);

        renderA.SetResult(null); // A completes after B was selected
        await WaitForValidation();

        Assert.Equal(PreviewState.Checking, vm.Preview); // A's result dropped
        await _previewService.Received(1).RenderFirstPageAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    private static async Task WaitForValidation()
    {
        await Task.Delay(200);
    }
}
