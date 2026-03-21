namespace CloudyWing.SchemaExporter.Core.SchemaProviders;

/// <summary>
/// 建立適用於已設定資料庫類型的結構描述提供者。
/// </summary>
public interface IDatabaseSchemaProviderFactory {
    /// <summary>
    /// 使用符合指定資料庫類型的提供者載入資料庫結構描述中繼資料。
    /// </summary>
    /// <param name="databaseType">已設定的資料庫提供者類型。</param>
    /// <param name="connectionString">資料庫連接字串。</param>
    /// <param name="cancellationToken">取消語彙基元。</param>
    /// <returns>已載入的結構描述匯出模型。</returns>
    Task<DatabaseSchemaExport> LoadSchemaAsync(
        DatabaseType databaseType,
        string connectionString,
        CancellationToken cancellationToken = default
    );
}

