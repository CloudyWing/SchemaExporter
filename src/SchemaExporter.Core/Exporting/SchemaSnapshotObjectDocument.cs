namespace CloudyWing.SchemaExporter.Core.Exporting;

/// <summary>
/// 表示資料庫物件（資料表或檢視表）的快照文件。
/// </summary>
public sealed class SchemaSnapshotObjectDocument {
    /// <summary>
    /// 取得結構描述名稱。
    /// </summary>
    public string SchemaName { get; init; } = "";

    /// <summary>
    /// 取得物件名稱。
    /// </summary>
    public string ObjectName { get; init; } = "";

    /// <summary>
    /// 取得物件類型（例如 TABLE、VIEW）。
    /// </summary>
    public string ObjectType { get; init; } = "";

    /// <summary>
    /// 取得物件描述。
    /// </summary>
    public string ObjectDescription { get; init; } = "";

    /// <summary>
    /// 取得屬於此物件的資料行集合。
    /// </summary>
    public IReadOnlyList<SchemaSnapshotColumnDocument> Columns { get; init; } = [];

    /// <summary>
    /// 取得屬於此物件的索引集合。
    /// </summary>
    public IReadOnlyList<SchemaSnapshotIndexDocument> Indexes { get; init; } = [];
}

