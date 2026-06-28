using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using vibepdf.Models;
using vibepdf.Services;
using vibepdf.Strings;
using Windows.Graphics;
using Windows.UI;
using WinRT.Interop;

namespace vibepdf;

public sealed partial class MainWindow : Window
{
    private const int MinWidth = 640;
    private const int MinHeight = 480;

    private bool _isSplitterDragging;
    private double _splitterDragStartX;
    private double _splitterDragStartWidth;

    // --- App state (folded in from the former view model) ---

    private readonly IFilePickerService _filePickerService;
    private readonly IPdfValidationService _validationService;
    private readonly IPdfMergeService _mergeService;
    private readonly IOutputWriter _outputWriter;
    private readonly IFolderLauncher _folderLauncher;
    private readonly IPdfPreviewService _previewService;
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly SemaphoreSlim _validationSemaphore = new(3);
    private readonly Dictionary<PdfFileItem, CancellationTokenSource> _validationCts = [];

    // BCL backing store for the file ListView (drag-reorder mutates it directly).
    private readonly ObservableCollection<PdfFileItem> _files = [];

    private PdfFileItem? _selectedFile;
    private bool _isMerging;
    private double _mergeProgress;

    private string? _lastOutputFolder;            // captured on success; used by Open folder
    private CancellationTokenSource? _previewCts;  // single in-flight first-page render; cancel on every selection/status change
    private CancellationTokenSource? _mergeCts;    // created per merge (close-guard cancels it in 2.3)

    public nint Hwnd { get; private set; }

    public MainWindow()
    {
        var services = App.Current.Services;
        _filePickerService = services.GetRequiredService<IFilePickerService>();
        _validationService = services.GetRequiredService<IPdfValidationService>();
        _mergeService = services.GetRequiredService<IPdfMergeService>();
        _outputWriter = services.GetRequiredService<IOutputWriter>();
        _folderLauncher = services.GetRequiredService<IFolderLauncher>();
        _previewService = services.GetRequiredService<IPdfPreviewService>();

        InitializeComponent();

        _dispatcherQueue = DispatcherQueue;
        _files.CollectionChanged += OnFilesCollectionChanged;
        FileListView.ItemsSource = _files;

        // Initial UI state (no bindings drive these any more).
        RemoveButton.IsEnabled = false;
        UpdateEmptyPlaceholder();
        SetPreview(PreviewState.None, null);
        UpdateMergeState();

        Hwnd = WindowNative.GetWindowHandle(this);
        AppWindow.Resize(new SizeInt32(900, 640));

        SetMinWindowSize();

        // Extend content under the title bar so the sidebar's tinted background
        // runs to the top of the window. AppTitleBar (from XAML) becomes the
        // draggable region; the system still draws the caption buttons on top.
        ExtendsContentIntoTitleBar = true;
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        SetTitleBar(AppTitleBar);

        // Keep the title bar in sync with the app's resolved (system) theme.
        ApplyTitleBarTheme();
        if (Content is FrameworkElement root)
        {
            root.ActualThemeChanged += (_, _) => ApplyTitleBarTheme();
        }
    }

    // --- File list / selection ---

    private async void AddButton_Click(object sender, RoutedEventArgs e)
    {
        var paths = await _filePickerService.PickFilesAsync();
        if (paths.Count == 0)
            return;

        foreach (var path in paths)
        {
            if (_files.Any(f => string.Equals(f.Path, path, StringComparison.OrdinalIgnoreCase)))
                continue;

            var item = new PdfFileItem(path);
            _files.Add(item);
            _ = ValidateFileAsync(item);
        }
    }

    private void FileListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var selected = FileListView.SelectedItem as PdfFileItem;

        // Only react to a genuine change of the selected file. A drag-reorder keeps the
        // same selected instance, so the preview is not re-rendered (parity with the
        // old TwoWay-bound SelectedFile, which only fired on an actual value change).
        if (ReferenceEquals(selected, _selectedFile))
            return;

