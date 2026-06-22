using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using pdfjunior.Models;
using pdfjunior.Strings;
using pdfjunior.ViewModels;
using Windows.Graphics;
using WinRT.Interop;

namespace pdfjunior;

public sealed partial class MainWindow : Window
{
    private const int MinWidth = 640;
    private const int MinHeight = 480;

    private bool _isSplitterDragging;
    private double _splitterDragStartX;
    private double _splitterDragStartWidth;

    public MainViewModel ViewModel { get; }

    public nint Hwnd { get; private set; }

    public MainWindow()
    {
        ViewModel = App.Current.Services.GetRequiredService<MainViewModel>();
        InitializeComponent();

        Hwnd = WindowNative.GetWindowHandle(this);
        AppWindow.Resize(new SizeInt32(900, 640));

        SetMinWindowSize();
    }

    private void FileListView_SelectionChanged(object sender, Microsoft.UI.Xaml.Controls.SelectionChangedEventArgs e)
    {
        ViewModel.SelectedFile = FileListView.SelectedItem as Models.PdfFileItem;
    }

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
