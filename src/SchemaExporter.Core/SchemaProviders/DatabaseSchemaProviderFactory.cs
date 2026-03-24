namespace CloudyWing.SchemaExporter.Core.SchemaProviders;

internal sealed class DatabaseSchemaProviderFactory : IDatabaseSchemaProviderFactory {
    private readonly IReadOnlyDictionary<DatabaseType, IDatabaseSchemaProvider> providers;

    /// <inheritdoc/>
    public DatabaseSchemaProviderFactory(IEnumerable<IDatabaseSchemaProvider> providers) {
        ArgumentNullException.ThrowIfNull(providers, nameof(providers));

        this.providers = providers.ToDictionary(x => x.DatabaseType);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<DatabaseObjectSchema>> LoadObjectsAsync(
        DatabaseType databaseType,
        string connectionString,
        CancellationToken cancellationToken = default
    ) {
        return GetProvider(databaseType).LoadObjectsAsync(connectionString, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<DatabaseSchemaDetails> LoadDetailsAsync(
        DatabaseType databaseType,
        string connectionString,
        IReadOnlyList<DatabaseObjectSchema> filteredObjects,
        CancellationToken cancellationToken = default
    ) {
        return GetProvider(databaseType).LoadDetailsAsync(connectionString, filteredObjects, cancellationToken);
    }

    private IDatabaseSchemaProvider GetProvider(DatabaseType databaseType) {
        if (!providers.TryGetValue(databaseType, out IDatabaseSchemaProvider? provider)) {
            throw new NotSupportedException($"Database type '{databaseType}' is not supported.");
        }

        return provider;
    }
}
