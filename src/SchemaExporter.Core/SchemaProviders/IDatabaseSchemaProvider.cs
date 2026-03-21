namespace CloudyWing.SchemaExporter.Core.SchemaProviders;

internal interface IDatabaseSchemaProvider {
    DatabaseType DatabaseType { get; }

    Task<DatabaseSchemaExport> LoadSchemaAsync(string connectionString, CancellationToken cancellationToken = default);
}

