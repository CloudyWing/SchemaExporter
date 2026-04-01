using System.IO;
using System.Text.Json;
using System.Windows;
using CloudyWing.SchemaExporter.Windows;
using Microsoft.Extensions.DependencyInjection;

namespace CloudyWing.SchemaExporter;

/// <summary>
/// Interaction logic for MainWindow
/// </summary>
public partial class MainWindow : Window {
    private readonly ViewModel viewModel;
    private readonly IServiceProvider serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindow"/> class.
    /// </summary>
    /// <param name="viewModel">The view model to bind to this window.</param>
    /// <param name="serviceProvider">The service provider used to resolve child windows.</param>
    public MainWindow(ViewModel viewModel, IServiceProvider serviceProvider) {
        ArgumentNullException.ThrowIfNull(viewModel, nameof(viewModel));
        ArgumentNullException.ThrowIfNull(serviceProvider, nameof(serviceProvider));
        InitializeComponent();

        this.viewModel = viewModel;
        this.serviceProvider = serviceProvider;
        DataContext = viewModel;
    }

    public Task InitializeAsync() {
        return viewModel.InitializeAsync();
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
