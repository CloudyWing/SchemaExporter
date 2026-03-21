namespace CloudyWing.SchemaExporter.SchemaProviders;

/// <summary>
/// Creates schema providers for configured database types.
/// </summary>
public interface IDatabaseSchemaProviderFactory {
    /// <summary>
    /// Loads database schema metadata with the provider matching the specified database type.
    /// </summary>
    /// <param name="databaseType">The configured database provider type.</param>
    /// <param name="connectionString">The database connection string.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The loaded schema export model.</returns>
    Task<DatabaseSchemaExport> LoadSchemaAsync(
        DatabaseType databaseType,
        string connectionString,
        CancellationToken cancellationToken = default
    );
}
