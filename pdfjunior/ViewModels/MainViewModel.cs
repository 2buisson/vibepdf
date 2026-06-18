using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media.Imaging;
using pdfjunior.Models;
using pdfjunior.Services;
using pdfjunior.Strings;

namespace pdfjunior.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IFilePickerService _filePickerService;
    private readonly IPdfValidationService _validationService;
    private readonly IPdfPreviewService _previewService;
    private readonly DispatcherQueue? _dispatcherQueue;
    private readonly SemaphoreSlim _validationSemaphore = new(3);
    private readonly List<PdfFileItem> _subscribedItems = [];
    private readonly Dictionary<PdfFileItem, CancellationTokenSource> _validationCts = [];
    private CancellationTokenSource? _previewCts;

    public ObservableCollection<PdfFileItem> Files { get; } = [];

    public ObservableCollection<BitmapImage> PreviewPages { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemoveCommand))]
    public partial PdfFileItem? SelectedFile { get; set; }

    [ObservableProperty]
    public partial bool HasFiles { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowPreviewPages))]
    [NotifyPropertyChangedFor(nameof(ShowPreviewPlaceholder))]
    [NotifyPropertyChangedFor(nameof(PreviewPlaceholderText))]
    public partial PreviewState Preview { get; set; }

    [ObservableProperty]
    public partial double PreviewViewportWidth { get; set; }

    public bool ShowPreviewPages => Preview == PreviewState.Ready;

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
        && !Files.Any(f => f.Status == ValidationStatus.Checking);

    public string? MergeDisabledReason
    {
        get
        {
            if (Files.Count == 0)
                return UiStrings.MergeDisabledNoFiles;
            if (Files.Any(f => f.Status is ValidationStatus.ErrorPassword or ValidationStatus.ErrorCorrupt or ValidationStatus.ErrorTimeout))
                return UiStrings.MergeDisabledFlaggedFiles;
            if (Files.Any(f => f.Status == ValidationStatus.Checking))
                return UiStrings.MergeDisabledStillChecking;
            if (!Files.Any(f => f.Status == ValidationStatus.Valid))
                return UiStrings.MergeDisabledNoFiles;
            return null;
        }
    }

    public MainViewModel(IFilePickerService filePickerService, IPdfValidationService validationService, IPdfPreviewService previewService)
    {
        _filePickerService = filePickerService;
        _validationService = validationService;
        _previewService = previewService;
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        Files.CollectionChanged += OnFilesCollectionChanged;
    }

    partial void OnSelectedFileChanged(PdfFileItem? value) => _ = UpdatePreviewAsync(value);

    partial void OnPreviewViewportWidthChanged(double value)
    {
        if (value > 0 && Preview != PreviewState.Ready && SelectedFile?.Status == ValidationStatus.Valid)
            _ = UpdatePreviewAsync(SelectedFile);
    }

    private async Task UpdatePreviewAsync(PdfFileItem? item)
    {
        _previewCts?.Cancel();
        _previewCts?.Dispose();
        var cts = _previewCts = new CancellationTokenSource();

        PreviewPages.Clear();

        if (item is null)
        {
            Preview = PreviewState.None;
            return;
        }

        switch (item.Status)
        {
            case ValidationStatus.Checking:
                Preview = PreviewState.Checking;
                return;
            case ValidationStatus.ErrorPassword:
                Preview = PreviewState.ExcludedPassword;
                return;
            case ValidationStatus.ErrorCorrupt:
            case ValidationStatus.ErrorTimeout:
                Preview = PreviewState.ExcludedCorrupt;
                return;
        }

        try
        {
            var path = item.Path;
            var pages = await _previewService.RenderPagesAsync(path, PreviewViewportWidth, cts.Token);

            if (cts.IsCancellationRequested || !ReferenceEquals(item, SelectedFile))
                return;

            RunOnUI(() =>
            {
                foreach (var page in pages)
                    PreviewPages.Add(page);
                Preview = PreviewState.Ready;
            });
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            if (!cts.IsCancellationRequested && ReferenceEquals(item, SelectedFile))
                RunOnUI(() => Preview = PreviewState.ExcludedCorrupt);
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

    [RelayCommand]
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
    private Task MergeAsync()
    {
        return Task.CompletedTask;
    }

    // Reorder is handled by the file ListView's built-in drag-and-drop
    // (CanReorderItems/AllowDrop/CanDragItems), which mutates the bound Files
    // collection directly — no view-model command is involved.

    private bool CanRemove() => SelectedFile is not null;

    [RelayCommand(CanExecute = nameof(CanRemove))]
    private void Remove()
    {
        var item = SelectedFile;
        if (item is null) return;

        // Cancel any in-flight validation so the semaphore slot frees and no status is
        // written back to the removed item (a removed item's late completion is dropped).
        if (_validationCts.TryGetValue(item, out var cts))
            cts.Cancel();

        Files.Remove(item);  // ListView auto-deselects the removed item
        SelectedFile = null; // explicit clear — makes the VM correct without a ListView
    }
}
