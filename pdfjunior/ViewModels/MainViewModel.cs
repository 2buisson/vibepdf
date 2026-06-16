using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using pdfjunior.Models;
using pdfjunior.Services;
using pdfjunior.Strings;

namespace pdfjunior.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IFilePickerService _filePickerService;
    private readonly IPdfValidationService _validationService;
    private readonly DispatcherQueue? _dispatcherQueue;
    private readonly SemaphoreSlim _validationSemaphore = new(3);
    private readonly List<PdfFileItem> _subscribedItems = [];

    public ObservableCollection<PdfFileItem> Files { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(MoveUpCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveDownCommand))]
    [NotifyCanExecuteChangedFor(nameof(RemoveCommand))]
    public partial PdfFileItem? SelectedFile { get; set; }

    [ObservableProperty]
    public partial bool HasFiles { get; set; }

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

    public MainViewModel(IFilePickerService filePickerService, IPdfValidationService validationService)
    {
        _filePickerService = filePickerService;
        _validationService = validationService;
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        Files.CollectionChanged += OnFilesCollectionChanged;
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
    }

    private void OnFilePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PdfFileItem.Status))
            NotifyMergeStateChanged();
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
        try
        {
            await _validationSemaphore.WaitAsync();
            try
            {
                using var cts = new CancellationTokenSource();
                var validationTask = Task.Run(
                    () => _validationService.ValidateAsync(item.Path, cts.Token));
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5), CancellationToken.None);

                var completed = await Task.WhenAny(validationTask, timeoutTask);
                if (completed == timeoutTask)
                {
                    await cts.CancelAsync();
                    RunOnUI(() => item.Status = ValidationStatus.ErrorTimeout);
                    return;
                }

                var (status, pageCount) = await validationTask;
                RunOnUI(() =>
                {
                    item.Status = status;
                    item.PageCount = pageCount;
                });
            }
            catch (OperationCanceledException)
            {
                RunOnUI(() => item.Status = ValidationStatus.ErrorTimeout);
            }
            catch
            {
                RunOnUI(() => item.Status = ValidationStatus.ErrorCorrupt);
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
    }

    [RelayCommand(CanExecute = nameof(CanMerge))]
    private Task MergeAsync()
    {
        return Task.CompletedTask;
    }

    private bool CanMoveUp() => SelectedFile is not null;

    [RelayCommand(CanExecute = nameof(CanMoveUp))]
    private void MoveUp()
    {
    }

    private bool CanMoveDown() => SelectedFile is not null;

    [RelayCommand(CanExecute = nameof(CanMoveDown))]
    private void MoveDown()
    {
    }

    private bool CanRemove() => SelectedFile is not null;

    [RelayCommand(CanExecute = nameof(CanRemove))]
    private void Remove()
    {
    }
}
