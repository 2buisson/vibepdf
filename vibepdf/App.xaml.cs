using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using vibepdf.Services;
using WinRT.Interop;

namespace vibepdf;

public partial class App : Application
{
    private Window? _window;

    public IServiceProvider Services { get; }

    public new static App Current => (App)Application.Current;

    public App()
    {
        Services = ConfigureServices();
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        var pickerService = Services.GetRequiredService<FilePickerService>();
        pickerService.Hwnd = WindowNative.GetWindowHandle(_window);
        _window.Activate();
    }

    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        services.AddSingleton<FilePickerService>();
        services.AddSingleton<IFilePickerService>(sp => sp.GetRequiredService<FilePickerService>());
        services.AddSingleton<IPdfValidationService, PdfValidationService>();
        services.AddSingleton<IPdfPreviewService, PdfPreviewService>();
        services.AddSingleton<IPdfMergeService, PdfSharpMergeService>();
        services.AddSingleton<IOutputWriter, OutputWriter>();
        services.AddSingleton<IFolderLauncher, FolderLauncher>();

        return services.BuildServiceProvider();
    }
}
