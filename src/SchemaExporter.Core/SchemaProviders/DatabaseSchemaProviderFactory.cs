namespace CloudyWing.SchemaExporter.Core.SchemaProviders;

internal sealed class DatabaseSchemaProviderFactory : IDatabaseSchemaProviderFactory {
    private readonly IReadOnlyDictionary<DatabaseType, IDatabaseSchemaProvider> providers;

    /// <inheritdoc/>
    public DatabaseSchemaProviderFactory(IEnumerable<IDatabaseSchemaProvider> providers) {
        ArgumentNullException.ThrowIfNull(providers, nameof(providers));

        this.providers = providers.ToDictionary(x => x.DatabaseType);
    }

    /// <inheritdoc/>
    public Task<DatabaseSchemaExport> LoadSchemaAsync(
        DatabaseType databaseType,
        string connectionString,
        CancellationToken cancellationToken = default
    ) {
        if (!providers.TryGetValue(databaseType, out IDatabaseSchemaProvider? provider)) {
            throw new NotSupportedException($"Database type '{databaseType}' is not supported.");
        }

        return provider.LoadSchemaAsync(connectionString, cancellationToken);
    }
}

