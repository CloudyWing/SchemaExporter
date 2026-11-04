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
        // 視窗關閉後背景作業仍可能回報錯誤，此時應用程式已結束，不再顯示對話框。
        Application? application = Application.Current;
        if (application is null) {
            return MessageBoxResult.None;
        }

        // MessageBox 必須在 WPF Dispatcher 上開啟並以主視窗為擁有者。
        return application.Dispatcher.Invoke(() => {
            Window? owner = application.MainWindow;
            return owner is null
                ? MessageBox.Show(message, title, button, image)
                : MessageBox.Show(owner, message, title, button, image);
        });
    }
}
