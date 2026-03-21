using System.Windows;
using CloudyWing.SchemaExporter.Core;
using CloudyWing.SchemaExporter.Core.Exporting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CloudyWing.SchemaExporter;

/// <summary>
/// App.xaml 的互動邏輯。
/// </summary>
public partial class App : Application {
    protected override void OnStartup(StartupEventArgs e) {
        SpreadsheetExporterBootstrapper.Configure();

        IConfiguration configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();

        ServiceCollection serviceCollection = new();
        serviceCollection.AddLogging(logging => {
            logging.ClearProviders();
            logging.AddDebug();
            logging.SetMinimumLevel(LogLevel.Information);
        });
        serviceCollection.AddSchemaExporterCore(configuration);
        serviceCollection.AddTransient<MainWindow>();
        serviceCollection.AddTransient<ViewModel>();

        ServiceProvider serviceProvider = serviceCollection.BuildServiceProvider();

        MainWindow mainWindow = serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }
}

