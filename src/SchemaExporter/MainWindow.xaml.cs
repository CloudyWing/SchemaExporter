using System.IO;
using System.Text.Json;
using System.Windows;
using CloudyWing.SchemaExporter.Windows;
using CloudyWing.SchemaExporter.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CloudyWing.SchemaExporter;

/// <summary>
/// Interaction logic for MainWindow
/// </summary>
public partial class MainWindow : Window {
    private readonly ViewModel viewModel;
    private readonly IServiceProvider serviceProvider;
    private readonly IUpdateService updateService;

    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindow"/> class.
    /// </summary>
    /// <param name="viewModel">The view model to bind to this window.</param>
    /// <param name="serviceProvider">The service provider used to resolve child windows.</param>
    internal MainWindow(ViewModel viewModel, IServiceProvider serviceProvider, IUpdateService updateService) {
        ArgumentNullException.ThrowIfNull(viewModel, nameof(viewModel));
        ArgumentNullException.ThrowIfNull(serviceProvider, nameof(serviceProvider));
        ArgumentNullException.ThrowIfNull(updateService, nameof(updateService));
        InitializeComponent();

        this.viewModel = viewModel;
        this.serviceProvider = serviceProvider;
        this.updateService = updateService;
        DataContext = viewModel;
    }

    public Task InitializeAsync() {
        return viewModel.InitializeAsync();
    }

    public async Task CheckForUpdatesAsync() {
        try {
            Velopack.UpdateInfo? update = await updateService.CheckForUpdatesAsync();
            if (update is null) {
                return;
            }

            MessageBoxResult result = MessageBox.Show(
                $"偵測到新版本 {update.TargetFullRelease.Version}，是否立即下載並重新啟動套用更新？",
                "有可用更新",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information
            );
            if (result != MessageBoxResult.Yes) {
                return;
            }

            string originalStatusMessage = viewModel.StatusMessage;
            try {
                Progress<int> progress = new(percent => {
                    viewModel.StatusMessage = $"正在下載更新... {percent}%";
                });
                await updateService.DownloadUpdateAsync(update, progress);
            } finally {
                viewModel.StatusMessage = originalStatusMessage;
            }

            MessageBox.Show(
                "更新已下載完成，應用程式將重新啟動以套用新版本。",
                "準備更新",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
            updateService.ApplyUpdateAndRestart(update);
        } catch (Exception ex) {
            viewModel.StatusMessage = $"更新檢查未完成：{ex.Message}";
        }
    }

    private async void OpenSettingsButton_Click(object sender, RoutedEventArgs e) {
        try {
            SettingsWindow settingsWindow = serviceProvider.GetRequiredService<SettingsWindow>();
            settingsWindow.Owner = this;
            await settingsWindow.InitializeAsync();

            if (settingsWindow.ShowDialog() == true) {
                await viewModel.ReloadSettingsAsync();
            }
        } catch (Exception ex) when (ex is IOException or InvalidOperationException or JsonException) {
            MessageBox.Show($"無法開啟設定視窗：{ex.Message}", "設定載入", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
