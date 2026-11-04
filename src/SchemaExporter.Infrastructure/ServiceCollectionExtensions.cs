using CloudyWing.SchemaExporter.Core.Exporting;
using CloudyWing.SchemaExporter.Infrastructure.Configuration;
using CloudyWing.SchemaExporter.Infrastructure.Shell;
using Microsoft.Extensions.DependencyInjection;

namespace CloudyWing.SchemaExporter.Infrastructure;

/// <summary>
/// 提供跨 Host 共用基礎服務的相依性注入輔助方法。
/// </summary>
public static class ServiceCollectionExtensions {
    /// <summary>
    /// 註冊使用者設定存取與開啟輸出資料夾等跨 Host 共用服務。
    /// </summary>
    /// <param name="services">要更新的服務集合。</param>
    /// <returns>已更新的服務集合。</returns>
    public static IServiceCollection AddSchemaExporterInfrastructure(this IServiceCollection services) {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<ISettingsService, JsonSettingsService>();
        services.AddSingleton<IOutputFolderLauncher, ShellOutputFolderLauncher>();

        return services;
    }
}
