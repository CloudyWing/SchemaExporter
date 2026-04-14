using System.IO;

namespace CloudyWing.SchemaExporter.Tests;

internal sealed class AppSettingsTestScope : IDisposable {
    private readonly string? originalLocalAppData;
    private readonly byte[]? originalInstallConfig;
    private readonly byte[]? originalInstallBackupConfig;
    private readonly string temporaryLocalAppDataRoot;

    public AppSettingsTestScope() {
        originalLocalAppData = Environment.GetEnvironmentVariable("LOCALAPPDATA");
        temporaryLocalAppDataRoot = Path.Combine(
            Path.GetTempPath(),
            "SchemaExporter.Tests",
            Guid.NewGuid().ToString("N")
        );
        Environment.SetEnvironmentVariable("LOCALAPPDATA", temporaryLocalAppDataRoot);

        InstallConfigPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        InstallBackupConfigPath = Path.Combine(AppContext.BaseDirectory, "appsettings.backup.json");
        UserConfigDirectory = AppPaths.UserConfigDirectory;
        UserConfigPath = AppPaths.UserConfigFile;

        originalInstallConfig = ReadBytesIfExists(InstallConfigPath);
        originalInstallBackupConfig = ReadBytesIfExists(InstallBackupConfigPath);
    }

    public string InstallConfigPath { get; }

    public string UserConfigDirectory { get; }

    public string UserConfigPath { get; }

    private string InstallBackupConfigPath { get; }

    public void Dispose() {
        RestoreFile(InstallConfigPath, originalInstallConfig);
        RestoreFile(InstallBackupConfigPath, originalInstallBackupConfig);
        Environment.SetEnvironmentVariable("LOCALAPPDATA", originalLocalAppData);

        if (Directory.Exists(temporaryLocalAppDataRoot)) {
            Directory.Delete(temporaryLocalAppDataRoot, recursive: true);
        }
    }

    private static byte[]? ReadBytesIfExists(string path) {
        return File.Exists(path) ? File.ReadAllBytes(path) : null;
    }

    private static void RestoreFile(string path, byte[]? content) {
        if (content is null) {
            if (File.Exists(path)) {
                File.Delete(path);
            }

            return;
        }

        File.WriteAllBytes(path, content);
    }
}
