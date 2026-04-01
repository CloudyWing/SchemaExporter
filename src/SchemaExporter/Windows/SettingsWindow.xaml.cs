using System.IO;
using System.Text.Json;
using System.Windows;
using CloudyWing.SchemaExporter.Core.Exporting;
using CloudyWing.SchemaExporter.ViewModels;

namespace CloudyWing.SchemaExporter.Windows;

public partial class SettingsWindow : Window {
    private readonly SettingsViewModel viewModel;

    internal SettingsWindow(SettingsViewModel viewModel) {
        ArgumentNullException.ThrowIfNull(viewModel, nameof(viewModel));
        InitializeComponent();
        this.viewModel = viewModel;
        DataContext = viewModel;
    }

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
