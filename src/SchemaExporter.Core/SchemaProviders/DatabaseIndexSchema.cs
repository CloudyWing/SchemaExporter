namespace CloudyWing.SchemaExporter.Core.SchemaProviders;

/// <summary>
/// 表示與提供者無關的資料庫索引。
/// </summary>
public sealed class DatabaseIndexSchema {
    /// <summary>
    /// 取得或設定結構描述名稱。
    /// </summary>
    public string SchemaName { get; set; } = "";

    /// <summary>
    /// 取得或設定所屬物件名稱。
    /// </summary>
    public string ObjectName { get; set; } = "";

    /// <summary>
    /// 取得或設定所屬物件類型。
    /// </summary>
    public string ObjectType { get; set; } = "";

    /// <summary>
    /// 取得或設定索引名稱。
    /// </summary>
    public string IndexName { get; set; } = "";

    /// <summary>
    /// 取得或設定索引是否為主鍵。
    /// </summary>
    public string IsPrimaryKey { get; set; } = "";

    /// <summary>
    /// 取得或設定索引是否為叢集索引。
    /// </summary>
    public string IsClustered { get; set; } = "";

    /// <summary>
    /// 取得或設定索引是否為唯一索引。
    /// </summary>
    public string IsUnique { get; set; } = "";

    /// <summary>
    /// 取得或設定索引是否代表外鍵。
    /// </summary>
    public string IsForeignKey { get; set; } = "";

    /// <summary>
    /// 取得或設定索引資料行。
    /// </summary>
    public string Columns { get; set; } = "";

    /// <summary>
    /// 取得或設定非索引鍵或參考資料行。
    /// </summary>
    public string OtherColumns { get; set; } = "";

    /// <summary>
    /// 取得所屬物件索引鍵。
    /// </summary>
    public DatabaseObjectKey ObjectKey => new(SchemaName, ObjectName, ObjectType);
}

