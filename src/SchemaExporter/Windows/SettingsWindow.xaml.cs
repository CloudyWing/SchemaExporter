using System.IO;
using System.Text.Json;
using System.Windows;
using CloudyWing.SchemaExporter.Core.Exporting;
using CloudyWing.SchemaExporter.ViewModels;

namespace CloudyWing.SchemaExporter.Windows;

/// <summary>
/// 設定視窗的互動邏輯，提供連線與匯出設定檔的管理介面。
/// </summary>
public partial class SettingsWindow : Window {
    private readonly SettingsViewModel viewModel;

    /// <summary>
    /// 初始化 <see cref="SettingsWindow"/> 類別的新執行個體。
    /// </summary>
    /// <param name="viewModel">要繫結至此視窗的設定 ViewModel。</param>
    internal SettingsWindow(SettingsViewModel viewModel) {
        ArgumentNullException.ThrowIfNull(viewModel);
        InitializeComponent();
        this.viewModel = viewModel;
        DataContext = viewModel;
    }

    /// <summary>
    /// 非同步從設定服務載入設定，以初始化視窗內容。
    /// </summary>
    /// <returns>代表非同步作業的工作。</returns>
    public Task InitializeAsync() {
        return viewModel.LoadAsync();
    }

    private void AddConnectionButton_Click(object sender, RoutedEventArgs e) {
        viewModel.AddConnection();
    }

    private void RemoveConnectionButton_Click(object sender, RoutedEventArgs e) {
        viewModel.RemoveSelectedConnection();
    }

    private void AddProfileButton_Click(object sender, RoutedEventArgs e) {
        viewModel.AddProfile();
    }

    private void RemoveProfileButton_Click(object sender, RoutedEventArgs e) {
        viewModel.RemoveSelectedProfile();
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e) {
        try {
            await viewModel.SaveAsync();
            DialogResult = true;
        } catch (ExportValidationException ex) {
            MessageBox.Show(ex.Message, "設定驗證", MessageBoxButton.OK, MessageBoxImage.Warning);
        } catch (Exception ex) when (ex is IOException or InvalidOperationException or JsonException) {
            MessageBox.Show($"無法儲存設定：{ex.Message}", "設定儲存", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
