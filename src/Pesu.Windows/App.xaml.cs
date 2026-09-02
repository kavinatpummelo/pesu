using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Pesu.Core.ViewModels;
using Pesu.Infrastructure;

namespace Pesu.Windows;

public partial class App : Application
{
    private readonly ServiceProvider _serviceProvider;

    public App()
    {
        InitializeComponent();
        var services = new ServiceCollection();
        services.AddPesuInfrastructure(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
        services.AddSingleton<MainWindow>();
        _serviceProvider = services.BuildServiceProvider();
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        var initializer = _serviceProvider.GetRequiredService<ISampleDataInitializer>();
        await initializer.EnsureSeededAsync();

        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        var viewModel = _serviceProvider.GetRequiredService<MainViewModel>();
        await viewModel.InitializeAsync();
        mainWindow.SetViewModel(viewModel);
        mainWindow.Activate();
    }
}
