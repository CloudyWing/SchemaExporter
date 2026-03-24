namespace CloudyWing.SchemaExporter.Core.SchemaProviders;

/// <summary>
/// 建立適用於已設定資料庫類型的結構描述提供者。
/// </summary>
public interface IDatabaseSchemaProviderFactory {
    /// <summary>
    /// 載入指定資料庫中所有資料表與檢視表的物件清單。
    /// </summary>
    /// <param name="databaseType">已設定的資料庫提供者類型。</param>
    /// <param name="connectionString">資料庫連接字串。</param>
    /// <param name="cancellationToken">取消語彙基元。</param>
    /// <returns>資料庫物件清單。</returns>
    Task<IReadOnlyList<DatabaseObjectSchema>> LoadObjectsAsync(
        DatabaseType databaseType,
        string connectionString,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 根據已篩選的物件清單載入欄位、索引與程序明細。
    /// </summary>
    /// <param name="databaseType">已設定的資料庫提供者類型。</param>
    /// <param name="connectionString">資料庫連接字串。</param>
    /// <param name="filteredObjects">經篩選後的資料庫物件清單。</param>
    /// <param name="cancellationToken">取消語彙基元。</param>
    /// <returns>欄位、索引與程序明細。</returns>
    Task<DatabaseSchemaDetails> LoadDetailsAsync(
        DatabaseType databaseType,
        string connectionString,
        IReadOnlyList<DatabaseObjectSchema> filteredObjects,
        CancellationToken cancellationToken = default
    );
}