        _selectedFile = selected;
        RemoveButton.IsEnabled = _selectedFile is not null && !_isMerging;
        _ = UpdatePreviewAsync(selected);
    }

    private void RemoveButton_Click(object sender, RoutedEventArgs e)
    {
        var item = _selectedFile;
        if (item is null) return;

        var index = _files.IndexOf(item);

        // Cancel any in-flight validation so the semaphore slot frees and no status is
        // written back to the removed item (a removed item's late completion is dropped).
        if (_validationCts.TryGetValue(item, out var cts))
            cts.Cancel();

        _files.Remove(item);

        // Removal shifts every later row up by one, so the file that was directly below
        // now sits at the removed item's old index. Select it for continuity; if the
        // removed row was last (index == Count) there is nothing below → clear.
        FileListView.SelectedItem = index < _files.Count ? _files[index] : null;
    }

    private void OnFilesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        UpdateEmptyPlaceholder();
        UpdateMergeState();
    }

    // Called after an item's validation status changes. Replaces the old INPC-driven
    // OnFilePropertyChanged: refresh the row, re-evaluate merge gating, and re-render
    // the preview when the changed item is the selected one.
    private void OnItemStatusChanged(PdfFileItem item)
    {
        RefreshRow(item);
        UpdateMergeState();
        if (ReferenceEquals(item, _selectedFile))
            _ = UpdatePreviewAsync(item);
    }

    // Imperative row refresh: locate the realized container and update its TextBlocks
    // directly. No-ops safely when the row is virtualized (ContainerFromItem == null).
    private void RefreshRow(PdfFileItem item)
    {
        if (FileListView.ContainerFromItem(item) is not ListViewItem container) return;
        if (container.ContentTemplateRoot is FrameworkElement root
            && root.FindName("StatusText") is TextBlock statusText)
        {
            statusText.Text = FormatStatus(item.Status, item.PageCount);
            statusText.Foreground = StatusForeground(item.Status);
        }
    }

    private void UpdateEmptyPlaceholder()
    {
        var hasFiles = _files.Count > 0;
        FileListView.Visibility = BoolToVisibility(hasFiles);
        EmptyListPlaceholder.Visibility = BoolToVisibilityInverse(hasFiles);
    }

    // --- Validation pipeline (off-thread, 3-permit semaphore, 5s timeout) ---

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
                    RunOnUI(() => { if (_files.Contains(item)) { item.Status = ValidationStatus.ErrorTimeout; OnItemStatusChanged(item); } });
                    return;
                }

                var (status, pageCount) = await validationTask;
                RunOnUI(() =>
                {
                    if (!_files.Contains(item)) return; // removed mid-flight → drop the write
                    item.Status = status;
                    item.PageCount = pageCount;
                    OnItemStatusChanged(item);
                });
            }
            catch (OperationCanceledException)
            {
                // Timeout OR removal-triggered cancel; only mark timeout if still present.
                RunOnUI(() => { if (_files.Contains(item)) { item.Status = ValidationStatus.ErrorTimeout; OnItemStatusChanged(item); } });
            }
            catch
            {
                RunOnUI(() => { if (_files.Contains(item)) { item.Status = ValidationStatus.ErrorCorrupt; OnItemStatusChanged(item); } });
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

    // --- Preview (single in-flight first-page render with staleness guard) ---

    private async Task UpdatePreviewAsync(PdfFileItem? item)
    {
        // Single in-flight render: cancel + dispose the previous one before doing anything.
        _previewCts?.Cancel();
        _previewCts?.Dispose();
        _previewCts = null;

        if (item is null)
        {
            SetPreview(PreviewState.None, null);
            return;
        }

        switch (item.Status)
        {
            case ValidationStatus.Checking:
                SetPreview(PreviewState.Checking, null);
                return;
            case ValidationStatus.ErrorPassword:
                SetPreview(PreviewState.ExcludedPassword, null);
                return;
            case ValidationStatus.ErrorCorrupt:
            case ValidationStatus.ErrorTimeout:
                SetPreview(PreviewState.ExcludedCorrupt, null);
                return;
        }

        // Valid → render the first page. Runs on the UI thread (BitmapImage is a
        // DependencyObject); _previewCts + the ReferenceEquals guard drop a slow
        // render of file A that lands after the user has selected file B.
        var cts = _previewCts = new CancellationTokenSource();
        try
        {
            var bitmap = await _previewService.RenderFirstPageAsync(item.Path, cts.Token);

            if (cts.IsCancellationRequested || !ReferenceEquals(item, _selectedFile))
                return;

            SetPreview(PreviewState.Ready, bitmap);
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer selection/status change — drop it silently.
        }
        catch
        {
            // A file that validated Valid but fails to render falls back to the corrupt notice.
            if (ReferenceEquals(item, _selectedFile))
                SetPreview(PreviewState.ExcludedCorrupt, null);
        }
    }

    // Pushes a preview state to the controls: the image card and its placeholder are
    // visibility-toggled, and the placeholder text matches the state.
    private void SetPreview(PreviewState state, BitmapImage? image)
    {
        PreviewImage.Source = image;

        var showImage = state == PreviewState.Ready;
        PreviewCard.Visibility = BoolToVisibility(showImage);
        PreviewPlaceholder.Visibility = BoolToVisibilityInverse(showImage);
        PreviewPlaceholder.Text = state switch
        {
            PreviewState.None => UiStrings.EmptyPreviewPlaceholder,
            PreviewState.Checking => UiStrings.PreviewChecking,
            PreviewState.ExcludedPassword => UiStrings.PreviewPasswordExclusion,
            PreviewState.ExcludedCorrupt => UiStrings.PreviewCorruptExclusion,
            _ => string.Empty,
        };
    }

    // --- Merge gating ---

    private void UpdateMergeState()
    {
        MergeButton.IsEnabled = CanMerge();
        ToolTipService.SetToolTip(MergeButtonTooltipHost, MergeDisabledReason());
    }

    private bool CanMerge() =>
        _files.Count > 0
        && _files.Any(f => f.Status == ValidationStatus.Valid)
        && !_files.Any(f => f.Status is ValidationStatus.ErrorPassword or ValidationStatus.ErrorCorrupt or ValidationStatus.ErrorTimeout)
        && !_files.Any(f => f.Status == ValidationStatus.Checking)
        && !_isMerging;

    private string? MergeDisabledReason()
    {
        if (_files.Count == 0)
            return UiStrings.MergeDisabledNoFiles;                 // MC-10 (empty)
        if (_files.Any(f => f.Status == ValidationStatus.Checking))
            return UiStrings.MergeDisabledStillChecking;           // MC-12 (checking outranks flagged)
        if (!_files.Any(f => f.Status == ValidationStatus.Valid))
            return UiStrings.MergeDisabledNoFiles;                 // MC-10 (all-flagged → "add a PDF")
        if (_files.Any(f => f.Status is ValidationStatus.ErrorPassword or ValidationStatus.ErrorCorrupt or ValidationStatus.ErrorTimeout))
            return UiStrings.MergeDisabledFlaggedFiles;            // MC-11 (has valid + flagged)
        return null;                                               // enabled
    }

    // --- Merge flow (per-merge CTS, progress-in-title, UI lock, result dialog) ---

    private async void MergeButton_Click(object sender, RoutedEventArgs e)
    {
        var destination = await _filePickerService.PickSaveFileAsync(UiStrings.DefaultMergeFileName);
        if (destination is null)
            return; // FR-7: cancelling the Save dialog is a silent no-op (no lock engaged)

        var paths = _files
            .Where(f => f.Status == ValidationStatus.Valid)
            .Select(f => f.Path)
            .ToList();

        _mergeCts = new CancellationTokenSource();
        SetMergeProgress(0);
        SetIsMerging(true);        // AC #4 — lock engages only AFTER confirm (AC #5); title shows 0% immediately

        try
        {
            var progress = new Progress<double>(SetMergeProgress); // captures UI sync context
            using var buffer = new MemoryStream();
            var outcome = await _mergeService.MergeAsync(paths, buffer, progress, _mergeCts.Token);

            if (outcome is MergeOutcome.Failure)
            {
                ShowMergeResult(success: false, UiStrings.MergeErrorGeneric); // 2.2 = generic only; 2.3 refines
                return;
            }

            buffer.Position = 0;
            await _outputWriter.WriteAsync(buffer, destination);

            _lastOutputFolder = Path.GetDirectoryName(destination.Path);
            ShowMergeResult(success: true, string.Format(UiStrings.MergeSuccess, destination.Name)); // AC #8
        }
        catch (OperationCanceledException)
        {
            // Cooperative cancel (close-guard, 2.3). No dialog.
        }
        catch
        {
            ShowMergeResult(success: false, UiStrings.MergeErrorGeneric); // 2.2 generic; 2.3 maps specific reasons
        }
        finally
        {
            SetIsMerging(false);   // AC #10 — UI unlocks; title reverts; Files untouched (preserved)
            _mergeCts.Dispose();
            _mergeCts = null;
        }
    }

    private void SetIsMerging(bool value)
    {
        _isMerging = value;

        var unlocked = !value;
        AddButton.IsEnabled = unlocked;
        RemoveButton.IsEnabled = unlocked && _selectedFile is not null;
        FileListView.CanReorderItems = unlocked;
        FileListView.CanDragItems = unlocked;
        FileListView.AllowDrop = unlocked;

        UpdateMergeState();
        UpdateTitle();
    }

    private void SetMergeProgress(double value)
    {
        _mergeProgress = value;
        UpdateTitle();
    }

    // Custom title-bar text. While a merge is in progress it shows the live
    // percentage ("Vibe PDF — 55%"); otherwise just the app name.
    private void UpdateTitle() =>
        TitleText.Text = _isMerging
            ? string.Format(UiStrings.AppTitleMergeProgress, UiStrings.AppTitle, _mergeProgress)
            : UiStrings.AppTitle;

    // Show the merge outcome in a modal dialog. Title is always the app name so the
    // success and error dialogs read consistently; the message carries the specifics.
    // Fire-and-forget so the merge flow's finally (UI unlock, title revert) runs while
    // the dialog stays open — matching the previous event-driven behavior.
    private void ShowMergeResult(bool success, string message) =>
        _ = ShowMergeResultAsync(success, message);

    private async Task ShowMergeResultAsync(bool success, string message)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = Content.XamlRoot,
            Title = UiStrings.AppTitle,
            Content = message,
            CloseButtonText = UiStrings.DialogClose,
        };

        if (Content is FrameworkElement root)
            dialog.RequestedTheme = root.ActualTheme;

        if (success)
        {
            dialog.PrimaryButtonText = UiStrings.MergeSuccessOpenFolder;
            dialog.DefaultButton = ContentDialogButton.Primary;

            // "Open folder" must not dismiss the dialog. Cancel is set before the first
            // await, so the dialog stays open and the continuation updates it in place.
            dialog.PrimaryButtonClick += async (dlg, clickArgs) =>
            {
                clickArgs.Cancel = true;
                if (!await TryOpenLastFolderAsync())
                    dlg.Content = UiStrings.FolderNotFound; // MC-19, surfaced inline
            };
        }

        await dialog.ShowAsync();
    }

    // Opens the folder captured on the last successful merge. Returns false when the
    // folder is gone so the dialog can surface MC-19 inline and stay open.
    private async Task<bool> TryOpenLastFolderAsync()
    {
        if (_lastOutputFolder is null) return false;
        return await _folderLauncher.LaunchFolderAsync(_lastOutputFolder);
    }

    private void RunOnUI(DispatcherQueueHandler action) => _dispatcherQueue.TryEnqueue(action);

    private void GridSplitter_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        SetSizeWestEastCursor();
    }

    private void GridSplitter_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (!_isSplitterDragging)
        {
            SetArrowCursor();
        }
    }

    private void GridSplitter_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        _isSplitterDragging = true;
        _splitterDragStartX = e.GetCurrentPoint((UIElement)Content).Position.X;
        _splitterDragStartWidth = SidebarColumn.ActualWidth;
        ((Border)sender).CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void GridSplitter_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isSplitterDragging) return;

        var currentX = e.GetCurrentPoint((UIElement)Content).Position.X;
        var delta = currentX - _splitterDragStartX;
        var newWidth = Math.Clamp(
            _splitterDragStartWidth + delta,
            SidebarColumn.MinWidth,
            SidebarColumn.MaxWidth);

        SidebarColumn.Width = new GridLength(newWidth, GridUnitType.Pixel);
        e.Handled = true;
    }

    private void GridSplitter_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        _isSplitterDragging = false;
        ((Border)sender).ReleasePointerCapture(e.Pointer);
        SetArrowCursor();
        e.Handled = true;
    }

    private static void SetSizeWestEastCursor()
    {
        var cursor = LoadCursor(nint.Zero, 32644); // IDC_SIZEWE
        SetCursor(cursor);
    }

    private static void SetArrowCursor()
    {
        var cursor = LoadCursor(nint.Zero, 32512); // IDC_ARROW
        SetCursor(cursor);
    }

    public static Visibility BoolToVisibility(bool value) =>
        value ? Visibility.Visible : Visibility.Collapsed;

    public static Visibility BoolToVisibilityInverse(bool value) =>
        value ? Visibility.Collapsed : Visibility.Visible;

    public static string FormatStatus(ValidationStatus status, int? pageCount) => status switch
    {
        ValidationStatus.Checking => UiStrings.StatusChecking,
        ValidationStatus.Valid => string.Format(
            pageCount == 1 ? UiStrings.StatusValidSingular : UiStrings.StatusValidPlural,
            pageCount),
        ValidationStatus.ErrorPassword => UiStrings.StatusErrorPassword,
        ValidationStatus.ErrorCorrupt => UiStrings.StatusErrorCorrupt,
        ValidationStatus.ErrorTimeout => UiStrings.StatusErrorTimeout,
        _ => string.Empty,
    };

    public static Brush StatusForeground(ValidationStatus status)
    {
        var key = status is ValidationStatus.ErrorPassword or ValidationStatus.ErrorCorrupt or ValidationStatus.ErrorTimeout
            ? "SystemFillColorCriticalBrush"
            : "TextFillColorSecondaryBrush";

        if (Application.Current.Resources.TryGetValue(key, out var resource) && resource is Brush brush)
            return brush;

        return new SolidColorBrush(Microsoft.UI.Colors.Gray);
    }

    private void SetMinWindowSize()
    {
        SetWindowSubclass(Hwnd, SubclassProc, 0, 0);
    }

    private static nint SubclassProc(nint hWnd, uint uMsg, nint wParam, nint lParam, nuint uIdSubclass, nuint dwRefData)
    {
        const uint WM_GETMINMAXINFO = 0x0024;

        if (uMsg == WM_GETMINMAXINFO)
        {
            var dpi = GetDpiForWindow(hWnd);
            var scalingFactor = dpi / 96.0;

            var minMaxInfo = Marshal.PtrToStructure<MINMAXINFO>(lParam);
            minMaxInfo.ptMinTrackSize.X = (int)(MinWidth * scalingFactor);
            minMaxInfo.ptMinTrackSize.Y = (int)(MinHeight * scalingFactor);
            Marshal.StructureToPtr(minMaxInfo, lParam, false);
        }

        return DefSubclassProc(hWnd, uMsg, wParam, lParam);
    }

    // DWMWA_USE_IMMERSIVE_DARK_MODE: renders the standard title bar in dark mode
    // (white caption text/glyphs) while preserving the native red close button.
    private const uint DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    private void ApplyTitleBarTheme()
    {
        if (Content is not FrameworkElement root)
            return;

        var isDark = root.ActualTheme == ElementTheme.Dark;

        // Keep the window frame/border in step with the theme.
        int useDarkMode = isDark ? 1 : 0;
        DwmSetWindowAttribute(Hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDarkMode, sizeof(int));

        // With content extended under the title bar the framework draws the
        // caption buttons: colour their glyphs to match the theme and keep the
        // backgrounds transparent so the Mica/sidebar backdrop shows through.
        // Hover/pressed backgrounds are left at their defaults so the close
        // button keeps its native red.
        var titleBar = AppWindow.TitleBar;
        var foreground = isDark ? Colors.White : Colors.Black;
        var inactiveForeground = Color.FromArgb(0xFF, 0x80, 0x80, 0x80);

        titleBar.ButtonBackgroundColor = Colors.Transparent;
        titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
        titleBar.ButtonForegroundColor = foreground;
        titleBar.ButtonHoverForegroundColor = foreground;
        titleBar.ButtonPressedForegroundColor = foreground;
        titleBar.ButtonInactiveForegroundColor = inactiveForeground;
    }

    [LibraryImport("dwmapi.dll")]
    private static partial int DwmSetWindowAttribute(nint hWnd, uint dwAttribute, ref int pvAttribute, int cbAttribute);

    [LibraryImport("comctl32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetWindowSubclass(nint hWnd, SubclassCallback pfnSubclass, nuint uIdSubclass, nuint dwRefData);

    [LibraryImport("comctl32.dll")]
    private static partial nint DefSubclassProc(nint hWnd, uint uMsg, nint wParam, nint lParam);

    [LibraryImport("user32.dll")]
    private static partial uint GetDpiForWindow(nint hWnd);

    [LibraryImport("user32.dll")]
    private static partial nint SetCursor(nint hCursor);

    [LibraryImport("user32.dll", EntryPoint = "LoadCursorW")]
    private static partial nint LoadCursor(nint hInstance, int lpCursorName);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate nint SubclassCallback(nint hWnd, uint uMsg, nint wParam, nint lParam, nuint uIdSubclass, nuint dwRefData);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MINMAXINFO
    {
        public POINT ptReserved;
        public POINT ptMaxSize;
        public POINT ptMaxPosition;
        public POINT ptMinTrackSize;
        public POINT ptMaxTrackSize;
    }
}
