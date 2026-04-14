using System.IO;

namespace CloudyWing.SchemaExporter;

/// <summary>
/// 提供應用程式各路徑的靜態存取與使用者設定檔初始化輔助。
/// </summary>
internal static class AppPaths {
    private const string ApplicationDirectoryName = "SchemaExporter";
    private const string AppSettingsFileName = "appsettings.json";

    /// <summary>
    /// 取得使用者設定目錄路徑（%LocalAppData%\SchemaExporter）。
    /// </summary>
    internal static string UserConfigDirectory => Path.Combine(GetLocalAppDataDirectory(), ApplicationDirectoryName);

    /// <summary>
    /// 取得使用者設定檔完整路徑（%LocalAppData%\SchemaExporter\appsettings.json）。
    /// </summary>
    internal static string UserConfigFile => Path.Combine(UserConfigDirectory, AppSettingsFileName);

    /// <summary>
    /// 取得安裝目錄的出廠設定範本路徑（AppContext.BaseDirectory\appsettings.json）。
    /// </summary>
    internal static string InstallConfigFile => Path.Combine(AppContext.BaseDirectory, AppSettingsFileName);

    /// <summary>
    /// 確保使用者設定檔存在，不存在時從安裝目錄複製預設設定。
    /// </summary>
    internal static void EnsureUserConfigExists() {
        if (File.Exists(UserConfigFile)) {
            return;
        }

        Directory.CreateDirectory(UserConfigDirectory);
        File.Copy(InstallConfigFile, UserConfigFile);
    }

    /// <summary>
    /// 嘗試在更新前將安裝目錄設定檔遷移至使用者目錄。
    /// </summary>
    internal static void EnsureUserConfigExistsIfInstallConfigExists() {
        if (File.Exists(UserConfigFile) || !File.Exists(InstallConfigFile)) {
            return;
        }

        Directory.CreateDirectory(UserConfigDirectory);
        File.Copy(InstallConfigFile, UserConfigFile);
    }

    private static string GetLocalAppDataDirectory() {
        string? localAppData = Environment.GetEnvironmentVariable("LOCALAPPDATA");
        return string.IsNullOrWhiteSpace(localAppData)
            ? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
            : localAppData;
    }
}
