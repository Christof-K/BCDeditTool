using System.IO;
using System.Windows;
using BcdEditTool.Configuration.BootManager;
using BcdEditTool.Interfaces.BootManager;
using BcdEditTool.Services.BootManager;
using BcdEditTool.ViewModels.BootManager;
using BcdEditTool.Views.BootManager;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BcdEditTool;

/// <summary>
/// Interaction logic for App.xaml with dependency injection hosting.
/// </summary>
public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .Build();

        var serviceCollection = new ServiceCollection();
        ConfigureServices(serviceCollection, configuration);

        _serviceProvider = serviceCollection.BuildServiceProvider();

        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }

    private static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(configuration);

        services.AddLogging(loggingBuilder =>
        {
            loggingBuilder.AddConsole();
            loggingBuilder.SetMinimumLevel(LogLevel.Information);
        });

        services.AddOptions<BootManagerOptions>()
            .BindConfiguration(BootManagerOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IDriveDetectionService, DriveDetectionService>();
        services.AddSingleton<IBcdCommandService, BcdCommandService>();
        services.AddSingleton<IBootManagerFacade, BootManagerFacade>();

        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();
    }
}
