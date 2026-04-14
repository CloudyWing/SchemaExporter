using System.IO;
using System.Text.Json;
using System.Windows;
using CloudyWing.SchemaExporter.Services;
using CloudyWing.SchemaExporter.Windows;
using Microsoft.Extensions.DependencyInjection;

namespace CloudyWing.SchemaExporter;

/// <summary>
/// MainWindow.xaml 的互動邏輯。
/// </summary>
public partial class MainWindow : Window {
    private readonly ViewModel viewModel;
    private readonly IServiceProvider serviceProvider;
    private readonly IUpdateService updateService;

    /// <summary>
    /// 初始化 <see cref="MainWindow"/> 類別的新執行個體。
    /// </summary>
    /// <param name="viewModel">要繫結至此視窗的 ViewModel。</param>
    /// <param name="serviceProvider">用於解析子視窗的服務提供者。</param>
    /// <param name="updateService">用於檢查與套用應用程式更新的服務。</param>
    internal MainWindow(ViewModel viewModel, IServiceProvider serviceProvider, IUpdateService updateService) {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(updateService);
        InitializeComponent();

        this.viewModel = viewModel;
        this.serviceProvider = serviceProvider;
        this.updateService = updateService;
        DataContext = viewModel;
    }

    /// <summary>
    /// 以目前的 ViewModel 狀態初始化視窗。
    /// </summary>
    /// <returns>代表非同步作業的工作。</returns>
    public Task InitializeAsync() {
        return viewModel.InitializeAsync();
    }

    /// <summary>
    /// 非同步檢查是否有可用更新，若有則提示使用者下載並重新啟動。
    /// </summary>
    /// <returns>代表非同步作業的工作。</returns>
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

            // 確保尚未啟動過新版的使用者，在更新前先將安裝目錄設定遷移至使用者目錄。
            AppPaths.EnsureUserConfigExistsIfInstallConfigExists();

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
