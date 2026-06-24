using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media.Imaging;
using vibepdf.Models;
using vibepdf.Services;
using vibepdf.Strings;

namespace vibepdf.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IFilePickerService _filePickerService;
    private readonly IPdfValidationService _validationService;
    private readonly IPdfMergeService _mergeService;
    private readonly IOutputWriter _outputWriter;
    private readonly IFolderLauncher _folderLauncher;
    private readonly IPdfPreviewService _previewService;
    private readonly DispatcherQueue? _dispatcherQueue;
    private readonly SemaphoreSlim _validationSemaphore = new(3);
    private readonly List<PdfFileItem> _subscribedItems = [];
    private readonly Dictionary<PdfFileItem, CancellationTokenSource> _validationCts = [];

    private string? _lastOutputFolder;          // captured on success; used by Open folder
    private CancellationTokenSource? _previewCts; // single in-flight first-page render; cancel on every selection/status change
    private CancellationTokenSource? _mergeCts;  // created per merge (close-guard cancels it in 2.3)

    public ObservableCollection<PdfFileItem> Files { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemoveCommand))]
    public partial PdfFileItem? SelectedFile { get; set; }

    [ObservableProperty]
    public partial bool HasFiles { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowPreviewImage))]
    [NotifyPropertyChangedFor(nameof(ShowPreviewPlaceholder))]
    [NotifyPropertyChangedFor(nameof(PreviewPlaceholderText))]
    public partial PreviewState Preview { get; set; }

    [ObservableProperty]
    public partial BitmapImage? PreviewImage { get; set; }

    // --- Merge UI-lock / progress / banner state (story 2.2) ---

    [ObservableProperty]
    public partial bool IsMerging { get; set; }

    public bool CanReorderFiles => !IsMerging;

    [ObservableProperty]
    public partial double MergeProgress { get; set; }

    [ObservableProperty]
    public partial bool IsProgressVisible { get; set; }

    [ObservableProperty]
    public partial bool IsSuccessBannerOpen { get; set; }

    [ObservableProperty]
    public partial string? SuccessBannerText { get; set; }

    [ObservableProperty]
    public partial bool IsErrorBannerOpen { get; set; }

    [ObservableProperty]
    public partial string? ErrorBannerText { get; set; }

    partial void OnIsMergingChanged(bool value)
    {
        OnPropertyChanged(nameof(CanReorderFiles));
        NotifyMergeStateChanged();                 // re-raises CanMerge + MergeDisabledReason + MergeCommand
        AddFilesCommand.NotifyCanExecuteChanged();
        RemoveCommand.NotifyCanExecuteChanged();
    }

    public bool ShowPreviewImage => Preview == PreviewState.Ready;

    public bool ShowPreviewPlaceholder => Preview != PreviewState.Ready;

    public string? PreviewPlaceholderText => Preview switch
    {
        PreviewState.None => UiStrings.EmptyPreviewPlaceholder,
        PreviewState.Checking => UiStrings.PreviewChecking,
        PreviewState.ExcludedPassword => UiStrings.PreviewPasswordExclusion,
        PreviewState.ExcludedCorrupt => UiStrings.PreviewCorruptExclusion,
        _ => null,
    };

    public bool CanMerge =>
        Files.Count > 0
        && Files.Any(f => f.Status == ValidationStatus.Valid)
        && !Files.Any(f => f.Status is ValidationStatus.ErrorPassword or ValidationStatus.ErrorCorrupt or ValidationStatus.ErrorTimeout)
        && !Files.Any(f => f.Status == ValidationStatus.Checking)
        && !IsMerging;

    public string? MergeDisabledReason
    {
        get
        {
            if (Files.Count == 0)
                return UiStrings.MergeDisabledNoFiles;                 // MC-10 (empty)
            if (Files.Any(f => f.Status == ValidationStatus.Checking))
                return UiStrings.MergeDisabledStillChecking;           // MC-12 (checking outranks flagged)
            if (!Files.Any(f => f.Status == ValidationStatus.Valid))
                return UiStrings.MergeDisabledNoFiles;                 // MC-10 (all-flagged → "add a PDF")
            if (Files.Any(f => f.Status is ValidationStatus.ErrorPassword or ValidationStatus.ErrorCorrupt or ValidationStatus.ErrorTimeout))
                return UiStrings.MergeDisabledFlaggedFiles;            // MC-11 (has valid + flagged)
            return null;                                               // enabled
        }
    }

    public MainViewModel(
        IFilePickerService filePickerService,
        IPdfValidationService validationService,
        IPdfMergeService mergeService,
        IOutputWriter outputWriter,
        IFolderLauncher folderLauncher,
        IPdfPreviewService previewService)
    {
        _filePickerService = filePickerService;
        _validationService = validationService;
        _mergeService = mergeService;
        _outputWriter = outputWriter;
        _folderLauncher = folderLauncher;
        _previewService = previewService;
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        Files.CollectionChanged += OnFilesCollectionChanged;
    }

    partial void OnSelectedFileChanged(PdfFileItem? value) => _ = UpdatePreviewAsync(value);

    private async Task UpdatePreviewAsync(PdfFileItem? item)
    {
        // Single in-flight render: cancel + dispose the previous one before doing anything.
        _previewCts?.Cancel();
        _previewCts?.Dispose();
        _previewCts = null;

        if (item is null)
        {
            PreviewImage = null;
            Preview = PreviewState.None;
            return;
        }

        switch (item.Status)
        {
            case ValidationStatus.Checking:
                PreviewImage = null;
                Preview = PreviewState.Checking;
                return;
            case ValidationStatus.ErrorPassword:
                PreviewImage = null;
                Preview = PreviewState.ExcludedPassword;
                return;
            case ValidationStatus.ErrorCorrupt:
            case ValidationStatus.ErrorTimeout:
                PreviewImage = null;
                Preview = PreviewState.ExcludedCorrupt;
                return;
        }

        // Valid → render the first page. Runs on the UI thread (BitmapImage is a
        // DependencyObject); _previewCts + the ReferenceEquals guard drop a slow
        // render of file A that lands after the user has selected file B.
        var cts = _previewCts = new CancellationTokenSource();
        try
        {
            var bitmap = await _previewService.RenderFirstPageAsync(item.Path, cts.Token);

            if (cts.IsCancellationRequested || !ReferenceEquals(item, SelectedFile))
                return;

            PreviewImage = bitmap;
            Preview = PreviewState.Ready;
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer selection/status change — drop it silently.
        }
        catch
        {
            // A file that validated Valid but fails to render falls back to the corrupt notice.
            if (ReferenceEquals(item, SelectedFile))
            {
                PreviewImage = null;
                Preview = PreviewState.ExcludedCorrupt;
            }
        }
    }

    private void OnFilesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        HasFiles = Files.Count > 0;

        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            foreach (var item in _subscribedItems)
                item.PropertyChanged -= OnFilePropertyChanged;
            _subscribedItems.Clear();

            foreach (var item in Files)
            {
                item.PropertyChanged += OnFilePropertyChanged;
                _subscribedItems.Add(item);
            }
        }
        else
        {
            if (e.NewItems is not null)
            {
                foreach (PdfFileItem item in e.NewItems)
                {
                    item.PropertyChanged += OnFilePropertyChanged;
                    _subscribedItems.Add(item);
                }
            }

            if (e.OldItems is not null)
            {
                foreach (PdfFileItem item in e.OldItems)
                {
                    item.PropertyChanged -= OnFilePropertyChanged;
                    _subscribedItems.Remove(item);
                }
            }
        }

        NotifyMergeStateChanged();

        // Removing the selected file normally clears selection via the ListView's
        // SelectionChanged (which re-evaluates RemoveCommand through SelectedFile's
        // NotifyCanExecuteChangedFor). Re-check here too so the command state stays
        // correct for collection changes that don't flow through a SelectedFile change.
        RemoveCommand.NotifyCanExecuteChanged();
    }

    private void OnFilePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PdfFileItem.Status))
        {
            NotifyMergeStateChanged();

            if (ReferenceEquals(sender, SelectedFile))
                _ = UpdatePreviewAsync(SelectedFile);
        }
    }

    private void NotifyMergeStateChanged()
    {
        OnPropertyChanged(nameof(CanMerge));
        OnPropertyChanged(nameof(MergeDisabledReason));
        MergeCommand.NotifyCanExecuteChanged();
    }

    private void RunOnUI(DispatcherQueueHandler action)
    {
        if (_dispatcherQueue is null)
            action();
        else
            _dispatcherQueue.TryEnqueue(action);
    }

    private bool CanAddFiles() => !IsMerging;

    [RelayCommand(CanExecute = nameof(CanAddFiles))]
    private async Task AddFilesAsync()
    {
        var paths = await _filePickerService.PickFilesAsync();
        if (paths.Count == 0)
            return;

        foreach (var path in paths)
        {
            if (Files.Any(f => string.Equals(f.Path, path, StringComparison.OrdinalIgnoreCase)))
                continue;

            var item = new PdfFileItem(path);
            Files.Add(item);
            _ = ValidateFileAsync(item);
        }
    }

    private async Task ValidateFileAsync(PdfFileItem item)
    {
        var cts = new CancellationTokenSource();
        _validationCts[item] = cts; // registered on the UI thread (sync prefix of the call)
        try
        {
            await _validationSemaphore.WaitAsync();
            try
            {
                var validationTask = Task.Run(
                    () => _validationService.ValidateAsync(item.Path, cts.Token));
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5), CancellationToken.None);

                var completed = await Task.WhenAny(validationTask, timeoutTask);
                if (completed == timeoutTask)
                {
                    await cts.CancelAsync();
                    RunOnUI(() => { if (Files.Contains(item)) item.Status = ValidationStatus.ErrorTimeout; });
                    return;
                }

                var (status, pageCount) = await validationTask;
                RunOnUI(() =>
                {
                    if (!Files.Contains(item)) return; // removed mid-flight → drop the write
                    item.Status = status;
                    item.PageCount = pageCount;
                });
            }
            catch (OperationCanceledException)
            {
                // Timeout OR removal-triggered cancel; only mark timeout if still present.
                RunOnUI(() => { if (Files.Contains(item)) item.Status = ValidationStatus.ErrorTimeout; });
            }
            catch
            {
                RunOnUI(() => { if (Files.Contains(item)) item.Status = ValidationStatus.ErrorCorrupt; });
            }
            finally
            {
                _validationSemaphore.Release();
            }
        }
        catch (ObjectDisposedException)
        {
            // Semaphore disposed during shutdown — nothing to do
        }
        finally
        {
            _validationCts.Remove(item);
            cts.Dispose();
        }
    }

    [RelayCommand(CanExecute = nameof(CanMerge))]
    private async Task MergeAsync()
    {
        // AC #11 — Merge press clears any visible banner before the dialog opens.
        IsSuccessBannerOpen = false;
        IsErrorBannerOpen = false;

        var destination = await _filePickerService.PickSaveFileAsync(UiStrings.DefaultMergeFileName);
        if (destination is null)
            return; // FR-7: cancelling the Save dialog is a silent no-op (no lock engaged)

        var paths = Files
            .Where(f => f.Status == ValidationStatus.Valid)
            .Select(f => f.Path)
            .ToList();

        _mergeCts = new CancellationTokenSource();
        IsMerging = true;          // AC #4 — lock engages only AFTER confirm (AC #5)
        MergeProgress = 0;
        var showProgress = StartProgressDelay(); // AC #6/#7 — reveal the bar only after 2 s

        try
        {
            var progress = new Progress<double>(p => MergeProgress = p); // captures UI sync context
            using var buffer = new MemoryStream();
            var outcome = await _mergeService.MergeAsync(paths, buffer, progress, _mergeCts.Token);

            if (outcome is MergeOutcome.Failure)
            {
                ShowError(UiStrings.MergeErrorGeneric); // 2.2 = generic only; 2.3 refines
                return;
            }

            buffer.Position = 0;
            await _outputWriter.WriteAsync(buffer, destination);

            _lastOutputFolder = Path.GetDirectoryName(destination.Path);
            ShowSuccess(string.Format(UiStrings.MergeSuccess, destination.Name)); // AC #8
        }
        catch (OperationCanceledException)
        {
            // Cooperative cancel (close-guard, 2.3). No banner this story.
        }
        catch
        {
            ShowError(UiStrings.MergeErrorGeneric); // 2.2 generic; 2.3 maps specific reasons
        }
        finally
        {
            showProgress.Cancel();
            IsProgressVisible = false;
            IsMerging = false;     // AC #10 — UI unlocks; Files untouched (preserved)
            _mergeCts.Dispose();
            _mergeCts = null;
        }
    }

    // Reveal the determinate progress bar only after a 2 s delay; cancelled the
    // instant the merge finishes so sub-2 s merges show nothing (AC #6/#7).
    private CancellationTokenSource StartProgressDelay()
    {
        var cts = new CancellationTokenSource();
        _ = Task.Delay(TimeSpan.FromSeconds(2), cts.Token)
                .ContinueWith(
                    _ => RunOnUI(() => IsProgressVisible = true),
                    cts.Token,
                    TaskContinuationOptions.OnlyOnRanToCompletion,
                    TaskScheduler.Default);
        return cts;
    }

    // Banner helpers enforce "at most one banner visible" (AC #8/#11).
    private void ShowSuccess(string text)
    {
        IsErrorBannerOpen = false;
        SuccessBannerText = text;
        IsSuccessBannerOpen = true; // stays open until the user dismisses it or presses Merge again (AC #11)
    }

    private void ShowError(string text)
    {
        IsSuccessBannerOpen = false;
        ErrorBannerText = text;
        IsErrorBannerOpen = true;  // manual dismiss only
    }

    [RelayCommand]
    private async Task OpenFolderAsync()
    {
        if (_lastOutputFolder is null) return;
        var ok = await _folderLauncher.LaunchFolderAsync(_lastOutputFolder);
        if (!ok)
        {
            SuccessBannerText = UiStrings.FolderNotFound; // MC-19, shown inline in the open banner
        }
    }

    // Reorder is handled by the file ListView's built-in drag-and-drop
    // (CanReorderItems/AllowDrop/CanDragItems), which mutates the bound Files
    // collection directly — no view-model command is involved.

    private bool CanRemove() => SelectedFile is not null && !IsMerging;

    [RelayCommand(CanExecute = nameof(CanRemove))]
    private void Remove()
    {
        var item = SelectedFile;
        if (item is null) return;

        var index = Files.IndexOf(item);

        // Cancel any in-flight validation so the semaphore slot frees and no status is
        // written back to the removed item (a removed item's late completion is dropped).
        if (_validationCts.TryGetValue(item, out var cts))
            cts.Cancel();

        Files.Remove(item);

        // Removal shifts every later row up by one, so the file that was directly
        // below now sits at the removed item's old index. Select it for continuity;
        // if the removed row was last (index == Count) there is nothing below → clear.
        SelectedFile = index < Files.Count ? Files[index] : null;
    }
}
