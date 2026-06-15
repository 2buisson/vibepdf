using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using pdfjunior.ViewModels;

namespace pdfjunior;

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
        _window.Activate();
    }

    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        services.AddSingleton<MainViewModel>();

        return services.BuildServiceProvider();
    }
}
