namespace CloudyWing.SchemaExporter.Core.SchemaProviders;

/// <summary>
/// 定義針對特定資料庫類型載入結構描述資料的提供者合約。
/// </summary>
internal interface IDatabaseSchemaProvider {
    /// <summary>
    /// 取得此提供者所支援的資料庫類型。
    /// </summary>
    DatabaseType DatabaseType { get; }

    /// <summary>
    /// 載入資料庫中所有資料表與檢視表的物件清單。
    /// </summary>
    /// <param name="connectionString">資料庫連接字串。</param>
    /// <param name="cancellationToken">取消語彙基元。</param>
    /// <returns>資料庫物件清單。</returns>
    Task<IReadOnlyList<DatabaseObjectSchema>> LoadObjectsAsync(
        string connectionString,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 根據已篩選的物件清單載入欄位、索引與程序明細。
    /// </summary>
    /// <param name="connectionString">資料庫連接字串。</param>
    /// <param name="filteredObjects">經篩選後的資料庫物件清單。</param>
    /// <param name="cancellationToken">取消語彙基元。</param>
    /// <returns>欄位、索引與程序明細。</returns>
    Task<DatabaseSchemaDetails> LoadDetailsAsync(
        string connectionString,
        IReadOnlyList<DatabaseObjectSchema> filteredObjects,
        CancellationToken cancellationToken = default
    );
}
