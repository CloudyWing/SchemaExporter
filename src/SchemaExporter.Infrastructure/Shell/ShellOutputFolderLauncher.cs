using System.Diagnostics;
using CloudyWing.SchemaExporter.Core.Exporting;

namespace CloudyWing.SchemaExporter.Infrastructure.Shell;

/// <summary>
/// 以作業系統 Shell 開啟輸出資料夾的 <see cref="IOutputFolderLauncher"/> 實作。
/// </summary>
public sealed class ShellOutputFolderLauncher : IOutputFolderLauncher {
    /// <inheritdoc/>
    public void Open(string directoryPath) {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);

        using Process? _ = Process.Start(new ProcessStartInfo {
            FileName = directoryPath,
            UseShellExecute = true
        });
    }
}
