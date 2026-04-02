namespace CloudyWing.SchemaExporter.Core.Exporting.Snapshots;

/// <summary>
/// 表示資料庫索引的快照文件。
/// </summary>
public sealed class SchemaSnapshotIndexDocument {
    /// <summary>
    /// 取得索引名稱。
    /// </summary>
    public required string IndexName { get; init; }

    /// <summary>
    /// 取得此索引是否為主索引鍵的描述。
    /// </summary>
    public required string IsPrimaryKey { get; init; }

    /// <summary>
    /// 取得此索引是否為叢集索引的描述。
    /// </summary>
    public required string IsClustered { get; init; }

    /// <summary>
    /// 取得此索引是否為唯一索引的描述。
    /// </summary>
    public required string IsUnique { get; init; }

    /// <summary>
    /// 取得此索引是否為外部索引鍵的描述。
    /// </summary>
    public required string IsForeignKey { get; init; }

    /// <summary>
    /// 取得此索引包含的資料行。
    /// </summary>
    public required string Columns { get; init; }

    /// <summary>
    /// 取得與此索引相關的其他資料行。
    /// </summary>
    public required string OtherColumns { get; init; }
}

