namespace CloudyWing.SchemaExporter.Core.Exporting.Snapshots;

/// <summary>
/// 表示 schema snapshot 中的一筆外鍵欄位配對。
/// </summary>
public sealed class SchemaSnapshotRelationshipDocument {
    /// <summary>
    /// 取得或設定外鍵約束名稱。
    /// </summary>
    public required string ForeignKeyName { get; init; }

    /// <summary>
    /// 取得或設定來源結構描述名稱。
    /// </summary>
    public required string SourceSchemaName { get; init; }

    /// <summary>
    /// 取得或設定來源資料表名稱。
    /// </summary>
    public required string SourceTableName { get; init; }

    /// <summary>
    /// 取得或設定來源資料行名稱。
    /// </summary>
    public required string SourceColumnName { get; init; }

    /// <summary>
    /// 取得或設定參考結構描述名稱。
    /// </summary>
    public required string ReferencedSchemaName { get; init; }

    /// <summary>
    /// 取得或設定參考資料表名稱。
    /// </summary>
    public required string ReferencedTableName { get; init; }

    /// <summary>
    /// 取得或設定參考資料行名稱。
    /// </summary>
    public required string ReferencedColumnName { get; init; }

    /// <summary>
    /// 取得或設定複合外鍵中的欄位順序。
    /// </summary>
    public int ColumnOrder { get; init; }
}
