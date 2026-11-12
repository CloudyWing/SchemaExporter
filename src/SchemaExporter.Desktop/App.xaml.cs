using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using CloudyWing.SchemaExporter.Core;
using CloudyWing.SchemaExporter.Core.Exporting;
using CloudyWing.SchemaExporter.Desktop.Hosting;
using CloudyWing.SchemaExporter.Desktop.Services;
using CloudyWing.SchemaExporter.Desktop.State;
using CloudyWing.SchemaExporter.Infrastructure;
using CloudyWing.SchemaExporter.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MudBlazor.Services;
using Velopack;

namespace CloudyWing.SchemaExporter.Desktop;

/// <summary>
/// 負責 Desktop 啟動、相依性注入組裝與主視窗生命週期。
/// </summary>
public partial class App : Application {
    private ServiceProvider? serviceProvider;
    private WebViewRecoveryCoordinator? recoveryCoordinator;

    /// <summary>
    /// 初始化 <see cref="App"/> 類別的新執行個體，並啟動 Velopack 更新流程。
    /// </summary>
    public App() {
        // Velopack 的安裝、更新 hook 必須在任何 UI 初始化之前處理，hook 執行完會直接結束處理序。
        VelopackApp.Build().Run();
    }

    protected override void OnStartup(StartupEventArgs e) {
        base.OnStartup(e);

        // CLI 已移至 tools\CloudyWing.SchemaExporter.Cli.exe。帶引數啟動時不開視窗，
        // 避免舊版 Agent Skill 或排程呼叫時開出沒人操作的桌面視窗而卡住流程。
        if (e.Args.Length > 0) {
            Shutdown(1);
            return;
        }

        try {
            SpreadsheetExporterBootstrapper.Configure();
            IConfiguration configuration = SchemaExporterConfiguration.Build(requiresUserConfig: true);
            serviceProvider = BuildServiceProvider(configuration);

            ExportWorkspace exportWorkspace = serviceProvider.GetRequiredService<ExportWorkspace>();
            WebViewRecoveryLog recoveryLog = new(
                Path.Combine(AppPaths.UserConfigDirectory, "logs", "webview-recovery.log"),
                TimeProvider.System
            );
            recoveryCoordinator = new WebViewRecoveryCoordinator(
                Dispatcher,
                recoveryLog,
                () => exportWorkspace.IsExporting,
                TimeProvider.System
            );
            DispatcherUnhandledException += HandleDispatcherUnhandledException;

            MainWindow mainWindow = new(serviceProvider, recoveryCoordinator);
            MainWindow = mainWindow;
            mainWindow.Show();
        } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or InvalidDataException or JsonException) {
            MessageBox.Show($"應用程式啟動失敗：{ex.Message}", "啟動失敗", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e) {
        DispatcherUnhandledException -= HandleDispatcherUnhandledException;
        recoveryCoordinator?.Dispose();
        serviceProvider?.Dispose();
        base.OnExit(e);
    }

    private void HandleDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e) {
        // 只吞 WebView2 圖形資源遺失造成的已知例外，其餘例外維持原本的未處理行為。
        if (recoveryCoordinator?.TryHandleUnhandledException(e.Exception) == true) {
            e.Handled = true;
        }
    }

    private static ServiceProvider BuildServiceProvider(IConfiguration configuration) {
        ServiceCollection services = new();
        services.AddLogging(logging => {
            logging.ClearProviders();
            logging.AddDebug();
            logging.SetMinimumLevel(LogLevel.Information);
        });
        services.AddWpfBlazorWebView();
        services.AddMudServices();
#if DEBUG
        services.AddBlazorWebViewDeveloperTools();
#endif
        services.AddSchemaExporterCore(configuration);
        services.AddSchemaExporterInfrastructure();
        services.AddSingleton<IDialogService, WpfDialogService>();
        services.AddSingleton<IFileDialogService, WpfFileDialogService>();
        services.AddSingleton<IUpdateService, VelopackUpdateService>();
        services.AddSingleton<ExportWorkspace>();
        services.AddSingleton<SettingsWorkspace>();
        services.AddSingleton<UpdateWorkspace>();
        services.AddSingleton(new ThemePreference(Path.Combine(AppPaths.UserConfigDirectory, "desktop.json")));

        return services.BuildServiceProvider();
    }
}
