namespace CloudyWing.SchemaExporter.Core.Exporting;

/// <summary>
/// 表示資料庫索引的快照文件。
/// </summary>
public sealed class SchemaSnapshotIndexDocument {
    /// <summary>
    /// 取得索引名稱。
    /// </summary>
    public string IndexName { get; init; } = "";

    /// <summary>
    /// 取得此索引是否為主索引鍵的描述。
    /// </summary>
    public string IsPrimaryKey { get; init; } = "";

    /// <summary>
    /// 取得此索引是否為叢集索引的描述。
    /// </summary>
    public string IsClustered { get; init; } = "";

    /// <summary>
    /// 取得此索引是否為唯一索引的描述。
    /// </summary>
    public string IsUnique { get; init; } = "";

    /// <summary>
    /// 取得此索引是否為外部索引鍵的描述。
    /// </summary>
    public string IsForeignKey { get; init; } = "";

    /// <summary>
    /// 取得此索引包含的資料行。
    /// </summary>
    public string Columns { get; init; } = "";

    /// <summary>
    /// 取得與此索引相關的其他資料行。
    /// </summary>
    public string OtherColumns { get; init; } = "";
}

