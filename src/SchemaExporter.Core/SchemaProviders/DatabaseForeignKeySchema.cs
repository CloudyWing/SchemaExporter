namespace CloudyWing.SchemaExporter.Core.SchemaProviders;

/// <summary>
/// 表示資料庫外鍵的一組來源欄位與參考欄位配對。
/// </summary>
public sealed class DatabaseForeignKeySchema {
    /// <summary>
    /// 取得外鍵約束名稱。
    /// </summary>
    public required string ForeignKeyName { get; init; }

    /// <summary>
    /// 取得來源結構描述名稱。
    /// </summary>
    public required string SourceSchemaName { get; init; }

    /// <summary>
    /// 取得來源資料表名稱。
    /// </summary>
    public required string SourceTableName { get; init; }

    /// <summary>
    /// 取得來源資料行名稱。
    /// </summary>
    public required string SourceColumnName { get; init; }

    /// <summary>
    /// 取得參考結構描述名稱。
    /// </summary>
    public required string ReferencedSchemaName { get; init; }

    /// <summary>
    /// 取得參考資料表名稱。
    /// </summary>
    public required string ReferencedTableName { get; init; }

    /// <summary>
    /// 取得參考資料行名稱。
    /// </summary>
    public required string ReferencedColumnName { get; init; }

    /// <summary>
    /// 取得複合外鍵中的欄位順序。
    /// </summary>
    public int ColumnOrder { get; init; }
}
