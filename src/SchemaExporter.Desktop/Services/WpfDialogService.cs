using System.Windows;

namespace CloudyWing.SchemaExporter.Desktop.Services;

/// <summary>
/// 以 WPF <see cref="MessageBox"/> 實作 <see cref="IDialogService"/>。
/// </summary>
internal sealed class WpfDialogService : IDialogService {
    /// <inheritdoc/>
    public void ShowInformation(string title, string message) {
        Show(title, message, MessageBoxButton.OK, MessageBoxImage.Information);
    }

    /// <inheritdoc/>
    public void ShowWarning(string title, string message) {
        Show(title, message, MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    /// <inheritdoc/>
    public void ShowError(string title, string message) {
        Show(title, message, MessageBoxButton.OK, MessageBoxImage.Error);
    }

    /// <inheritdoc/>
    public bool Confirm(string title, string message) {
        return Show(title, message, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
    }

    private static MessageBoxResult Show(string title, string message, MessageBoxButton button, MessageBoxImage image) {
        // Blazor 事件可能在非 UI 執行緒延續，MessageBox 必須在 WPF Dispatcher 上開啟並以主視窗為擁有者。
        return Application.Current.Dispatcher.Invoke(() => {
            Window? owner = Application.Current.MainWindow;
            return owner is null
                ? MessageBox.Show(message, title, button, image)
                : MessageBox.Show(owner, message, title, button, image);
        });
    }
}
