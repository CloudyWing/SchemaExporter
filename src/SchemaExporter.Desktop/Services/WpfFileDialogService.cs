using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace CloudyWing.SchemaExporter.Desktop.Services;

/// <summary>
/// 以 WPF 原生 <see cref="OpenFolderDialog"/> 與 <see cref="OpenFileDialog"/> 實作 <see cref="IFileDialogService"/>。
/// </summary>
internal sealed class WpfFileDialogService : IFileDialogService {
    /// <inheritdoc/>
    public string? PickFolder(string title, string? initialDirectory) {
        return Application.Current.Dispatcher.Invoke(() => {
            OpenFolderDialog dialog = new() {
                Title = title,
                Multiselect = false
            };
            if (!string.IsNullOrWhiteSpace(initialDirectory) && Directory.Exists(initialDirectory)) {
                dialog.InitialDirectory = initialDirectory;
            }

            return dialog.ShowDialog(Application.Current.MainWindow) == true ? dialog.FolderName : null;
        });
    }

    /// <inheritdoc/>
    public string? PickFile(string title, string filter, string? initialPath) {
        return Application.Current.Dispatcher.Invoke(() => {
            OpenFileDialog dialog = new() {
                Title = title,
                Filter = filter,
                CheckFileExists = true
            };
            string? initialDirectory = ResolveInitialDirectory(initialPath);
            if (initialDirectory is not null) {
                dialog.InitialDirectory = initialDirectory;
            }

            return dialog.ShowDialog(Application.Current.MainWindow) == true ? dialog.FileName : null;
        });
    }

    private static string? ResolveInitialDirectory(string? initialPath) {
        if (string.IsNullOrWhiteSpace(initialPath)) {
            return null;
        }

        if (Directory.Exists(initialPath)) {
            return initialPath;
        }

        string? directory = Path.GetDirectoryName(initialPath);
        return !string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory) ? directory : null;
    }
}
