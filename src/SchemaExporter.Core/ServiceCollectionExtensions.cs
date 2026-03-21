using CloudyWing.SchemaExporter.Exporting;
using CloudyWing.SchemaExporter.SchemaProviders;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CloudyWing.SchemaExporter;

/// <summary>
/// Provides dependency injection helpers for the shared schema exporter core services.
/// </summary>
public static class ServiceCollectionExtensions {
    /// <summary>
    /// Registers the shared schema exporter services and binds the schema configuration section.
    /// </summary>
    /// <param name="services">The service collection to update.</param>
    /// <param name="configuration">The application configuration root.</param>
    /// <returns>The updated service collection.</returns>
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
