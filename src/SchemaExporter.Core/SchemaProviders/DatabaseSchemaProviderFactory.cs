namespace CloudyWing.SchemaExporter.Core.SchemaProviders;

/// <summary>
/// 根據資料庫類型選取對應的 <see cref="IDatabaseSchemaProvider"/> 並委派載入操作。
/// </summary>
internal sealed class DatabaseSchemaProviderFactory : IDatabaseSchemaProviderFactory {
    private readonly IReadOnlyDictionary<DatabaseType, IDatabaseSchemaProvider> providers;

    /// <summary>
    /// 初始化 <see cref="DatabaseSchemaProviderFactory"/> 類別的新執行個體。
    /// </summary>
    /// <param name="providers">已註冊的資料庫結構描述提供者集合。</param>
    public DatabaseSchemaProviderFactory(IEnumerable<IDatabaseSchemaProvider> providers) {
        ArgumentNullException.ThrowIfNull(providers);

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
