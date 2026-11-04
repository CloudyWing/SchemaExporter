using Microsoft.Extensions.Configuration;

namespace CloudyWing.SchemaExporter.Infrastructure.Configuration;

/// <summary>
/// 建立 Desktop 與 CLI 共用的組態來源。
/// </summary>
public static class SchemaExporterConfiguration {
    /// <summary>
    /// 建立應用程式組態。
    /// </summary>
    /// <param name="requiresUserConfig">若要讀取使用者設定檔（不存在時從出廠範本建立）則為 <see langword="true" />；否則為 <see langword="false" />，回傳空組態。</param>
    /// <returns>應用程式組態。</returns>
    /// <remarks>
    /// <c>diff</c>、<c>comment-sql</c>、說明與引數錯誤屬離線作業，呼叫端傳入 <see langword="false" />，
    /// 使用者設定檔缺漏或損毀時仍可執行，也不會因此建立設定檔。
    /// </remarks>
    public static IConfiguration Build(bool requiresUserConfig) {
        if (!requiresUserConfig) {
            return new ConfigurationBuilder().Build();
        }

        AppPaths.EnsureUserConfigExists();
        return new ConfigurationBuilder()
            .AddJsonFile(AppPaths.UserConfigFile, optional: false, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();
    }
}
