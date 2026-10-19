namespace CloudyWing.SchemaExporter.Core.SchemaProviders;

/// <summary>
/// 表示與提供者無關的資料庫索引。
/// </summary>
public sealed class DatabaseIndexSchema {
    /// <summary>
    /// 取得或設定結構描述名稱。
    /// </summary>
    public required string SchemaName { get; set; }

    /// <summary>
    /// 取得或設定所屬物件名稱。
    /// </summary>
    public required string ObjectName { get; set; }

    /// <summary>
    /// 取得或設定所屬物件類型。
    /// </summary>
    public required string ObjectType { get; set; }

    /// <summary>
    /// 取得或設定索引名稱。
    /// </summary>
    public required string IndexName { get; set; }

    /// <summary>
    /// 取得或設定索引是否為主鍵。
    /// </summary>
    public required string IsPrimaryKey { get; set; }

    /// <summary>
    /// 取得或設定索引是否為叢集索引。
    /// </summary>
    public required string IsClustered { get; set; }

    /// <summary>
    /// 取得或設定索引是否為唯一索引。
    /// </summary>
    public required string IsUnique { get; set; }

    /// <summary>
    /// 取得或設定索引資料行。
    /// </summary>
    public required string Columns { get; set; }

    /// <summary>
    /// 取得或設定索引包含資料行。
    /// </summary>
    public string? IncludedColumns { get; set; }

    /// <summary>
    /// 取得所屬物件索引鍵。
    /// </summary>
    public DatabaseObjectKey ObjectKey => new(SchemaName, ObjectName, ObjectType);
}

