using CloudyWing.SchemaExporter.Core.Exporting;
using CloudyWing.SchemaExporter.Core.Exporting.Snapshots;
using CloudyWing.SchemaExporter.Core.SchemaProviders;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CloudyWing.SchemaExporter.Core;

/// <summary>
/// 提供結構描述匯出核心服務的相依性注入輔助方法。
/// </summary>
public static class ServiceCollectionExtensions {
    /// <summary>
    /// 註冊結構描述匯出核心服務，並繫結結構描述組態區段。
    /// </summary>
    /// <param name="services">要更新的服務集合。</param>
    /// <param name="configuration">應用程式組態根物件。</param>
    /// <returns>已更新的服務集合。</returns>
    public static IServiceCollection AddSchemaExporterCore(this IServiceCollection services, IConfiguration configuration) {
        ArgumentNullException.ThrowIfNull(services, nameof(services));
        ArgumentNullException.ThrowIfNull(configuration, nameof(configuration));

        services.AddLogging();
        services.AddSingleton(configuration);
        services.Configure<SchemaOptions>(configuration.GetSection(SchemaOptions.OptionsName));
        services.AddSingleton<IDatabaseSchemaProvider, SqlServerDatabaseSchemaProvider>();
        services.AddSingleton<IDatabaseSchemaProvider, OracleDatabaseSchemaProvider>();
        services.AddSingleton<IDatabaseSchemaProviderFactory, DatabaseSchemaProviderFactory>();
        services.AddSingleton<SchemaSnapshotDiffService>();
        services.AddSingleton<SchemaExportOrchestrator>();

        return services;
    }
}

