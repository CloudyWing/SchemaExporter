using System.IO;
using System.Text.Json;
using System.Windows;
using CloudyWing.SchemaExporter.Cli;
using CloudyWing.SchemaExporter.Core;
using CloudyWing.SchemaExporter.Core.Exporting;
using CloudyWing.SchemaExporter.Services;
using CloudyWing.SchemaExporter.ViewModels;
using CloudyWing.SchemaExporter.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Velopack;

namespace CloudyWing.SchemaExporter;

/// <summary>
/// App.xaml 的互動邏輯。
/// </summary>
public partial class App : Application {
    private ServiceProvider? serviceProvider;

    public App() {
        VelopackApp.Build().Run();
    }

    protected override async void OnStartup(StartupEventArgs e) {
        base.OnStartup(e);

        bool isCliMode = HasCliArguments(e.Args);
        if (isCliMode) {
            _ = CliConsoleSession.Attach();
        }

        try {
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
                logging.AddSimpleConsole(options => {
                    options.SingleLine = true;
                    options.TimestampFormat = "HH:mm:ss ";
                });
                logging.SetMinimumLevel(LogLevel.Information);
            });
            serviceCollection.AddSchemaExporterCore(configuration);
            serviceCollection.AddSingleton<ISettingsService, JsonSettingsService>();
            serviceCollection.AddSingleton<IUpdateService, VelopackUpdateService>();
            serviceCollection.AddSingleton<CliRunner>();
            serviceCollection.AddTransient<MainWindow>(sp => new MainWindow(
                sp.GetRequiredService<ViewModel>(),
                sp,
                sp.GetRequiredService<IUpdateService>()
            ));
            serviceCollection.AddTransient<ViewModel>(sp => new ViewModel(
                sp.GetRequiredService<ISettingsService>(),
                sp.GetRequiredService<SchemaExportOrchestrator>()
            ));
            serviceCollection.AddTransient<SettingsWindow>(sp => new SettingsWindow(
                sp.GetRequiredService<SettingsViewModel>()
            ));
            serviceCollection.AddTransient<SettingsViewModel>();

            serviceProvider = serviceCollection.BuildServiceProvider();

            if (isCliMode) {
                int exitCode = await serviceProvider.GetRequiredService<CliRunner>().RunAsync(e.Args);
                Shutdown(exitCode);
                return;
            }

            MainWindow mainWindow = serviceProvider.GetRequiredService<MainWindow>();
            await mainWindow.InitializeAsync();
            MainWindow = mainWindow;
            mainWindow.Show();
            _ = mainWindow.CheckForUpdatesAsync();
        } catch (Exception ex) when (ex is IOException or InvalidOperationException or JsonException) {
            if (isCliMode) {
                Console.Error.WriteLine($"Startup failed: {ex.Message}");
            } else {
                MessageBox.Show($"應用程式啟動失敗：{ex.Message}", "啟動失敗", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e) {
        serviceProvider?.Dispose();
        base.OnExit(e);
    }

    private static bool HasCliArguments(IReadOnlyList<string> args) {
        if (args.Count == 0) {
            return false;
        }

        string firstArgument = args[0];
        return firstArgument.StartsWith("--", StringComparison.Ordinal)
            || firstArgument is "-h" or "/?"
            || string.Equals(firstArgument, "export", StringComparison.OrdinalIgnoreCase)
            || string.Equals(firstArgument, "diff", StringComparison.OrdinalIgnoreCase)
            || string.Equals(firstArgument, "--help", StringComparison.OrdinalIgnoreCase);
    }
}
