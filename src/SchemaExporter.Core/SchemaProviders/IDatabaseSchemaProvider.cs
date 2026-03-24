namespace CloudyWing.SchemaExporter.Core.SchemaProviders;

internal interface IDatabaseSchemaProvider {
    DatabaseType DatabaseType { get; }

    /// <summary>
    /// 載入資料庫中所有資料表與檢視表的物件清單。
    /// </summary>
    Task<IReadOnlyList<DatabaseObjectSchema>> LoadObjectsAsync(
        string connectionString,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 根據已篩選的物件清單載入欄位、索引與程序明細。
    /// </summary>
    Task<DatabaseSchemaDetails> LoadDetailsAsync(
        string connectionString,
        IReadOnlyList<DatabaseObjectSchema> filteredObjects,
        CancellationToken cancellationToken = default
    );
}
