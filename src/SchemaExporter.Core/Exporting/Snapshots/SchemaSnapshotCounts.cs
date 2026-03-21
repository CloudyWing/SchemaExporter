namespace CloudyWing.SchemaExporter.Core.Exporting.Snapshots;

/// <summary>
/// 表示 schema snapshot 的數量統計資訊。
/// </summary>
public sealed class SchemaSnapshotCounts {
    /// <summary>
    /// 取得或設定物件數量。
    /// </summary>
    public int Objects { get; init; }

    /// <summary>
    /// 取得或設定欄位數量。
    /// </summary>
    public int Columns { get; init; }

    /// <summary>
    /// 取得或設定索引數量。
    /// </summary>
    public int Indexes { get; init; }

    /// <summary>
    /// 取得或設定程序與函數數量。
    /// </summary>
    public int Routines { get; init; }
}

