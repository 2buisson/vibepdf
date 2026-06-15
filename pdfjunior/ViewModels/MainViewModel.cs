using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using pdfjunior.Models;

namespace pdfjunior.ViewModels;

public partial class MainViewModel : ObservableObject
{
    public ObservableCollection<PdfFileItem> Files { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(MoveUpCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveDownCommand))]
    [NotifyCanExecuteChangedFor(nameof(RemoveCommand))]
    public partial PdfFileItem? SelectedFile { get; set; }

    [ObservableProperty]
    public partial bool HasFiles { get; set; }

    public bool CanMerge => false;

    public MainViewModel()
    {
        Files.CollectionChanged += OnFilesCollectionChanged;
    }

    private void OnFilesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        HasFiles = Files.Count > 0;
    }

    [RelayCommand]
    private Task AddFilesAsync()
    {
        return Task.CompletedTask;
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
